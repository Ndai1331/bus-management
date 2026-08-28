using HCS.DocumentService.Signing;

namespace HC.RemoteSigns;

public static class PdfPigSignLocator
{
    public record Hit(int Page, double X, double Y, double Width, double Height);

    public static Hit? Find(byte[] pdfBytes, string marker)
    {
        var hit = PdfPlaceholderLocator.Find(pdfBytes, marker);
        return hit is null ? null : new Hit(hit.Page, hit.X, hit.Y, hit.Width, hit.Height);
    }
}
