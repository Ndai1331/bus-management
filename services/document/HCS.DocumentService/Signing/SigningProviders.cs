using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using HC.RemoteSigns;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HCS.DocumentService.Signing;

/// <summary>
/// Provider-specific signing payload. The adapter boundary deliberately contains
/// only the bytes and already-authorized user configuration; adapters never query
/// the database or expose secrets in their result.
/// </summary>
public sealed record SigningProviderRequest(
    byte[] Content,
    string Endpoint,
    string Secret,
    string TokenRef,
    byte[] SignatureImage,
    byte[] SealImage,
    byte[] LayoutImage,
    string Placeholder,
    string SignerName,
    string Note,
    int Width,
    int Height,
    int TimeoutSeconds,
    bool WordPrepared = false);

public sealed class LicensedElectronicSigningAdapter : IDigitalSigningAdapter
{
    public SigningKind Kind => SigningKind.Electronic;

    public Task<SigningAdapterResult> SignAsync(SigningAdapterRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var providerRequest = request.ProviderRequest
            ?? throw new InvalidOperationException("Electronic signing configuration is missing.");
        if (providerRequest.WordPrepared)
        {
            // The signature image and approval text were already placed in DOCX
            // and converted to this PDF. A second PDF overlay would cover the
            // layout and is the cause of the SignXX format regression.
            return Task.FromResult(new SigningAdapterResult(providerRequest.Content, "electronic-docx-v1"));
        }
        return Task.FromResult(new SigningAdapterResult(
            PdfSigningDrawing.ApplyElectronic(providerRequest), "electronic-pdfsharp-v1"));
    }
}

public sealed class LicensedRemoteCaSigningAdapter : IDigitalSigningAdapter
{
    private readonly ILoggerFactory _loggerFactory;
    public LicensedRemoteCaSigningAdapter(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;
    public SigningKind Kind => SigningKind.RemoteCa;

    public async Task<SigningAdapterResult> SignAsync(SigningAdapterRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var providerRequest = request.ProviderRequest
            ?? throw new InvalidOperationException("Remote CA signing configuration is missing.");
        var hit = PdfSigningDrawing.FindPlaceholder(providerRequest.Content, providerRequest.Placeholder)
            ?? throw new HC.BnnSoftSigns.SignPlaceholderNotFoundException(providerRequest.Placeholder, -1);
        var timeout = TimeSpan.FromSeconds(Math.Clamp(providerRequest.TimeoutSeconds, 30, 240));
        var signer = new SignTextV2(providerRequest.TokenRef, providerRequest.Secret, providerRequest.Endpoint,
            timeout, _loggerFactory.CreateLogger<SignTextV2>());
        var imageBytes = PdfSigningDrawing.ComposeSignatureWithLayout(providerRequest.SignatureImage, providerRequest.LayoutImage);
        var image = Convert.ToBase64String(imageBytes);
        var signerText = string.IsNullOrWhiteSpace(providerRequest.Note)
            ? providerRequest.SignerName
            : $"{providerRequest.SignerName}\r\n{providerRequest.Note}";
        var result = await signer.SignatureAsync(new PdfSignRequest
        {
            base64pdf = Convert.ToBase64String(providerRequest.Content),
            hashalg = "SHA256",
            typesignature = 3,
            textout = signerText,
            base64image = image,
            base64SignImage = image,
            signaturename = providerRequest.Placeholder,
            xpoint = (int)(hit.X - 5),
            ypoint = (int)(hit.Y - 30),
            pagesign = hit.Page,
            TextLocationIdentifier = providerRequest.Placeholder,
            width = providerRequest.Width,
            height = providerRequest.Height,
            AppendDateSign = false,
            DateFormatString = "dd/MM/yyyy HH:mm:ss",
            FontSize = 9f
        }, cancellationToken);
        if (result is not { Length: > 0 }) throw new InvalidDataException("Remote CA returned an empty signed PDF.");
        return new SigningAdapterResult(PdfSigningDrawing.WhiteoutPlaceholder(result, providerRequest.Placeholder), "remote-ca-tag-v2");
    }
}

/// <summary>
/// HSM and USB-token use the same BnnSoftSigns PDF/hash/certificate flow as the
/// licensed application. USB is kept as a separate adapter kind so the UI and
/// audit trail preserve the selected provider type.
/// </summary>
public sealed class LicensedBnnSigningAdapter(SigningKind kind, ILoggerFactory loggerFactory) : IDigitalSigningAdapter
{
    public SigningKind Kind { get; } = kind;

    public Task<SigningAdapterResult> SignAsync(SigningAdapterRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var providerRequest = request.ProviderRequest
            ?? throw new InvalidOperationException("HSM signing configuration is missing.");
        if (string.IsNullOrWhiteSpace(providerRequest.TokenRef))
            throw new InvalidOperationException("A token reference is required for HSM or USB signing.");
        var signer = new HC.BnnSoftSigns.SignText(providerRequest.TokenRef, providerRequest.Secret,
            providerRequest.Endpoint, loggerFactory.CreateLogger<HC.BnnSoftSigns.SignText>());
        var signed = signer.SignTextLocationCustomizeV2(new HC.BnnSoftSigns.SignPdfInput
        {
            datapdf = providerRequest.Content,
            chukytuoi = providerRequest.SignatureImage,
            condau = providerRequest.SealImage,
            anhkhung = providerRequest.LayoutImage,
            signaturename = Guid.NewGuid().ToString("N"),
            nguoiky = providerRequest.SignerName,
            chucvu = string.Empty,
            fontsize = 9,
            fontcolor = "#002f7a",
            fontname = HC.PdfFontEnvironment.DefaultPdfSerifFontFamily,
            pagesign = -1,
            typesignature = 3,
            hashalg = "SHA-256",
            textsign = providerRequest.Placeholder,
            width = providerRequest.Width,
            height = providerRequest.Height,
            imgwidth = providerRequest.Width,
            imgheight = providerRequest.Height,
            borderstyle = 0,
            bordercolor = "#000000"
        }, xOffset: 10, yOffset: 42);
        if (signed is not { Length: > 0 }) throw new InvalidDataException("HSM provider returned an empty signed PDF.");
        return Task.FromResult(new SigningAdapterResult(PdfSigningDrawing.WhiteoutPlaceholder(signed, providerRequest.Placeholder), Kind == SigningKind.UsbToken
            ? "usb-token-bnn-v1" : "hsm-bnn-v1"));
    }
}

internal static class PdfSigningDrawing
{
    internal sealed record PlaceholderHit(int Page, double X, double Y, double Width, double Height);

    public static PlaceholderHit? FindPlaceholder(byte[] pdfBytes, string placeholder)
    {
        var hit = PdfPlaceholderLocator.Find(pdfBytes, placeholder);
        return hit is null ? null : new PlaceholderHit(hit.Page, hit.X, hit.Y, hit.Width, hit.Height);
    }

    public static byte[] ApplyElectronic(SigningProviderRequest request)
    {
        var hit = FindPlaceholder(request.Content, request.Placeholder)
            ?? throw new HC.BnnSoftSigns.SignPlaceholderNotFoundException(request.Placeholder, -1);
        if (request.SignatureImage is not { Length: > 0 })
            throw new InvalidOperationException("An electronic signature image is required.");

        using var input = new MemoryStream(request.Content);
        using var pdf = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        var page = pdf.Pages[hit.Page - 1];
        var pageHeight = page.Height.Point;
        var requestedWidth = Math.Max(hit.Width, request.Width);
        var requestedHeight = Math.Max(hit.Height, request.Height);
        var drawingRect = ClampRect(page, hit.X - 4, pageHeight - hit.Y - hit.Height - 4,
            requestedWidth, requestedHeight);
        var x = drawingRect.X;
        var y = drawingRect.Y;
        var width = drawingRect.Width;
        var height = drawingRect.Height;
        using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        graphics.DrawRectangle(XBrushes.White, new XRect(x, y, width, height));
        var composedSignature = request.LayoutImage is { Length: > 0 }
            ? ComposeSignatureWithLayout(request.SignatureImage, request.LayoutImage)
            : ElectronicSignatureLayoutComposer.Compose(request.SignatureImage);
        var signatureImage = NormalizeToPng(composedSignature);
        using var imageStream = new MemoryStream(signatureImage);
        using var image = XImage.FromStream(imageStream);
        var aspect = image.PixelHeight == 0 ? 1d : (double)image.PixelWidth / image.PixelHeight;
        var imageWidth = Math.Min(width, Math.Max(80d, width));
        var imageHeight = imageWidth / aspect;
        if (imageHeight > height)
        {
            imageHeight = height;
            imageWidth = imageHeight * aspect;
        }
        graphics.DrawImage(image, x, y + (height - imageHeight) / 2, imageWidth, imageHeight);
        if (!string.IsNullOrWhiteSpace(request.SignerName))
        {
            var font = new XFont(HC.PdfFontEnvironment.DefaultPdfSerifFontFamily, 8);
            graphics.DrawString(request.SignerName, font, XBrushes.Black,
                new XRect(x, y + height - 12, width, 12), XStringFormats.CenterLeft);
        }
        using var output = new MemoryStream();
        pdf.Save(output, false);
        return output.ToArray();
    }

    public static byte[] WhiteoutPlaceholder(byte[] pdfBytes, string placeholder)
    {
        var hit = FindPlaceholder(pdfBytes, placeholder);
        if (hit is null) return pdfBytes;
        using var input = new MemoryStream(pdfBytes);
        using var pdf = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        var page = pdf.Pages[hit.Page - 1];
        var drawingRect = ClampRect(page, hit.X - 3, page.Height.Point - hit.Y - hit.Height - 3,
            hit.Width + 6, hit.Height + 6);
        using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        graphics.DrawRectangle(XBrushes.White, drawingRect);
        using var output = new MemoryStream();
        pdf.Save(output, false);
        return output.ToArray();
    }

    public static byte[] ComposeSignatureWithLayout(byte[] signatureImage, byte[] layoutImage)
    {
        if (signatureImage is not { Length: > 0 } || layoutImage is not { Length: > 0 }) return signatureImage;
        try
        {
            using var layout = Image.Load<Rgba32>(layoutImage);
            using var signature = Image.Load<Rgba32>(signatureImage);
            if (signature.Width > 4 && signature.Height > 4)
                signature.Mutate(ctx => ctx.Crop(new Rectangle(2, 2, signature.Width - 4, signature.Height - 4)));
            var zoneWidth = Math.Max(1, (int)(layout.Width * 0.58) - 8);
            var zoneHeight = Math.Max(1, layout.Height - 8);
            signature.Mutate(ctx => ctx.Resize(new ResizeOptions { Size = new Size(zoneWidth, zoneHeight), Mode = ResizeMode.Max }));
            var posX = 4 + Math.Max(0, (zoneWidth - signature.Width) / 2);
            var posY = 4 + Math.Max(0, (zoneHeight - signature.Height) / 2);
            using var composite = new Image<Rgba32>(layout.Width, layout.Height);
            composite.Mutate(ctx => ctx.DrawImage(signature, new Point(posX, posY), 1f));
            composite.Mutate(ctx => ctx.DrawImage(layout, Point.Empty, 1f));
            using var output = new MemoryStream();
            composite.Save(output, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
            return output.ToArray();
        }
        catch
        {
            return signatureImage;
        }
    }

    private static byte[] NormalizeToPng(byte[] imageBytes)
    {
        try
        {
            using var image = Image.Load<Rgba32>(imageBytes);
            using var output = new MemoryStream();
            image.Save(output, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
            return output.ToArray();
        }
        catch
        {
            return imageBytes;
        }
    }

    private static XRect ClampRect(PdfSharp.Pdf.PdfPage page, double x, double y, double width, double height)
    {
        var pageWidth = page.Width.Point;
        var pageHeight = page.Height.Point;
        var left = Math.Clamp(double.IsFinite(x) ? x : 0, 0, pageWidth);
        var bottom = Math.Clamp(double.IsFinite(y) ? y : 0, 0, pageHeight);
        var right = Math.Clamp(left + Math.Max(1, double.IsFinite(width) ? width : 1), left, pageWidth);
        var top = Math.Clamp(bottom + Math.Max(1, double.IsFinite(height) ? height : 1), bottom, pageHeight);
        return new XRect(left, bottom, Math.Max(1, right - left), Math.Max(1, top - bottom));
    }
}
