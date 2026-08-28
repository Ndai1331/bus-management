using PdfSharp.Fonts;
using HCS.DocumentService.Documents;

namespace HCS.DocumentService.Tests;

public sealed class PdfSharpFontResolverTests
{
    [Fact]
    public void Embedded_resolver_resolves_regular_and_bold_faces_without_platform_fonts()
    {
        var resolver = new EmbeddedSegoeFontResolver();

        var regular = resolver.ResolveTypeface("Arial", isBold: false, isItalic: false);
        var bold = resolver.ResolveTypeface("Arial", isBold: true, isItalic: false);

        Assert.NotNull(regular);
        Assert.NotNull(bold);
        Assert.NotEmpty(resolver.GetFont(regular!.FaceName)!);
        Assert.NotEmpty(resolver.GetFont(bold!.FaceName)!);
    }
}
