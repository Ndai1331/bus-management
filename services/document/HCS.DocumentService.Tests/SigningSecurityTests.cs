using System.Text.Json;
using HCS.DocumentService.Signing;
using Microsoft.AspNetCore.DataProtection;

namespace HCS.DocumentService.Tests;

public sealed class SigningSecurityTests
{
    [Fact]
    public void User_signature_rejects_empty_user_and_can_become_default()
    {
        Assert.Throws<ArgumentException>(() => new UserSignature(Guid.NewGuid(), Guid.Empty, "sign.png", "image/png", "signatures/a", 10, DateTime.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UserSignature(Guid.NewGuid(), Guid.NewGuid(), "sign.png", "image/png", "signatures/a", 0, DateTime.UtcNow));
        var signature = new UserSignature(Guid.NewGuid(), Guid.NewGuid(), "sign.png", "image/png", "signatures/a", 12, DateTime.UtcNow);
        Assert.Equal(UserSignatureType.Electronic, signature.Type);
        Assert.False(signature.IsDefault);
        signature.MarkDefault();
        Assert.True(signature.IsDefault);
        signature.ClearDefault();
        Assert.False(signature.IsDefault);
        signature.Rename("renamed-signature.png");
        Assert.Equal("renamed-signature.png", signature.FileName);
        signature.ReplaceContent("updated-signature.webp", "image/webp", "signatures/b", 24);
        Assert.Equal("updated-signature.webp", signature.FileName);
        Assert.Equal("image/webp", signature.ContentType);
        Assert.Equal("signatures/b", signature.BlobName);
        Assert.Equal(24, signature.Size);
        signature.ChangeType(UserSignatureType.Digital);
        Assert.Equal(UserSignatureType.Digital, signature.Type);
        Assert.Throws<ArgumentOutOfRangeException>(() => signature.ChangeType((UserSignatureType)99));
    }

    [Fact]
    public void Credential_request_secret_is_write_only_and_response_is_masked()
    {
        var request = JsonSerializer.Deserialize<ConfigureSigningCredentialRequest>("""{"kind":0,"endpoint":"https://ca.local","secret":"top-secret"}""")!;
        Assert.Equal("top-secret", request.ConsumeSecret());
        Assert.DoesNotContain("top-secret", JsonSerializer.Serialize(request));
        var response = new SigningCredentialDto(Guid.NewGuid(), SigningKind.RemoteCa, "https://ca.local", "********", DateTime.UtcNow);
        Assert.Contains("********", JsonSerializer.Serialize(response));
    }

    [Fact]
    public void Signing_secret_is_encrypted_at_rest()
    {
        var protector = new DataProtectionSigningSecretProtector(new EphemeralDataProtectionProvider());
        var encrypted = protector.Protect("top-secret");
        Assert.DoesNotContain("top-secret", encrypted);
        Assert.Equal("top-secret", protector.Unprotect(encrypted));
    }

    [Fact]
    public async Task Electronic_adapter_fails_closed_instead_of_emitting_a_non_cryptographic_marker()
    {
        var adapter = new ElectronicSigningAdapter();
        await Assert.ThrowsAsync<NotSupportedException>(() => adapter.SignAsync(
            new SigningAdapterRequest([], new string('a', 64), "https://electronic.local", ""), default));
    }

    [Fact]
    public async Task Word_prepared_electronic_content_is_not_overlayed_again_in_pdf()
    {
        var content = new byte[] { 1, 2, 3 };
        var providerRequest = new SigningProviderRequest(content, "https://electronic.local", "", "",
            [], [], [], "<<Sign02>>", "Nguyễn Văn A", "", 150, 70, 30, WordPrepared: true);

        var result = await new LicensedElectronicSigningAdapter().SignAsync(
            new SigningAdapterRequest(content, new string('a', 64), providerRequest.Endpoint, "", providerRequest), default);

        Assert.Same(content, result.SignedContent);
        Assert.Equal("electronic-docx-v1", result.AdapterId);
    }

    [Theory]
    [InlineData(SigningKind.RemoteCa)]
    [InlineData(SigningKind.Hsm)]
    public async Task External_adapters_fail_closed_until_approved(SigningKind kind)
    {
        var adapter = new UnavailableExternalSigningAdapter(kind);
        await Assert.ThrowsAsync<NotSupportedException>(() => adapter.SignAsync(new SigningAdapterRequest([], new string('a', 64), "https://ca.local", "secret"), default));
    }

    [Fact]
    public void Signing_failures_are_sanitized_before_persistence_and_api_output()
    {
        var message = SigningFailureSanitizer.ToPublicMessage(
            new InvalidOperationException("https://internal-ca.local secret=do-not-leak"));
        Assert.DoesNotContain("internal-ca", message);
        Assert.DoesNotContain("secret", message);
    }
}
