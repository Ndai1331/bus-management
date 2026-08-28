namespace HCS.WebGateway;

internal sealed class BffWebSocketOriginMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly string[] allowedOrigins = HCSWebGatewayModule.GetCorsOrigins(configuration);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!BffDeploymentPolicy.IsAllowedWebSocketOrigin(context.Request, allowedOrigins))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "websocket_origin_denied" });
            return;
        }

        await next(context);
    }
}
