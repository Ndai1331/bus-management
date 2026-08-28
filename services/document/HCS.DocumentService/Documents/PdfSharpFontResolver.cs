using System.Threading;
using PdfSharp.Fonts;

namespace HCS.DocumentService.Documents;

internal static class PdfSharpFontResolverRegistration
{
    private static int isRegistered;

    public static void EnsureRegistered()
    {
        if (Volatile.Read(ref isRegistered) != 0) return;

        try
        {
            GlobalFontSettings.FallbackFontResolver = new EmbeddedSegoeFontResolver();
        }
        catch (InvalidOperationException)
        {
            // Another PDFsharp consumer may have registered a resolver first.
        }

        Interlocked.Exchange(ref isRegistered, 1);
    }
}

internal sealed class EmbeddedSegoeFontResolver : IFontResolver
{
    private const string RegularFace = "HcsSegoeWp";
    private const string BoldFace = "HcsSegoeWpBold";

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? BoldFace : RegularFace, mustSimulateBold: false, mustSimulateItalic: isItalic);

    public byte[]? GetFont(string faceName) => faceName switch
    {
        RegularFace => PdfSharp.WPFonts.FontDataHelper.SegoeWP,
        BoldFace => PdfSharp.WPFonts.FontDataHelper.SegoeWPBold,
        _ => null
    };
}
