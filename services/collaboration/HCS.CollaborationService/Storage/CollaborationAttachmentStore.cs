using HCS.CollaborationService.Contracts;
using HCS.CollaborationService.Data;
using HCS.CollaborationService.Domain;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Users;

namespace HCS.CollaborationService.Storage;

[BlobContainerName("hcs-collaboration")]
public sealed class CollaborationAttachmentContainer;

public sealed class CollaborationAttachmentStore(
    IBlobContainer<CollaborationAttachmentContainer> container,
    CollaborationDbContext db,
    ICurrentUser currentUser,
    IGuidGenerator guidGenerator,
    IConfiguration configuration) : ITransientDependency
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "application/pdf", "text/plain",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "video/mp4", "audio/mpeg"
    };

    public async Task<UploadAttachmentResult> UploadAsync(Guid conversationId, string fileName,
        string contentType, Stream content, long size, CancellationToken ct = default)
    {
        var userId = currentUser.Id ?? throw new AbpAuthorizationException();
        if (!await db.ConversationMembers.AnyAsync(x => x.ConversationId == conversationId && x.UserId == userId, ct))
            throw new AbpAuthorizationException();
        var maxBytes = configuration.GetValue<long?>("AttachmentPolicy:MaxBytes") ?? 25 * 1024 * 1024;
        if (size <= 0 || size > maxBytes) throw new BusinessException("Collaboration:InvalidAttachmentSize");
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Length > 128)
            throw new BusinessException("Collaboration:InvalidAttachmentType");
        if (!AllowedTypes.Contains(contentType)) throw new BusinessException("Collaboration:InvalidAttachmentType");
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 256) throw new BusinessException("Collaboration:InvalidFileName");
        var id = guidGenerator.Create();
        var blobName = $"conversations/{conversationId:N}/{id:N}";
        await using var buffer = await AttachmentContent.BufferAsync(content, size, ct);
        if (buffer.Length != size)
            throw new BusinessException("Collaboration:InvalidAttachmentSize");
        await container.SaveAsync(blobName, buffer, overrideExisting: false, cancellationToken: ct);
        var kind = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? AttachmentKind.Image
            : contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? AttachmentKind.Video
            : contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ? AttachmentKind.Audio : AttachmentKind.File;
        var attachment = new MessageAttachment(id, conversationId, userId, blobName, safeName, contentType, size, kind);
        db.Attachments.Add(attachment);
        try { await db.SaveChangesAsync(ct); }
        catch { await container.DeleteAsync(blobName, ct); throw; }
        return new(id, safeName, contentType, size, kind);
    }

    public async Task<AuthorizedDownload> DownloadAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var userId = currentUser.Id ?? throw new AbpAuthorizationException();
        var attachment = await db.Attachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachmentId, ct)
            ?? throw new BusinessException("Collaboration:AttachmentNotFound");
        if (!await db.ConversationMembers.AnyAsync(x => x.ConversationId == attachment.ConversationId && x.UserId == userId, ct))
            throw new AbpAuthorizationException();
        return new AuthorizedDownload(attachment.FileName, attachment.ContentType, await container.GetAsync(attachment.BlobName, ct));
    }

    public async Task DeleteAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var userId = currentUser.Id ?? throw new AbpAuthorizationException();
        var attachment = await db.Attachments.SingleOrDefaultAsync(x => x.Id == attachmentId, ct)
            ?? throw new BusinessException("Collaboration:AttachmentNotFound");
        if (attachment.UploadedByUserId != userId || attachment.MessageId.HasValue) throw new AbpAuthorizationException();
        db.Attachments.Remove(attachment); await db.SaveChangesAsync(ct); await container.DeleteAsync(attachment.BlobName, ct);
    }
}
