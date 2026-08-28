using HCS.WorkManagementService.Contracts;
using HCS.WorkManagementService.Domain;
using HCS.WorkManagementService.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace HCS.WorkManagementService.Tests;

public sealed class SecurityContractTests
{
    [Fact]
    public void Survey_submission_ignores_a_caller_supplied_user_id()
    {
        var authenticated = Guid.NewGuid();
        Assert.Equal(authenticated, HCS.WorkManagementService.Application.SurveySubmissionIdentity.Resolve(
            authenticated, Guid.NewGuid()));
    }

    [Fact]
    public void Public_work_asset_contract_never_exposes_internal_blob_name()
    {
        Assert.Null(typeof(SurveyFileReferenceDto).GetProperty("BlobName"));
    }

    [Fact]
    public void Owned_records_reject_an_empty_caller_identity()
    {
        Assert.Throws<Volo.Abp.BusinessException>(() => new Project(Guid.NewGuid(), "P", "Project",
            DateTime.UtcNow, DateTime.UtcNow, "Active", null, Guid.Empty));
        Assert.Throws<Volo.Abp.BusinessException>(() => new SurveySession(Guid.NewGuid(), "S", "Survey",
            DateTime.UtcNow, DateTime.UtcNow, null, Guid.Empty));
    }

    [Fact]
    public void Survey_submission_always_requires_the_active_window()
    {
        var owner = Guid.NewGuid(); var audience = Guid.NewGuid(); var now = DateTime.UtcNow;
        Assert.False(HCS.WorkManagementService.Application.SurveyAccessRules.CanSubmit(false, owner, owner,
            "Draft", now.AddDays(1), now.AddDays(2), now));
        Assert.True(HCS.WorkManagementService.Application.SurveyAccessRules.CanSubmit(false, owner, audience,
            "Active", now.AddMinutes(-1), now.AddMinutes(1), now));
        Assert.False(HCS.WorkManagementService.Application.SurveyAccessRules.CanSubmit(false, owner, audience,
            "Active", now.AddMinutes(1), now.AddMinutes(2), now));
    }

    [Theory]
    [InlineData(nameof(SurveysController.CreateCriteria))]
    [InlineData(nameof(SurveysController.CreateLocation))]
    [InlineData(nameof(SurveysController.CreateSession))]
    [InlineData(nameof(SurveysController.UpdateCriteria))]
    [InlineData(nameof(SurveysController.DeleteCriteria))]
    [InlineData(nameof(SurveysController.UpdateLocation))]
    [InlineData(nameof(SurveysController.DeleteLocation))]
    [InlineData(nameof(SurveysController.UpdateSession))]
    [InlineData(nameof(SurveysController.ChangeStatus))]
    [InlineData(nameof(SurveysController.DeleteSession))]
    public void Global_survey_management_endpoints_require_the_management_policy(string action)
    {
        var method = typeof(SurveysController).GetMethod(action)!;
        Assert.Contains(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(),
            attribute => attribute.Policy == WorkPermissions.SurveyManagement);
    }

    [Theory]
    [InlineData(nameof(SurveysController.GetResults))]
    [InlineData(nameof(SurveysController.Submit))]
    [InlineData(nameof(SurveysController.GetFiles))]
    public void Survey_participant_endpoints_inherit_the_survey_policy(string action)
    {
        Assert.NotNull(typeof(SurveysController).GetMethod(action));
        Assert.Contains(typeof(SurveysController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>(),
            attribute => attribute.Policy == WorkPermissions.Surveys);
    }

    [Theory]
    [InlineData(nameof(SurveysController.GetPublicLocation))]
    [InlineData(nameof(SurveysController.GetPublicCriteria))]
    [InlineData(nameof(SurveysController.CreatePublicSession))]
    [InlineData(nameof(SurveysController.SubmitPublicResults))]
    [InlineData(nameof(SurveysController.UploadPublic))]
    public void Public_survey_endpoints_are_explicitly_anonymous(string action)
    {
        var method = typeof(SurveysController).GetMethod(action)!;
        Assert.NotNull(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).SingleOrDefault());
    }

    [Fact]
    public void Bearer_apis_do_not_auto_validate_antiforgery_cookies()
    {
        var options = new Volo.Abp.AspNetCore.Mvc.AntiForgery.AbpAntiForgeryOptions { AutoValidate = true };
        BearerApiAntiforgery.DisableCookieValidation(options);
        Assert.False(options.AutoValidate);
        Assert.True(typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(typeof(ProjectsController)));
    }
}
