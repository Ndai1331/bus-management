namespace HCS.CollaborationService.Storage;

/// <summary>
/// MinIO PutObject needs a seekable stream with a known length. IFormFile and
/// gateway-proxied multipart streams are often forward-only, which surfaces as
/// "Error while copying content to a stream".
/// </summary>
public static class AttachmentContent
{
    public static async Task<MemoryStream> BufferAsync(Stream content, long size, CancellationToken ct = default)
    {
        var capacity = size is > 0 and <= int.MaxValue ? (int)size : 0;
        var buffer = new MemoryStream(capacity);
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return buffer;
    }
}
