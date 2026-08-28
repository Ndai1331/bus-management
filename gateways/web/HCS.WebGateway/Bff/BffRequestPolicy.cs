namespace HCS.WebGateway;

internal static class BffRequestPolicy
{
    private static readonly string[] AnonymousBootstrapPaths =
    [
        "/api/abp/application-configuration",
        "/api/abp/application-localization"
    ];
    private const string AnonymousSurveyPrefix = "/api/surveys/public";
    private static readonly string[] ProtectedPrefixes = ["/api", "/hubs"];
    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

    internal static bool IsProxyPath(PathString path) =>
        ProtectedPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    internal static bool IsProtectedResourcePath(PathString path) =>
        IsProxyPath(path) || path.StartsWithSegments("/bff", StringComparison.OrdinalIgnoreCase);

    internal static bool IsAnonymousBootstrapPath(PathString path) =>
        AnonymousBootstrapPaths.Any(bootstrap => path.StartsWithSegments(bootstrap, StringComparison.OrdinalIgnoreCase));

    internal static bool IsAnonymousSurveyPath(PathString path) =>
        path.StartsWithSegments(AnonymousSurveyPrefix, StringComparison.OrdinalIgnoreCase);

    internal static bool RequiresAntiforgery(HttpRequest request) =>
        IsProxyPath(request.Path) && !IsAnonymousSurveyPath(request.Path) &&
        !SafeMethods.Contains(request.Method, StringComparer.OrdinalIgnoreCase);
}
