using System.Security.Claims;
using System.Security.Cryptography;
using HCS.DocumentService.Conversion;
using HCS.DocumentService.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.BlobStoring;

namespace HCS.DocumentService.Documents;

public sealed class DocumentFileService(DocumentServiceDbContext db, IBlobContainer<DocumentBlobContainer> blobs,
    IHttpContextAccessor httpContext, IDocxToPdfConverter converter, ILogger<DocumentFileService> logger)
{
    public const long MaxFileSize = 50 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "image/png", "image/jpeg" };

    public async Task<DocumentFileDto> UploadAsync(Guid documentId, string fileName, string contentType, Stream content, long size, CancellationToken cancellationToken)
    {
        var (principal, userId) = RequireCurrentUser(DocumentPermissions.ManageFiles);
        if (size > MaxFileSize) throw new InvalidOperationException("File size is outside the allowed range.");
        var bytes = await ReadAllBytesAsync(content, cancellationToken);
        if (bytes.Length <= 0 || bytes.Length > MaxFileSize) throw new InvalidOperationException("File size is outside the allowed range.");
        if (!TryNormalizeContentType(fileName, contentType, out var normalizedType))
            throw new InvalidOperationException("File type is not allowed.");
        var document = await LoadManagedDocumentAsync(documentId, userId, principal, cancellationToken);
        var fileId = Guid.NewGuid();
        var blobName = BlobNamePolicy.Document(documentId, fileId);
        await SaveBlobAsync(blobName, bytes, cancellationToken);
        try
        {
            var existingFileIds = document.Files.Select(x => x.Id).ToHashSet();
            var existingHistoryIds = document.History.Select(x => x.Id).ToHashSet();
            var file = document.AddFile(fileId, fileName, normalizedType, bytes.Length, Sha256Hex(bytes), blobName, userId, DateTime.UtcNow);
            try
            {
                await TryAttachConvertedPdfAsync(document, file, bytes, userId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Word-to-PDF conversion failed for {File}; Word file was kept.", fileName);
            }
            TrackNewChildren(db, document, existingFileIds, existingHistoryIds);
            await db.SaveChangesAsync(cancellationToken);
            return Map(file);
        }
        catch
        {
            await blobs.DeleteAsync(blobName, cancellationToken: cancellationToken);
            throw;
        }
    }

    public async Task<(DocumentFile File, Stream Content)> OpenAuthorizedAsync(Guid documentId, Guid fileId, CancellationToken cancellationToken)
    {
        var (principal, userId) = RequireCurrentUser(DocumentPermissions.View);
        var document = await db.Documents.AsNoTracking().Include(x => x.Assignments).Include(x => x.History)
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken)
            ?? throw new KeyNotFoundException("Document not found.");
        DocumentAccess.EnsureCanView(document, userId, principal);
        var file = await db.DocumentFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId && x.DocumentId == documentId && !x.IsPendingDeletion, cancellationToken)
            ?? throw new KeyNotFoundException("Document file not found.");
        return (file, await blobs.GetAsync(file.BlobName, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid documentId, Guid fileId, CancellationToken cancellationToken)
    {
        var (principal, userId) = RequireCurrentUser(DocumentPermissions.ManageFiles);
        var document = await LoadManagedDocumentAsync(documentId, userId, principal, cancellationToken);
        var file = document.BeginFileDeletion(fileId, userId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await blobs.DeleteAsync(file.BlobName, cancellationToken: cancellationToken);
        document.CompleteFileDeletion(fileId, userId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CopyFilesAsync(DocumentAggregate source, DocumentAggregate target, Guid? actorUserId, DateTime now,
        CancellationToken cancellationToken)
    {
        var copiedFiles = new Dictionary<Guid, DocumentFile>();
        foreach (var file in source.Files.Where(x => !x.IsPendingDeletion))
        {
            await using var content = await blobs.GetAsync(file.BlobName, cancellationToken: cancellationToken);
            await using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            copy.Position = 0;
            var fileId = Guid.NewGuid();
            var blobName = BlobNamePolicy.Document(target.Id, fileId);
            await blobs.SaveAsync(blobName, copy, overrideExisting: false, cancellationToken: cancellationToken);
            copiedFiles[file.Id] = target.AddFile(fileId, file.FileName, file.ContentType, file.Size, file.Sha256,
                blobName, actorUserId, now);
        }

        foreach (var file in source.Files.Where(x => !x.IsPendingDeletion))
        {
            if (file.PairedFileId is { } pairedId
                && copiedFiles.TryGetValue(file.Id, out var copiedFile)
                && copiedFiles.TryGetValue(pairedId, out var copiedPair))
            {
                copiedFile.SetPairedFileId(copiedPair.Id);
            }
        }
    }

    public async Task AttachBlobAsync(DocumentAggregate document, string fileName, string contentType, Stream content,
        Guid? actorUserId, DateTime now, CancellationToken cancellationToken)
    {
        var bytes = await ReadAllBytesAsync(content, cancellationToken);
        var fileId = Guid.NewGuid();
        var blobName = BlobNamePolicy.Document(document.Id, fileId);
        await SaveBlobAsync(blobName, bytes, cancellationToken);
        document.AddFile(fileId, fileName, contentType, bytes.Length, Sha256Hex(bytes), blobName, actorUserId, now);
    }

    /// <summary>
    /// Stores a newly rendered DOCX/PDF pair without mutating either source file.
    /// The pair is attached to the same document and linked in both directions so
    /// subsequent signing steps can continue from the Word working copy.
    /// </summary>
    public async Task<(DocumentFile WordFile, DocumentFile PdfFile, string WordBlobName, string PdfBlobName)>
        AddDocxPdfPairAsync(DocumentAggregate document, byte[] docxBytes, byte[] pdfBytes,
            string docxFileName, string pdfFileName, Guid? actorUserId, DateTime now,
            CancellationToken cancellationToken)
    {
        if (docxBytes is not { Length: > 0 }) throw new ArgumentException("DOCX content is required.", nameof(docxBytes));
        if (pdfBytes is not { Length: > 0 }) throw new ArgumentException("PDF content is required.", nameof(pdfBytes));

        var wordId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var wordBlobName = BlobNamePolicy.Document(document.Id, wordId);
        var pdfBlobName = BlobNamePolicy.Document(document.Id, pdfId);
        try
        {
            await SaveBlobAsync(wordBlobName, docxBytes, cancellationToken);
            await SaveBlobAsync(pdfBlobName, pdfBytes, cancellationToken);

            var existingFileIds = document.Files.Select(x => x.Id).ToHashSet();
            var existingHistoryIds = document.History.Select(x => x.Id).ToHashSet();
            var wordFile = document.AddFile(wordId, docxFileName,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", docxBytes.Length,
                Sha256Hex(docxBytes), wordBlobName, actorUserId, now);
            var pdfFile = document.AddFile(pdfId, pdfFileName, "application/pdf", pdfBytes.Length,
                Sha256Hex(pdfBytes), pdfBlobName, actorUserId, now);
            wordFile.SetPairedFileId(pdfFile.Id);
            pdfFile.SetPairedFileId(wordFile.Id);
            TrackNewChildren(db, document, existingFileIds, existingHistoryIds);
            return (wordFile, pdfFile, wordBlobName, pdfBlobName);
        }
        catch
        {
            await DeleteBlobIfCreatedAsync(wordBlobName, cancellationToken);
            await DeleteBlobIfCreatedAsync(pdfBlobName, cancellationToken);
            throw;
        }
    }

    private async Task TryAttachConvertedPdfAsync(DocumentAggregate document, DocumentFile wordFile, byte[] docxBytes,
        Guid? actorUserId, CancellationToken cancellationToken)
    {
        if (!IsDocx(wordFile.FileName, wordFile.ContentType)) return;
        if (!converter.IsAvailable)
        {
            logger.LogInformation("Skipping Word-to-PDF conversion because LibreOffice is not available.");
            return;
        }
        var pdfBytes = await converter.ConvertAsync(docxBytes, cancellationToken);
        if (pdfBytes is null or { Length: 0 })
        {
            logger.LogWarning("Word-to-PDF conversion produced no output for {File}.", wordFile.FileName);
            return;
        }
        var pdfId = Guid.NewGuid();
        var pdfName = Path.ChangeExtension(wordFile.FileName, ".pdf");
        var pdfBlob = BlobNamePolicy.Document(document.Id, pdfId);
        await SaveBlobAsync(pdfBlob, pdfBytes, cancellationToken);
        var pdfFile = document.AddFile(pdfId, pdfName, "application/pdf", pdfBytes.Length, Sha256Hex(pdfBytes), pdfBlob, actorUserId, DateTime.UtcNow);
        wordFile.SetPairedFileId(pdfFile.Id);
        pdfFile.SetPairedFileId(wordFile.Id);
    }

    internal static DocumentFileDto Map(DocumentFile file) =>
        new(file.Id, file.FileName, file.ContentType, file.Size, file.Sha256, file.CreationTime, file.PairedFileId);

    internal static void TrackNewChildren(DocumentServiceDbContext db, DocumentAggregate document,
        IReadOnlySet<Guid> existingFileIds, IReadOnlySet<Guid> existingHistoryIds)
    {
        // A file upload mutates an already tracked aggregate. Explicitly add only the
        // children created by this mutation so EF does not treat them as existing rows
        // and issue an UPDATE for a key that is not in the database yet.
        db.DocumentFiles.AddRange(document.Files.Where(x => !existingFileIds.Contains(x.Id)));
        db.DocumentHistories.AddRange(document.History.Where(x => !existingHistoryIds.Contains(x.Id)));
    }

    public static bool TryNormalizeContentType(string fileName, string? contentType, out string normalized)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension.Length > 0)
        {
            normalized = extension switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => ""
            };
            return normalized.Length > 0;
        }

        normalized = contentType?.Trim() ?? "";
        return AllowedTypes.Contains(normalized);
    }

    private (ClaimsPrincipal Principal, Guid UserId) RequireCurrentUser(string permission)
    {
        var principal = httpContext.HttpContext?.User ?? new ClaimsPrincipal();
        var userId = DocumentAccess.RequireUser(principal);
        DocumentAccess.RequirePermission(principal, permission);
        return (principal, userId);
    }

    private async Task<DocumentAggregate> LoadManagedDocumentAsync(Guid documentId, Guid userId, ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var document = await db.Documents.Include(x => x.Files).Include(x => x.History)
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken)
            ?? throw new KeyNotFoundException("Document not found.");
        await db.Entry(document).Collection(x => x.Assignments).LoadAsync(cancellationToken);
        DocumentAccess.EnsureCanManage(document, userId, principal);
        return document;
    }

    private async Task SaveBlobAsync(string blobName, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(bytes);
        await blobs.SaveAsync(blobName, stream, overrideExisting: false, cancellationToken: cancellationToken);
    }

    private async Task DeleteBlobIfCreatedAsync(string blobName, CancellationToken cancellationToken)
    {
        try
        {
            await blobs.DeleteAsync(blobName, cancellationToken: cancellationToken);
        }
        catch
        {
            // Blob cleanup is best effort; the transaction has not been committed yet.
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream content, CancellationToken cancellationToken)
    {
        await using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        return copy.ToArray();
    }

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool IsDocx(string fileName, string contentType) =>
        fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
        || string.Equals(contentType, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase);
}
