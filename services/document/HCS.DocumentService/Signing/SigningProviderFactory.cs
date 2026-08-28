using Microsoft.Extensions.Configuration;

namespace HCS.DocumentService.Signing;

public static class SigningProviderCodes
{
    public const string Visnam = "VISNAM";
    public const string Tag = "TAG";

    public static string Normalize(string? providerCode)
    {
        var value = providerCode?.Trim().ToUpperInvariant() ?? string.Empty;
        return value switch
        {
            "VINHSM" or "VIN-HSM" or "VIN_HSM" => Visnam,
            "REMOTE_CA" or "REMOTE-CA" or "REMOTECA" => Tag,
            _ => value
        };
    }
}

public sealed record SigningProviderDefaults(
    string Code,
    string DisplayName,
    IReadOnlySet<SigningKind> SupportedKinds,
    string? DefaultEndpoint,
    bool RequiresLayoutImage,
    bool RequiresSealImage,
    bool RequiresBase64Secret,
    int DefaultApiTimeoutSeconds = 30,
    int DefaultSignWidth = 150,
    int DefaultSignHeight = 70);

public interface ISigningProviderFactory
{
    IReadOnlyList<SigningProviderDefaults> Definitions { get; }
    SigningProviderDefaults GetDefinition(SigningKind kind, string? providerCode = null);
    IDigitalSigningAdapter GetAdapter(SigningKind kind, string? providerCode = null);
}

public sealed class SigningProviderFactory(
    IEnumerable<IDigitalSigningAdapter> adapters,
    IConfiguration configuration) : ISigningProviderFactory
{
    private readonly IReadOnlyDictionary<SigningKind, IDigitalSigningAdapter> adapterByKind =
        adapters.GroupBy(adapter => adapter.Kind).ToDictionary(group => group.Key, group =>
        {
            if (group.Count() != 1)
                throw new InvalidOperationException($"Multiple signing adapters are registered for {group.Key}.");
            return group.Single();
        });

    public IReadOnlyList<SigningProviderDefaults> Definitions { get; } = CreateDefinitions(configuration);

    public SigningProviderDefaults GetDefinition(SigningKind kind, string? providerCode = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));

        var normalizedCode = SigningProviderCodes.Normalize(providerCode);
        if (kind == SigningKind.Electronic)
        {
            return new SigningProviderDefaults(
                normalizedCode,
                "Electronic signature",
                new HashSet<SigningKind> { SigningKind.Electronic },
                "https://electronic.local",
                false,
                false,
                false);
        }

        if (string.IsNullOrWhiteSpace(normalizedCode))
            normalizedCode = GetDefaultCode(kind);

        var known = Definitions.FirstOrDefault(definition =>
            string.Equals(definition.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
        if (known is not null)
        {
            if (!known.SupportedKinds.Contains(kind))
                throw new ArgumentException($"Provider '{known.Code}' cannot be used with signing kind '{kind}'.", nameof(providerCode));
            return known;
        }

        // Preserve compatibility for a deployment-specific provider code. Its
        // adapter is still selected by signing kind, while named presets get
        // stricter validation and defaults above.
        return new SigningProviderDefaults(
            normalizedCode,
            normalizedCode,
            new HashSet<SigningKind> { kind },
            null,
            kind is SigningKind.Hsm or SigningKind.UsbToken,
            kind is SigningKind.Hsm or SigningKind.UsbToken,
            kind == SigningKind.RemoteCa);
    }

    public IDigitalSigningAdapter GetAdapter(SigningKind kind, string? providerCode = null)
    {
        _ = GetDefinition(kind, providerCode);
        return adapterByKind.TryGetValue(kind, out var adapter)
            ? adapter
            : throw new NotSupportedException($"No adapter registered for {kind}.");
    }

    private string GetDefaultCode(SigningKind kind) => kind switch
    {
        SigningKind.RemoteCa => SigningProviderCodes.Tag,
        SigningKind.Hsm or SigningKind.UsbToken => SigningProviderCodes.Visnam,
        _ => string.Empty
    };

    private static IReadOnlyList<SigningProviderDefaults> CreateDefinitions(IConfiguration configuration)
    {
        var visnamEndpoint = configuration["Signing:Providers:VISNAM:DefaultEndpoint"];
        if (string.IsNullOrWhiteSpace(visnamEndpoint))
            visnamEndpoint = "https://sign-hn10.vin-hsm.com";

        var tagEndpoint = configuration["Signing:Providers:TAG:DefaultEndpoint"];
        return
        [
            new SigningProviderDefaults(
                SigningProviderCodes.Visnam,
                "VISNAM / Vin-HSM",
                new HashSet<SigningKind> { SigningKind.Hsm, SigningKind.UsbToken },
                visnamEndpoint,
                RequiresLayoutImage: true,
                RequiresSealImage: true,
                RequiresBase64Secret: false),
            new SigningProviderDefaults(
                SigningProviderCodes.Tag,
                "TAG Remote CA",
                new HashSet<SigningKind> { SigningKind.RemoteCa },
                tagEndpoint,
                RequiresLayoutImage: false,
                RequiresSealImage: false,
                RequiresBase64Secret: true)
        ];
    }
}
