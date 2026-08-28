using System.Reflection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HCS.DocumentService.Signing;

/// <summary>
/// Adds the standard "Đã ký" badge to an electronic signature image.
/// The badge is embedded so the signing result does not depend on a web asset URL.
/// </summary>
internal static class ElectronicSignatureLayoutComposer
{
    private const string LayoutResourceSuffix = "electronic-signature-layout.png";
    private const double LayoutBadgeWidthRatio = 0.28;
    private const int BadgeMarginPx = 6;
    private const int ExportMinWidthPx = 504;
    private static byte[]? cachedLayoutBytes;

    internal static byte[] Compose(byte[] signatureImageBytes)
    {
        if (signatureImageBytes is not { Length: > 0 }) return signatureImageBytes;

        try
        {
            using var layoutBadge = Image.Load<Rgba32>(GetLayoutBytes());
            using var signature = Image.Load<Rgba32>(signatureImageBytes);
            CropSignatureBorder(signature);

            var badgeWidth = Math.Clamp(
                (int)Math.Round(signature.Width * LayoutBadgeWidthRatio),
                48,
                Math.Max(48, signature.Width - BadgeMarginPx * 2));
            var badgeHeight = Math.Max(1,
                (int)Math.Round(layoutBadge.Height * (badgeWidth / (double)layoutBadge.Width)));
            layoutBadge.Mutate(ctx => ctx.Resize(badgeWidth, badgeHeight));

            var posX = Math.Max(0, signature.Width - layoutBadge.Width - BadgeMarginPx);
            var posY = Math.Max(0, signature.Height - layoutBadge.Height - BadgeMarginPx);
            signature.Mutate(ctx => ctx.DrawImage(layoutBadge, new Point(posX, posY), 1f));

            if (signature.Width < ExportMinWidthPx)
            {
                var exportHeight = Math.Max(1,
                    (int)Math.Round(signature.Height * ((double)ExportMinWidthPx / signature.Width)));
                signature.Mutate(ctx => ctx.Resize(ExportMinWidthPx, exportHeight));
            }

            using var output = new MemoryStream();
            signature.Save(output, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
            return output.ToArray();
        }
        catch
        {
            return signatureImageBytes;
        }
    }

    private static void CropSignatureBorder(Image<Rgba32> signature)
    {
        const int borderCropPx = 2;
        if (signature.Width <= borderCropPx * 2 || signature.Height <= borderCropPx * 2) return;
        signature.Mutate(ctx => ctx.Crop(new Rectangle(
            borderCropPx,
            borderCropPx,
            signature.Width - borderCropPx * 2,
            signature.Height - borderCropPx * 2)));
    }

    private static byte[] GetLayoutBytes()
    {
        if (cachedLayoutBytes is not null) return cachedLayoutBytes;
        var assembly = typeof(ElectronicSignatureLayoutComposer).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(LayoutResourceSuffix, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(resourceName))
            throw new FileNotFoundException($"Electronic signature layout resource '{LayoutResourceSuffix}' was not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Cannot open embedded resource '{resourceName}'.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        cachedLayoutBytes = memory.ToArray();
        return cachedLayoutBytes;
    }
}
