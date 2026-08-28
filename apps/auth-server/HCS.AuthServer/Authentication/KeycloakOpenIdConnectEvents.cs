using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;

namespace HCS.AuthServer;

public static class KeycloakOpenIdConnectEvents
{
    public static OpenIdConnectEvents Create() => new()
    {
        OnRedirectToIdentityProvider = context =>
        {
            context.ProtocolMessage.Prompt = "login";
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            if (context.Principal is null)
            {
                context.Fail("Keycloak did not return an authenticated principal.");
                return;
            }

            var result = KeycloakClaimsProcessor.Apply(context.Principal);
            if (!result.IsAllowed)
            {
                context.Fail(result.FailureReason!);
                return;
            }

            try
            {
                var provisioner = context.HttpContext.RequestServices.GetRequiredService<IKeycloakUserProvisioner>();
                await provisioner.ProvisionAsync(
                    context.Principal,
                    result.Roles,
                    context.HttpContext.RequestAborted);
            }
            catch (KeycloakProvisioningException exception)
            {
                context.Fail(exception.Message);
            }
        }
    };
}
