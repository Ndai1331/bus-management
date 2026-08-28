using System.Security.Cryptography;
using HCS.DocumentService.Conversion;
using HCS.DocumentService.Documents;
using HCS.DocumentService.Signing;
using HCS.DocumentService.Storage;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.BlobStoring;

namespace HCS.DocumentService.Workflows;

public sealed class WorkflowSubmissionPreparationService(
    DocumentServiceDbContext db,
    IBlobContainer<DocumentBlobContainer> documentBlobs,
    IBlobContainer<SigningBlobContainer> signingBlobs,
    IDocxToPdfConverter converter,
    IWorkflowUserProfileResolver profileResolver,
    DocumentFileService files)
{
    public async Task PrepareAsync(DocumentAggregate document, Guid actorUserId, WorkflowDefinition definition,
        string? signingContent, CancellationToken cancellationToken = default)
    {
        if (!definition.Steps.Any(x => string.Equals(x.Type, WorkflowStepTypes.Sign, StringComparison.OrdinalIgnoreCase)))
            return;

        var createdBlobNames = new List<string>();
        try
        {
            await PrepareCoreAsync(document, actorUserId, definition, signingContent, createdBlobNames, cancellationToken);
        }
        catch
        {
            foreach (var blobName in createdBlobNames)
            {
                try
                {
                    await documentBlobs.DeleteAsync(blobName, cancellationToken: CancellationToken.None);
                }
                catch
                {
                    // Cleanup is best effort; the original document metadata is not committed here.
                }
            }

            throw;
        }
    }

    private async Task PrepareCoreAsync(DocumentAggregate document, Guid actorUserId, WorkflowDefinition definition,
        string? signingContent, List<string> createdBlobNames, CancellationToken cancellationToken)
    {

        var file = document.Files.Where(x => !x.IsPendingDeletion)
            .OrderByDescending(x => IsWord(x))
            .ThenByDescending(x => x.CreationTime)
            .FirstOrDefault();
        if (file is null)
            throw new InvalidOperationException("A document file is required before presenting the document for signing.");

        var signature = await db.UserSignatures.AsNoTracking()
            .Where(x => x.UserId == actorUserId && x.Type == UserSignatureType.Electronic && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.CreationTime)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The submitter must configure an active electronic signature before presenting the document for signing.");

        var now = DateTime.UtcNow;
        if (signature.ValidFrom.HasValue && signature.ValidFrom.Value > now)
            throw new InvalidOperationException("The submitter's electronic signature is not yet valid.");
        if (signature.ValidTo.HasValue && signature.ValidTo.Value < now)
            throw new InvalidOperationException("The submitter's electronic signature has expired.");

        var profile = await profileResolver.ResolveAsync(actorUserId, cancellationToken);
        var fullName = string.IsNullOrWhiteSpace(profile.FullName) ? actorUserId.ToString("N") : profile.FullName;
        await using var signatureStream = await signingBlobs.GetAsync(signature.BlobName, cancellationToken);
        await using var signatureBuffer = new MemoryStream();
        await signatureStream.CopyToAsync(signatureBuffer, cancellationToken);
        var signatureBytes = signatureBuffer.ToArray();
        if (signatureBytes.Length == 0)
            throw new InvalidOperationException("The submitter's electronic signature image is empty.");
        var renderedSignatureBytes = ElectronicSignatureLayoutComposer.Compose(signatureBytes);

        await using var fileStream = await documentBlobs.GetAsync(file.BlobName, cancellationToken);
        await using var fileBuffer = new MemoryStream();
        await fileStream.CopyToAsync(fileBuffer, cancellationToken);
        var sourceBytes = fileBuffer.ToArray();

        if (IsWord(file))
        {
            if (string.IsNullOrWhiteSpace(signingContent))
                throw new InvalidOperationException("Signing content is required for a Word signing document.");
            await PrepareWordAsync(document, actorUserId, file, sourceBytes, renderedSignatureBytes, fullName,
                profile, signingContent, now, createdBlobNames, cancellationToken);
        }
        else if (IsPdf(file))
        {
            var replaced = PdfPlaceholderReplacer.ReplacePrepared(sourceBytes, signatureBytes, fullName,
                profile.PositionName, profile.DepartmentName, signingContent, now);
            if (!sourceBytes.AsSpan().SequenceEqual(replaced))
                await ReplaceBlobAsync(file, replaced, createdBlobNames, cancellationToken);
        }
    }

    private async Task PrepareWordAsync(
        DocumentAggregate document,
        Guid actorUserId,
        DocumentFile wordFile,
        byte[] sourceBytes,
        byte[] signatureBytes,
        string fullName,
        WorkflowUserProfile profile,
        string signingContent,
        DateTime now,
        List<string> createdBlobNames,
        CancellationToken cancellationToken)
    {
        var replacedWord = WordPlaceholderReplacer.ReplacePrepared(sourceBytes, signatureBytes, fullName,
            profile.PositionName, profile.DepartmentName, signingContent, now);
        if (!converter.IsAvailable)
            throw new InvalidOperationException("LibreOffice is required to prepare a Word signing document.");
        var pdfBytes = await converter.ConvertAsync(replacedWord, cancellationToken)
            ?? throw new InvalidOperationException("The Word signing document could not be converted to PDF.");
        if (pdfBytes.Length == 0)
            throw new InvalidOperationException("The Word signing document could not be converted to PDF.");

        // Keep the uploaded/template Word and its original PDF immutable. The
        // rendered submission is a new pair and becomes the next signing input.
        if (sourceBytes.AsSpan().SequenceEqual(replacedWord))
        {
            if (document.Files.Any(x => x.Id == wordFile.PairedFileId && IsPdf(x))) return;
            var generatedPdfId = Guid.NewGuid();
            var generatedPdfBlob = await SaveBlobAsync(document.Id, pdfBytes, createdBlobNames, cancellationToken);
            var generatedPdf = document.AddFile(generatedPdfId,
                BuildDerivedFileName(wordFile.FileName, "-prepared", ".pdf"), "application/pdf", pdfBytes.Length,
                Sha256Hex(pdfBytes), generatedPdfBlob, actorUserId, now);
            wordFile.SetPairedFileId(generatedPdf.Id);
            generatedPdf.SetPairedFileId(wordFile.Id);
            return;
        }
        var pair = await files.AddDocxPdfPairAsync(document, replacedWord, pdfBytes,
            BuildDerivedFileName(wordFile.FileName, "-prepared", ".docx"),
            BuildDerivedFileName(wordFile.FileName, "-prepared", ".pdf"),
            actorUserId, now, cancellationToken);
        createdBlobNames.Add(pair.WordBlobName);
        createdBlobNames.Add(pair.PdfBlobName);
    }

    private async Task ReplaceBlobAsync(DocumentFile file, byte[] bytes, List<string> createdBlobNames,
        CancellationToken cancellationToken)
    {
        var blobName = await SaveBlobAsync(file.DocumentId, bytes, createdBlobNames, cancellationToken);
        file.ReplaceContent(bytes.Length, Sha256Hex(bytes), blobName);
    }

    private async Task<string> SaveBlobAsync(Guid documentId, byte[] bytes, List<string> createdBlobNames,
        CancellationToken cancellationToken)
    {
        var blobName = BlobNamePolicy.Document(documentId, Guid.NewGuid());
        await using var stream = new MemoryStream(bytes);
        await documentBlobs.SaveAsync(blobName, stream, overrideExisting: false, cancellationToken: cancellationToken);
        createdBlobNames.Add(blobName);
        return blobName;
    }

    private static bool IsWord(DocumentFile file) =>
        file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
        || string.Equals(file.ContentType, "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsPdf(DocumentFile file) =>
        file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
        || string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string BuildDerivedFileName(string fileName, string suffix, string extension)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem)) stem = "workflow";
        var maxStemLength = Math.Max(1, 256 - suffix.Length - extension.Length);
        if (stem.Length > maxStemLength) stem = stem[..maxStemLength];
        return $"{stem}{suffix}{extension}";
    }
}
