using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;

namespace HCS.Blazor.Client.Authentication;

internal sealed class BffAuthenticationStateProvider(IHttpClientFactory httpClientFactory) : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private Task<AuthenticationState>? currentState;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        currentState ??= LoadStateAsync();

    public Task<AuthenticationState> RefreshAsync()
    {
        currentState = LoadStateAsync();
        NotifyAuthenticationStateChanged(currentState);
        return currentState;
    }

    private async Task<AuthenticationState> LoadStateAsync()
    {
        try
        {
            using var client = httpClientFactory.CreateClient("HCS.Bff");
            using var response = await client.GetAsync("bff/user");
            if (!response.IsSuccessStatusCode)
            {
                return AnonymousState;
            }

            var profile = await response.Content.ReadFromJsonAsync<BffUserResponse>(JsonOptions);
            return profile?.IsAuthenticated == true
                ? CreateAuthenticatedState(profile)
                : AnonymousState;
        }
        catch (HttpRequestException)
        {
            return AnonymousState;
        }
        catch (JsonException)
        {
            return AnonymousState;
        }
        catch (OperationCanceledException)
        {
            return AnonymousState;
        }
    }

    private static AuthenticationState CreateAuthenticatedState(BffUserResponse profile)
    {
        var claims = new List<Claim>();
        foreach (var claim in profile.Claims ?? [])
        {
            if (string.IsNullOrWhiteSpace(claim.Type) || string.IsNullOrWhiteSpace(claim.Value))
            {
                continue;
            }

            claims.Add(new Claim(claim.Type, claim.Value));
            if (claim.Type == "role")
            {
                claims.Add(new Claim(ClaimTypes.Role, claim.Value));
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.Name) && !claims.Any(claim => claim.Type == ClaimTypes.Name))
        {
            claims.Add(new Claim(ClaimTypes.Name, profile.Name));
        }

        var identity = new ClaimsIdentity(claims, "HCS.Bff", ClaimTypes.Name, ClaimTypes.Role);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private sealed record BffUserResponse(bool IsAuthenticated, string? Name, BffClaim[]? Claims);

    private sealed record BffClaim(string? Type, string? Value);
}
