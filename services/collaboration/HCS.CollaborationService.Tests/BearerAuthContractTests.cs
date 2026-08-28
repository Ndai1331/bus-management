using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;

namespace HCS.CollaborationService.Tests;

public sealed class BearerAuthContractTests
{
    [Fact]
    public void Bearer_apis_do_not_auto_validate_antiforgery_cookies()
    {
        var options = new AbpAntiForgeryOptions { AutoValidate = true };
        BearerApiAntiforgery.DisableCookieValidation(options);
        options.AutoValidate.ShouldBeFalse();
    }

    [Fact]
    public void Jwt_issuers_include_the_public_authority_with_and_without_slash()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuthServer:Authority"] = "https://auth.hcs.localhost"
        }).Build();

        var issuers = CollaborationJwtBearer.ResolveIssuers(configuration);

        issuers.ShouldContain("https://auth.hcs.localhost");
        issuers.ShouldContain("https://auth.hcs.localhost/");
    }

    [Fact]
    public void Jwt_accepts_the_public_issuer_even_when_metadata_is_fetched_internally()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuthServer:Authority"] = "https://auth.hcs.localhost",
            ["AuthServer:ValidIssuers:0"] = "http://auth-server:8080",
            ["AuthServer:AllowUntrustedBackchannelCertificate"] = "true",
            ["AuthServer:Audience"] = "HCS"
        }).Build();

        var options = new JwtBearerOptions();
        CollaborationJwtBearer.Configure(options, configuration);

        options.Authority.ShouldBe("https://auth.hcs.localhost");
        options.Audience.ShouldBe("HCS");
        options.MapInboundClaims.ShouldBeFalse();
        options.TokenValidationParameters.NameClaimType.ShouldBe(CollaborationJwtBearer.JwtSubjectClaim);
        options.TokenValidationParameters.RoleClaimType.ShouldBe(CollaborationJwtBearer.JwtRoleClaim);
        Volo.Abp.Security.Claims.AbpClaimTypes.UserId.ShouldBe(CollaborationJwtBearer.JwtSubjectClaim);
        Volo.Abp.Security.Claims.AbpClaimTypes.Role.ShouldBe(CollaborationJwtBearer.JwtRoleClaim);
        Volo.Abp.Security.Claims.AbpClaimTypes.UserName.ShouldBe("preferred_username");
        Volo.Abp.Security.Claims.AbpClaimTypes.Name.ShouldBe("given_name");
        Volo.Abp.Security.Claims.AbpClaimTypes.SurName.ShouldBe("family_name");
        options.BackchannelHttpHandler.ShouldNotBeNull();
        CollaborationJwtBearer.ResolveIssuers(configuration)
            .ShouldContain("https://auth.hcs.localhost/");
        CollaborationJwtBearer.ResolveIssuers(configuration)
            .ShouldContain("http://auth-server:8080/");
    }
}
