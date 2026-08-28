using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HCS.DocumentService.Documents;
using HCS.DocumentService.Signing;
using HCS.DocumentService.Workflows;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace HCS.DocumentService.Tests;

public sealed class WorkflowPlaceholderTests
{
    [Fact]
    public void Prepared_aliases_are_replaced_in_docx_runs()
    {
        var source = CreateDocx("<<PreparedFullName>>|<<VitriVieclam>>|<<Position>>|<<PhongBan>>|<<Department>>|<<ContentToBeApproved>>");

        var result = WordPlaceholderReplacer.ReplacePrepared(source, null,
            "Nguyễn Văn A", "Chuyên viên", "Phòng Nội vụ", "Nội dung trình ký", new DateTime(2026, 8, 26));

        var text = ReadBodyText(result);
        Assert.Contains("Nguyễn Văn A", text);
        Assert.Contains("Chuyên viên", text);
        Assert.Contains("Phòng Nội vụ", text);
        Assert.Contains("Nội dung trình ký", text);
        Assert.DoesNotContain("<<PreparedFullName>>", text);
        Assert.DoesNotContain("<<VitriVieclam>>", text);
        Assert.DoesNotContain("<<Department>>", text);
    }

    [Fact]
    public void Approval_name_and_note_use_the_numbered_step_aliases()
    {
        var source = CreateDocx("<<FullName02>>|<<NoteContent02>>|<<NoteContent>>");

        var result = WordPlaceholderReplacer.ReplaceApprovalText(source, 2,
            "Trần Thị B", "Đã kiểm tra hồ sơ");

        var text = ReadBodyText(result);
        Assert.Contains("Trần Thị B", text);
        Assert.Equal(2, text.Split("Đã kiểm tra hồ sơ", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("<<FullName02>>", text);
        Assert.DoesNotContain("<<NoteContent02>>", text);
        Assert.DoesNotContain("<<NoteContent>>", text);
    }

    [Fact]
    public void Word_first_electronic_signing_replaces_the_step_image_and_text()
    {
        var source = CreateDocx("<<Sign02>>|<<FullName02>>|<<NoteContent02>>");

        var result = WordFirstSigningDocumentBuilder.Replace(source, SigningKind.Electronic,
            CreatePng(640, 200), 2, "Trần Thị B", "Đã kiểm tra hồ sơ");

        var text = ReadBodyText(result);
        Assert.DoesNotContain("<<Sign02>>", text);
        Assert.DoesNotContain("<<FullName02>>", text);
        Assert.DoesNotContain("<<NoteContent02>>", text);
        Assert.Contains("Trần Thị B", text);
        Assert.Equal(1, ReadImagePartCount(result));
    }

    [Fact]
    public void Word_first_digital_signing_replaces_text_but_keeps_the_provider_placeholder()
    {
        var source = CreateDocx("<<Sign02>>|<<FullName02>>|<<NoteContent02>>");

        var result = WordFirstSigningDocumentBuilder.Replace(source, SigningKind.RemoteCa,
            CreatePng(640, 200), 2, "Trần Thị B", "Đã kiểm tra hồ sơ");

        var text = ReadBodyText(result);
        Assert.Contains("<<Sign02>>", text);
        Assert.Contains("Trần Thị B", text);
        Assert.Contains("Đã kiểm tra hồ sơ", text);
        Assert.DoesNotContain("<<FullName02>>", text);
        Assert.DoesNotContain("<<NoteContent02>>", text);
        Assert.Equal(0, ReadImagePartCount(result));
    }

    [Fact]
    public void Approval_name_and_note_are_overlaid_in_pdf()
    {
        var source = CreatePdf("<<Sign02>>|<<FullName02>>|<<NoteContent02>>|<<NoteContent>>");

        var result = PdfPlaceholderReplacer.ReplaceApprovalText(source, 2,
            "Trần Thị B", "Đã kiểm tra hồ sơ");

        Assert.NotEqual(source, result);
        Assert.NotNull(PdfPlaceholderReplacer.FindPlaceholder(result, "<<FullName02>>"));
        Assert.NotNull(PdfPlaceholderReplacer.FindPlaceholder(result, "<<NoteContent02>>"));
        Assert.NotNull(PdfPlaceholderReplacer.FindPlaceholder(result, "<<NoteContent>>"));
        Assert.NotNull(PdfPlaceholderReplacer.FindPlaceholder(result, "<<Sign02>>"));
    }

    private static byte[] CreateDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
            main.Document.Save();
        }
        return stream.ToArray();
    }

    private static byte[] CreatePdf(string text)
    {
        PdfSharpFontResolverRegistration.EnsureRegistered();
        using var stream = new MemoryStream();
        using (var document = new PdfDocument())
        {
            var page = document.AddPage();
            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawString(text, new XFont(HC.PdfFontEnvironment.DefaultPdfSerifFontFamily, 10),
                XBrushes.Black, new XRect(20, 40, 550, 40), XStringFormats.TopLeft);
            document.Save(stream, false);
        }
        return stream.ToArray();
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image[0, 0] = new Rgba32(0, 0, 0, 255);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    private static int ReadImagePartCount(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart?.ImageParts.Count() ?? 0;
    }

    private static string ReadBodyText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
    }
}
