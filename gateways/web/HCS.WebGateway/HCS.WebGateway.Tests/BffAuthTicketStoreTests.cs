using System.Security.Claims;
using HCS.Bff;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HCS.WebGateway.Tests;

public sealed class BffAuthTicketStoreTests
{
    [Fact]
    public async Task Store_retrieve_roundtrips_tokens_and_permission_claims()
    {
        var store = CreateStore();
        var ticket = CreateTicket("access-token-value", "refresh-token-value", "HCS.Organization.Departments");

        var key = await store.StoreAsync(ticket);
        var restored = await store.RetrieveAsync(key);

        Assert.False(string.IsNullOrWhiteSpace(key));
        Assert.True(key.Length < 64);
        Assert.NotNull(restored);
        Assert.Equal("admin", restored.Principal.Identity?.Name);
        Assert.Equal("access-token-value", restored.Properties.GetTokenValue("access_token"));
        Assert.Equal("refresh-token-value", restored.Properties.GetTokenValue("refresh_token"));
        Assert.Contains(restored.Principal.Claims, claim => claim.Type == "permission" && claim.Value == "HCS.Organization.Departments");
    }

    [Fact]
    public async Task Remove_drops_the_ticket()
    {
        var store = CreateStore();
        var key = await store.StoreAsync(CreateTicket("access", "refresh"));

        await store.RemoveAsync(key);

        Assert.Null(await store.RetrieveAsync(key));
    }

    [Fact]
    public async Task Renew_replaces_the_stored_access_token()
    {
        var store = CreateStore();
        var ticket = CreateTicket("old-access", "refresh");
        var key = await store.StoreAsync(ticket);
        ticket.Properties.UpdateTokenValue("access_token", "rotated-access");

        await store.RenewAsync(key, ticket);
        var restored = await store.RetrieveAsync(key);

        Assert.Equal("rotated-access", restored?.Properties.GetTokenValue("access_token"));
    }

    [Fact]
    public void Lifetime_uses_ticket_expiry_or_eight_hour_fallback()
    {
        var now = new DateTimeOffset(2026, 8, 17, 6, 0, 0, TimeSpan.Zero);
        var expiring = CreateTicket("access", "refresh");
        expiring.Properties.ExpiresUtc = now.AddMinutes(30);
        Assert.Equal(TimeSpan.FromMinutes(30), BffAuthTicketStore.GetLifetime(expiring, now));

        var expired = CreateTicket("access", "refresh");
        expired.Properties.ExpiresUtc = now.AddSeconds(-5);
        Assert.Equal(TimeSpan.FromSeconds(1), BffAuthTicketStore.GetLifetime(expired, now));

        var unbounded = CreateTicket("access", "refresh");
        unbounded.Properties.ExpiresUtc = null;
        Assert.Equal(TimeSpan.FromHours(8), BffAuthTicketStore.GetLifetime(unbounded, now));
    }

    [Theory]
    [InlineData(".HCS.BffC1", true)]
    [InlineData(".HCS.BffC12", true)]
    [InlineData(".HCS.Bff", false)]
    [InlineData(".HCS.Bff.Antiforgery", false)]
    [InlineData(".HCS.BffChunk", false)]
    public void Chunk_cookie_names_match_aspnet_chunking_suffix(string name, bool expected) =>
        Assert.Equal(expected, BffCookieChunkCleanup.IsChunkCookie(name));

    [Fact]
    public void Cleanup_expires_only_leftover_chunk_cookies()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = ".HCS.Bff=session; .HCS.BffC1=chunk; .HCS.Bff.Antiforgery=xsrf";
        var options = new CookieOptions { Path = "/", Secure = true, HttpOnly = true, SameSite = SameSiteMode.None };

        BffCookieChunkCleanup.ExpireLegacyChunks(context, options);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains(".HCS.BffC1=", setCookie, StringComparison.Ordinal);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".HCS.Bff=", setCookie, StringComparison.Ordinal);
        Assert.DoesNotContain(".HCS.Bff.Antiforgery=", setCookie, StringComparison.Ordinal);
    }

    private static BffAuthTicketStore CreateStore() =>
        new(new MemoryBffTicketCache(), new EphemeralDataProtectionProvider(), TimeProvider.System);

    private static AuthenticationTicket CreateTicket(string accessToken, string refreshToken, string? permission = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "admin"),
            new("sub", "user-1")
        };
        if (!string.IsNullOrWhiteSpace(permission))
        {
            claims.Add(new Claim("permission", permission));
        }

        var properties = new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) };
        properties.StoreTokens([
            new AuthenticationToken { Name = "access_token", Value = accessToken },
            new AuthenticationToken { Name = "refresh_token", Value = refreshToken }
        ]);
        return new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, HCSWebGatewayModule.CookieScheme)),
            properties,
            HCSWebGatewayModule.CookieScheme);
    }
}
