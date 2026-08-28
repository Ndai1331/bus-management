using System.Security.Claims;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace HCS.DocumentService.Documents;

public sealed class DocumentPdfWatermarkService(
    DocumentFileService files,
    IHttpContextAccessor httpContext)
{
    public async Task<(DocumentFile File, byte[] Bytes)> OpenAsync(
        Guid documentId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var result = await files.OpenAuthorizedAsync(documentId, fileId, cancellationToken);
        await using var content = result.Content;
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        if (!string.Equals(result.File.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return (result.File, bytes);

        var user = CurrentUserLabel(httpContext.HttpContext?.User);
        var stamp = $"HCS · {user} · {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
        return (result.File, Stamp(bytes, stamp));
    }

    private static byte[] Stamp(byte[] source, string text)
    {
        using var input = new MemoryStream(source, writable: false);
        using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        var font = new XFont("Arial", 8, XFontStyleEx.Regular);
        var brush = new XSolidBrush(XColor.FromArgb(150, 90, 90, 90));

        foreach (var page in document.Pages)
        {
            using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            graphics.DrawString(text, font, brush,
                new XRect(18, page.Height.Point - 28, page.Width.Point - 36, 14),
                XStringFormats.BottomLeft);
        }

        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static string CurrentUserLabel(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst("name")?.Value
            ?? principal?.FindFirst("preferred_username")?.Value
            ?? principal?.Identity?.Name
            ?? principal?.FindFirst("sub")?.Value;
        return string.IsNullOrWhiteSpace(value) ? "authenticated-user" : value.Trim();
    }
}
