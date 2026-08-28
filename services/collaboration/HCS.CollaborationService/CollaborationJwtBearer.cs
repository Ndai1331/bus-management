using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Volo.Abp.Security.Claims;

namespace HCS.CollaborationService;

/// <summary>
    /// Aligns JwtBearer with the public AuthServer issuer used on access tokens.
    /// OpenIddict keeps JWT names (`sub`, `role`, `preferred_username`, `given_name`).
    /// ABP ICurrentUser defaults to ClaimTypes.NameIdentifier until these are aligned.
/// </summary>
public static class CollaborationJwtBearer
{
    public const string JwtSubjectClaim = "sub";
    public const string JwtRoleClaim = "role";

    public static IReadOnlyList<string> ResolveIssuers(IConfiguration configuration)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        Add(values, configuration["AuthServer:Authority"]);
        foreach (var extra in configuration.GetSection("AuthServer:ValidIssuers").Get<string[]>() ?? [])
        {
            Add(values, extra);
        }

        return [.. values];
    }

    /// <summary>
    /// OpenIddict keeps JWT names (`sub`, `role`, `preferred_username`, `given_name`).
    /// ABP ICurrentUser defaults to ClaimTypes.NameIdentifier until these are aligned.
    /// </summary>
    public static void AlignAbpClaimTypes()
    {
        AbpClaimTypes.UserId = JwtSubjectClaim;
        AbpClaimTypes.Role = JwtRoleClaim;
        AbpClaimTypes.UserName = "preferred_username";
        AbpClaimTypes.Name = "given_name";
        AbpClaimTypes.SurName = "family_name";
        AbpClaimTypes.Email = "email";
    }

    public static void Configure(JwtBearerOptions options, IConfiguration configuration)
    {
        AlignAbpClaimTypes();
        options.Authority = configuration["AuthServer:Authority"];
        options.Audience = configuration["AuthServer:Audience"] ?? "HCS";
        options.RequireHttpsMetadata = configuration.GetValue("AuthServer:RequireHttpsMetadata", true);
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = JwtSubjectClaim;
        options.TokenValidationParameters.RoleClaimType = JwtRoleClaim;
        if (configuration.GetValue("AuthServer:AllowUntrustedBackchannelCertificate", false))
        {
            options.BackchannelHttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
        }

        var issuers = ResolveIssuers(configuration);
        if (issuers.Count > 0)
        {
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidIssuers = issuers;
            options.TokenValidationParameters.IssuerValidator = (issuer, _, _) =>
                issuers.Contains(issuer, StringComparer.Ordinal)
                    ? issuer
                    : throw new SecurityTokenInvalidIssuerException(
                        $"IDX10205: Issuer '{issuer}' is not in the configured AuthServer issuers.");
        }

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/chat"))
                {
                    ctx.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    }

    private static void Add(ISet<string> values, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim().TrimEnd('/');
        values.Add(trimmed);
        values.Add(trimmed + "/");
    }
}
