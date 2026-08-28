using HCS.DocumentService.Signing;
using Microsoft.Extensions.Configuration;

namespace HCS.DocumentService.Tests;

public sealed class SigningProviderFactoryTests
{
    [Fact]
    public void Named_provider_defaults_resolve_to_the_correct_adapter()
    {
        var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Signing:Providers:VISNAM:DefaultEndpoint"] = "https://visnam.test"
        });

        var visnam = factory.GetDefinition(SigningKind.Hsm);
        var tag = factory.GetDefinition(SigningKind.RemoteCa);

        Assert.Equal("VISNAM", visnam.Code);
        Assert.Equal("https://visnam.test", visnam.DefaultEndpoint);
        Assert.Contains(SigningKind.UsbToken, visnam.SupportedKinds);
        Assert.Same(factory.GetAdapter(SigningKind.Hsm), factory.GetAdapter(SigningKind.Hsm, "vin-hsm"));
        Assert.Equal("TAG", tag.Code);
        Assert.True(tag.RequiresBase64Secret);
        Assert.False(tag.RequiresSealImage);
        Assert.Null(tag.DefaultEndpoint);
        Assert.True(visnam.RequiresSealImage);
    }

    [Fact]
    public void Named_provider_cannot_be_used_with_a_different_signing_kind()
    {
        var factory = CreateFactory();

        Assert.Throws<ArgumentException>(() => factory.GetDefinition(SigningKind.RemoteCa, "VISNAM"));
        Assert.Throws<ArgumentException>(() => factory.GetDefinition(SigningKind.Hsm, "TAG"));
    }

    private static SigningProviderFactory CreateFactory(IReadOnlyDictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();
        return new SigningProviderFactory(
        [
            new StubAdapter(SigningKind.Electronic),
            new StubAdapter(SigningKind.RemoteCa),
            new StubAdapter(SigningKind.Hsm),
            new StubAdapter(SigningKind.UsbToken)
        ], configuration);
    }

    private sealed class StubAdapter(SigningKind kind) : IDigitalSigningAdapter
    {
        public SigningKind Kind { get; } = kind;

        public Task<SigningAdapterResult> SignAsync(SigningAdapterRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
