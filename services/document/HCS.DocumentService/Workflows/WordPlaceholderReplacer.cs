using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SixLabors.ImageSharp;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace HCS.DocumentService.Workflows;

/// <summary>
/// Replaces workflow merge fields in DOCX bytes while retaining the first run's
/// formatting. The helper is intentionally independent of the licensed project.
/// </summary>
internal static class WordPlaceholderReplacer
{
    private const long SignatureImageMaxWidthEmu = 1_512_000L;
    private const long SignatureImageFallbackHeightEmu = 621_000L;

    public static byte[] ReplacePrepared(
        byte[] docxBytes,
        byte[]? signatureImageBytes,
        string fullName,
        string? positionName,
        string? departmentName,
        string? signingContent,
        DateTime now)
    {
        var replacements = new (string Placeholder, string Value)[]
        {
            ("<<DD>>", now.ToString("dd")),
            ("<<Day>>", now.ToString("dd")),
            ("<<MM>>", now.ToString("MM")),
            ("<<Month>>", now.ToString("MM")),
            ("<<YYYY>>", now.ToString("yyyy")),
            ("<<Year>>", now.ToString("yyyy")),
            ("<<PreparedFullName>>", fullName),
            ("<<VitriVieclam>>", positionName ?? string.Empty),
            ("<<ViTriLamViec>>", positionName ?? string.Empty),
            ("<<Position>>", positionName ?? string.Empty),
            ("<<PositionName>>", positionName ?? string.Empty),
            ("<<Department>>", departmentName ?? string.Empty),
            ("<<PhongBan>>", departmentName ?? string.Empty),
            ("<<ContentToBeApproved>>", PlainText(signingContent)),
        };

        using var stream = new MemoryStream(docxBytes.Length);
        stream.Write(docxBytes, 0, docxBytes.Length);
        stream.Position = 0;
        using (var document = WordprocessingDocument.Open(stream, true))
        {
            var mainPart = document.MainDocumentPart
                ?? throw new InvalidDataException("The Word document has no main document part.");
            ReplaceTextInPart(mainPart.Document?.Body, replacements);
            foreach (var header in mainPart.HeaderParts)
                ReplaceTextInPart(header.Header, replacements);
            foreach (var footer in mainPart.FooterParts)
                ReplaceTextInPart(footer.Footer, replacements);
            if (signatureImageBytes is { Length: > 0 })
                ReplaceImagePlaceholder(mainPart, mainPart.Document?.Body, signatureImageBytes, "<<PreparedBySign>>");
        }

        return stream.ToArray();
    }

    public static byte[] ReplaceApprovalText(byte[] docxBytes, int stepOrder, string fullName, string? noteContent)
    {
        var suffix = stepOrder.ToString("D2");
        var replacements = new (string Placeholder, string Value)[]
        {
            ($"<<FullName{suffix}>>", fullName),
            ($"<<NoteContent{suffix}>>", PlainText(noteContent)),
            ("<<NoteContent>>", PlainText(noteContent)),
        };

        return ReplaceText(docxBytes, replacements);
    }

    public static byte[] ReplaceApproval(
        byte[] docxBytes,
        int stepOrder,
        byte[]? signatureImageBytes,
        string fullName,
        string? noteContent)
    {
        var replaced = ReplaceApprovalText(docxBytes, stepOrder, fullName, noteContent);
        if (signatureImageBytes is not { Length: > 0 }) return replaced;

        var suffix = stepOrder.ToString("D2");
        using var stream = new MemoryStream(replaced.Length);
        stream.Write(replaced, 0, replaced.Length);
        stream.Position = 0;
        using (var document = WordprocessingDocument.Open(stream, true))
        {
            var mainPart = document.MainDocumentPart
                ?? throw new InvalidDataException("The Word document has no main document part.");
            ReplaceImagePlaceholder(mainPart, mainPart.Document?.Body, signatureImageBytes, $"<<Sign{suffix}>>");
        }

        return stream.ToArray();
    }

    private static byte[] ReplaceText(byte[] docxBytes, (string Placeholder, string Value)[] replacements)
    {
        using var stream = new MemoryStream(docxBytes.Length);
        stream.Write(docxBytes, 0, docxBytes.Length);
        stream.Position = 0;
        using (var document = WordprocessingDocument.Open(stream, true))
        {
            var mainPart = document.MainDocumentPart
                ?? throw new InvalidDataException("The Word document has no main document part.");
            ReplaceTextInPart(mainPart.Document?.Body, replacements);
            foreach (var header in mainPart.HeaderParts)
                ReplaceTextInPart(header.Header, replacements);
            foreach (var footer in mainPart.FooterParts)
                ReplaceTextInPart(footer.Footer, replacements);
        }

        return stream.ToArray();
    }

    private static void ReplaceTextInPart(OpenXmlElement? root, (string Placeholder, string Value)[] replacements)
    {
        if (root is null) return;
        foreach (var paragraph in root.Descendants<Paragraph>())
        {
            var textNodes = paragraph.Descendants<Text>().ToList();
            if (textNodes.Count == 0) continue;
            var fullText = string.Concat(textNodes.Select(x => x.Text ?? string.Empty));
            if (fullText.Length == 0) continue;

            var modified = fullText;
            foreach (var (placeholder, value) in replacements)
                modified = modified.Replace(placeholder, value ?? string.Empty, StringComparison.Ordinal);
            if (string.Equals(modified, fullText, StringComparison.Ordinal)) continue;

            if (modified.Contains('\n'))
            {
                var properties = CloneRunProperties(textNodes[0]);
                foreach (var child in paragraph.ChildElements.Where(x => x is not ParagraphProperties).ToList())
                    child.Remove();
                var lines = modified.Split('\n');
                for (var index = 0; index < lines.Length; index++)
                {
                    var run = new Run();
                    if (properties is not null) run.Append((RunProperties)properties.CloneNode(true));
                    run.Append(new Text(lines[index]) { Space = SpaceProcessingModeValues.Preserve });
                    if (index < lines.Length - 1) run.Append(new Break());
                    paragraph.Append(run);
                }
            }
            else
            {
                textNodes[0].Text = modified;
                for (var index = 1; index < textNodes.Count; index++) textNodes[index].Text = string.Empty;
            }
        }
    }

    private static void ReplaceImagePlaceholder(
        MainDocumentPart mainPart,
        OpenXmlElement? root,
        byte[] imageBytes,
        string placeholder)
    {
        if (root is null) return;
        foreach (var paragraph in root.Descendants<Paragraph>())
        {
            var textNodes = paragraph.Descendants<Text>().ToList();
            if (textNodes.Count == 0) continue;
            var fullText = string.Concat(textNodes.Select(x => x.Text ?? string.Empty));
            var index = fullText.IndexOf(placeholder, StringComparison.Ordinal);
            if (index < 0) continue;

            var prefix = fullText[..index];
            var suffix = fullText[(index + placeholder.Length)..];
            var properties = textNodes.Select(CloneRunProperties).FirstOrDefault(x => x is not null);
            var contentType = DetectContentType(imageBytes);
            var imagePart = mainPart.AddImagePart(contentType);
            using (var imageStream = new MemoryStream(imageBytes)) imagePart.FeedData(imageStream);
            var relationshipId = mainPart.GetIdOfPart(imagePart);
            var (width, height) = ResolveImageExtent(imageBytes);

            foreach (var child in paragraph.ChildElements.Where(x => x is not ParagraphProperties).ToList())
                child.Remove();
            if (prefix.Length > 0) paragraph.Append(CreateRun(properties, prefix));
            paragraph.Append(new Run(CreateInlineImageDrawing(relationshipId, contentType, width, height)));
            if (suffix.Length > 0) paragraph.Append(CreateRun(properties, suffix));
            return;
        }
    }

    private static Run CreateRun(RunProperties? properties, string text)
    {
        var run = new Run();
        if (properties is not null) run.Append((RunProperties)properties.CloneNode(true));
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static RunProperties? CloneRunProperties(Text text) =>
        text.Parent is Run { RunProperties: not null } run
            ? (RunProperties)run.RunProperties!.CloneNode(true)
            : null;

    private static string PlainText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var text = value.Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "</p>\\s*<p[^>]*>", "\n",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", " ");
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }

    private static string DetectContentType(byte[] bytes) => bytes switch
    {
        { Length: >= 3 } when bytes[0] == 0xFF && bytes[1] == 0xD8 => "image/jpeg",
        { Length: >= 8 } when bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E => "image/png",
        { Length: >= 3 } when bytes[0] == 0x47 && bytes[1] == 0x49 => "image/gif",
        _ => "image/jpeg"
    };

    private static (long Width, long Height) ResolveImageExtent(byte[] bytes)
    {
        try
        {
            var info = Image.Identify(bytes);
            if (info is { Width: > 0, Height: > 0 })
            {
                var height = (long)Math.Round(SignatureImageMaxWidthEmu / ((double)info.Width / info.Height));
                return (SignatureImageMaxWidthEmu, Math.Max(1, height));
            }
        }
        catch
        {
            // Keep the stable fallback size for malformed/unsupported image metadata.
        }

        return (SignatureImageMaxWidthEmu, SignatureImageFallbackHeightEmu);
    }

    private static Drawing CreateInlineImageDrawing(string relationshipId, string contentType, long width, long height)
    {
        var extension = contentType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
        return new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = width, Cy = height },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = 1U, Name = "Signature" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = "signature" + extension },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId, CompressionState = A.BlipCompressionValues.Print },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = width, Cy = height }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            });
    }
}
