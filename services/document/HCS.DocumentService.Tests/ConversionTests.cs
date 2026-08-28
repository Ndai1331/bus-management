using HCS.DocumentService.Conversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCS.DocumentService.Tests;

public sealed class ConversionTests
{
    [Fact]
    public async Task Converter_skips_when_soffice_is_missing()
    {
        var config = new ConfigurationManager();
        config["LibreOffice:SofficePath"] = "/tmp/hcs-missing-soffice";
        var converter = new LibreOfficeDocxToPdfConverter(config, NullLogger<LibreOfficeDocxToPdfConverter>.Instance);
        Assert.False(converter.IsAvailable);
        Assert.Null(await converter.ConvertAsync(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public async Task Fake_converter_returns_pdf_bytes()
    {
        var converter = new FakeDocxToPdfConverter(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 fake"));
        Assert.True(converter.IsAvailable);
        var pdf = await converter.ConvertAsync([0x50, 0x4B]);
        Assert.NotNull(pdf);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf!));
    }

    private sealed class FakeDocxToPdfConverter(byte[] pdf) : IDocxToPdfConverter
    {
        public bool IsAvailable => true;
        public Task<byte[]?> ConvertAsync(byte[] docxBytes, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]?>(pdf);
    }
}
