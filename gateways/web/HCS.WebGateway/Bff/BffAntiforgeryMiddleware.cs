using Microsoft.AspNetCore.Antiforgery;

namespace HCS.WebGateway;

internal sealed class BffAntiforgeryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (BffRequestPolicy.RequiresAntiforgery(context.Request))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.Headers["X-HCS-Antiforgery"] = "invalid";
                await context.Response.WriteAsJsonAsync(new { error = "invalid_antiforgery_token" });
                return;
            }
        }

        await next(context);
    }
}
