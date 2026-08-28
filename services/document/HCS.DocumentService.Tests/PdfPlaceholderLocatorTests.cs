using HCS.DocumentService.Signing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace HCS.DocumentService.Tests;

public sealed class PdfPlaceholderLocatorTests
{
    [Fact]
    public void Character_offsets_are_mapped_back_to_the_originating_text_run()
    {
        var runs = new List<PdfPlaceholderLocator.TextRun>
        {
            new("HEADER", 10, 700, 70, 712),
            new("A", 10, 680, 16, 692),
            new("A", 18, 680, 24, 692),
            new("A", 26, 680, 32, 692),
            new("A", 34, 680, 40, 692),
            new("A", 42, 680, 48, 692),
            new("<<Sign01>>", 100, 100, 180, 112),
            new("B", 500, 700, 506, 712),
            new("B", 508, 700, 514, 712),
            new("B", 516, 700, 522, 712),
            new("B", 524, 700, 530, 712),
            new("B", 532, 700, 538, 712),
            new("B", 540, 700, 546, 712),
            new("B", 548, 700, 554, 712),
            new("B", 556, 700, 562, 712),
            new("B", 564, 700, 570, 712),
            new("B", 572, 700, 578, 712),
            new("B", 580, 700, 586, 712),
            new("B", 588, 700, 594, 712)
        };

        var hit = PdfPlaceholderLocator.FindInRuns(1, 612, 792, runs, "<<Sign01>>");

        Assert.NotNull(hit);
        Assert.Equal(100, hit.X);
        Assert.Equal(100, hit.Y);
        Assert.Equal(80, hit.Width);
        Assert.Equal(12, hit.Height);
    }

    [Fact]
    public void A_placeholder_that_spans_most_of_the_page_is_rejected()
    {
        var runs = new List<PdfPlaceholderLocator.TextRun>
        {
            new("<<Sign01>>", 1, 1, 600, 400)
        };

        Assert.Null(PdfPlaceholderLocator.FindInRuns(1, 612, 792, runs, "<<Sign01>>"));
    }

    [Fact]
    public void Electronic_signature_uses_the_embedded_da_ky_layout_when_no_custom_layout_is_set()
    {
        using var image = new Image<Rgba32>(126, 71, Color.Transparent);
        using var source = new MemoryStream();
        image.Save(source, new PngEncoder());

        var composed = ElectronicSignatureLayoutComposer.Compose(source.ToArray());

        using var result = Image.Load<Rgba32>(composed);
        Assert.Equal(504, result.Width);
        Assert.True(result.Height > 0);
    }
}
