using HCS.Blazor.Client.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace HCS.WebGateway.Tests;

public sealed class PermissionAuthorizationPolicyProviderTests
{
    [Theory]
    [InlineData("Collaboration.Chat")]
    [InlineData("WorkManagement.Dashboard")]
    [InlineData("WorkManagement.Surveys")]
    [InlineData("WorkManagement.Projects")]
    [InlineData("WorkManagement.ProjectTasks")]
    [InlineData("WorkManagement.Calendar")]
    [InlineData("WorkManagement.SurveyManagement")]
    [InlineData("Documents.View")]
    [InlineData("Documents.Workflow.View")]
    [InlineData("Documents.Signing.Execute")]
    [InlineData("Documents.Signing.Configure")]
    [InlineData("Documents.Signing.Report")]
    [InlineData("HCS.BusManagement.Reports")]
    [InlineData("HCS.BusManagement.Reconciliation.Close")]
    public async Task Module_permission_names_resolve_to_matching_claim_policies(string permission)
    {
        var provider = new PermissionAuthorizationPolicyProvider(
            Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync(permission);

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, requirement =>
            requirement is ClaimsAuthorizationRequirement claimRequirement
            && claimRequirement.ClaimType == "permission"
            && claimRequirement.AllowedValues?.Contains(permission) == true);
    }

    [Fact]
    public async Task Non_permission_names_are_not_created_dynamically()
    {
        var provider = new PermissionAuthorizationPolicyProvider(
            Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync("NotAConfiguredPolicy");

        Assert.Null(policy);
    }
}
