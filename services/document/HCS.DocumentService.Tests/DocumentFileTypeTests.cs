using HCS.DocumentService.Documents;

namespace HCS.DocumentService.Tests;

public sealed class DocumentFileTypeTests
{
    [Theory]
    [InlineData("memo.docx", "application/octet-stream", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("memo.docx", "", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("scan.pdf", "application/pdf", "application/pdf")]
    [InlineData("photo.PNG", "image/png", "image/png")]
    [InlineData("photo.jpg", "application/octet-stream", "image/jpeg")]
    public void Normalizes_allowed_types_from_extension_when_browser_mime_is_generic(
        string fileName, string contentType, string expected)
    {
        Assert.True(DocumentFileService.TryNormalizeContentType(fileName, contentType, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Rejects_unknown_extensions()
    {
        Assert.False(DocumentFileService.TryNormalizeContentType("notes.exe", "application/octet-stream", out _));
        Assert.False(DocumentFileService.TryNormalizeContentType("notes.exe", "application/pdf", out _));
    }

    [Fact]
    public void Pdf_extension_wins_over_generic_browser_mime()
    {
        Assert.True(DocumentFileService.TryNormalizeContentType("scan.pdf", "application/octet-stream", out var normalized));
        Assert.Equal("application/pdf", normalized);
    }
}
