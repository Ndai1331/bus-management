namespace HCS.AuthServer;

public sealed class KeycloakOptions
{
    public const string SectionName = "Authentication:Keycloak";
    public const string Scheme = "Keycloak";
    public const string CallbackPath = "/signin-oidc";

    public bool Enabled { get; set; }

    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Optional OIDC discovery URL for backchannel metadata when <see cref="Authority"/>
    /// must stay browser-reachable (for example http://localhost:5110) while the host
    /// resolves metadata through Docker (host.docker.internal).
    /// </summary>
    public string? MetadataAddress { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; } = true;
}
