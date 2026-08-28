namespace HCS.WebGateway;

internal sealed class BffAccessTokenMiddleware(RequestDelegate next)
{
    internal const string AccessTokenItemKey = "HCS.Bff.AccessToken";
    internal const string TokenRefreshUnavailableItemKey = "HCS.Bff.TokenRefreshUnavailable";

    public async Task InvokeAsync(HttpContext context, BffTokenRefreshService tokenRefreshService)
    {
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;

        if (BffRequestPolicy.IsProxyPath(context.Request.Path) && isAuthenticated)
        {
            var accessToken = await tokenRefreshService.GetValidAccessTokenAsync(context);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                context.Response.StatusCode = context.Items.ContainsKey(TokenRefreshUnavailableItemKey)
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status401Unauthorized;
                return;
            }

            context.Items[AccessTokenItemKey] = accessToken;
        }

        await next(context);
    }
}
