using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace HCS.DocumentService.Signing;

public interface ISigningSecretProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedValue);
}

public sealed class DataProtectionSigningSecretProtector(IDataProtectionProvider provider) : ISigningSecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("HCS.DocumentService.SigningCredential.v1");
    public string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText)) throw new ArgumentException("Signing secret is required.");
        return _protector.Protect(plainText);
    }
    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}

public sealed record SigningAdapterRequest(byte[] Content, string InputSha256, string Endpoint, string Secret,
    SigningProviderRequest? ProviderRequest = null);
public sealed record SigningAdapterResult(byte[] SignedContent, string AdapterId);

public interface IDigitalSigningAdapter
{
    SigningKind Kind { get; }
    Task<SigningAdapterResult> SignAsync(SigningAdapterRequest request, CancellationToken cancellationToken);
}

public sealed class ElectronicSigningAdapter : IDigitalSigningAdapter
{
    public SigningKind Kind => SigningKind.Electronic;
    public Task<SigningAdapterResult> SignAsync(SigningAdapterRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Electronic signing is disabled until a cryptographic signature adapter and verification path are configured.");
}

public sealed class UnavailableExternalSigningAdapter(SigningKind kind) : IDigitalSigningAdapter
{
    public SigningKind Kind { get; } = kind;
    public Task<SigningAdapterResult> SignAsync(SigningAdapterRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"Signing adapter '{Kind}' is a release blocker until an approved implementation and redistribution evidence are supplied.");
}

internal static class ContentHash
{
    public static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
