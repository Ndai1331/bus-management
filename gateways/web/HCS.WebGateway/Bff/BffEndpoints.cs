using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace HCS.WebGateway;

internal static class BffEndpoints
{
    private const string DefaultReturnUrl = "https://localhost:44403/";

    internal static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/bff/login", (HttpContext context, IConfiguration configuration, string? returnUrl) =>
        {
            var safeReturnUrl = GetSafeReturnUrl(configuration, returnUrl);
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = safeReturnUrl },
                [HCSWebGatewayModule.OidcScheme]);
        }).AllowAnonymous();

        endpoints.MapGet("/bff/logout", (HttpContext context, IConfiguration configuration, string? returnUrl) =>
            FederatedLogout(context, configuration, returnUrl)).AllowAnonymous();

        endpoints.MapPost("/bff/logout", async (HttpContext context, IConfiguration configuration, IAntiforgery antiforgery, string? returnUrl) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.Headers["X-HCS-Antiforgery"] = "invalid";
                return Results.BadRequest(new { error = "invalid_antiforgery_token" });
            }

            return FederatedLogout(context, configuration, returnUrl);
        }).RequireAuthorization();

        endpoints.MapGet("/bff/user", (HttpContext context, ClaimsPrincipal user) =>
        {
            return Results.Ok(new
            {
                isAuthenticated = user.Identity?.IsAuthenticated == true,
                name = user.Identity?.Name,
                claims = user.Claims
                    .Where(claim => IsPublicClaim(claim.Type))
                    .Select(claim => new { claim.Type, claim.Value })
            });
        }).RequireAuthorization();

        endpoints.MapGet("/bff/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken, headerName = tokens.HeaderName });
        }).RequireAuthorization();

        return endpoints;
    }

    private static IResult FederatedLogout(HttpContext context, IConfiguration configuration, string? returnUrl)
    {
        var postLogoutUrl = GetSafePostLogoutUrl(configuration, returnUrl);
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Results.Redirect(postLogoutUrl);
        }

        // Top-level navigation is required: OIDC SignOut redirects to AuthServer
        // end_session so the Identity cookie is cleared. Cookie-only sign-out
        // leaves that session alive and /bff/login silently signs the user back in.
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = postLogoutUrl },
            [HCSWebGatewayModule.CookieScheme, HCSWebGatewayModule.OidcScheme]);
    }

    internal static string GetSafePostLogoutUrl(IConfiguration configuration, string? returnUrl)
    {
        var loginFallback = GetSafeReturnUrl(configuration, null).TrimEnd('/') + "/login";
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            !Uri.TryCreate(returnUrl, UriKind.Absolute, out var candidate))
        {
            return loginFallback;
        }

        var allowed = GetSafeReturnUrl(configuration, returnUrl);
        return string.Equals(allowed, candidate.ToString(), StringComparison.Ordinal)
            ? allowed
            : loginFallback;
    }

    internal static string GetSafeReturnUrl(IConfiguration configuration, string? returnUrl)
    {
        var allowedOrigins = HCSWebGatewayModule.GetCorsOrigins(configuration);
        var fallback = allowedOrigins.FirstOrDefault() ?? DefaultReturnUrl;
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var candidate))
        {
            return fallback;
        }

        var candidateOrigin = candidate.GetLeftPart(UriPartial.Authority);
        return allowedOrigins.Contains(candidateOrigin, StringComparer.OrdinalIgnoreCase)
            ? candidate.ToString()
            : fallback;
    }

    private static bool IsPublicClaim(string type) => type is
        "sub" or "name" or "preferred_username" or "email" or "role" or "permission" or ClaimTypes.Name or ClaimTypes.Role;
}
