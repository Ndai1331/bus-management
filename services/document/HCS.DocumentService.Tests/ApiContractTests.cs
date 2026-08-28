using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using HCS.DocumentService.Controllers;
using HCS.DocumentService.Documents;
using HCS.DocumentService.Signing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HCS.DocumentService.Tests;

public sealed class ApiContractTests
{
    [Fact]
    public void Document_and_workflow_read_routes_are_authorized()
    {
        AssertHttpGet(typeof(DocumentsController), nameof(DocumentsController.GetList), null);
        AssertHttpGet(typeof(DocumentsController), nameof(DocumentsController.Get), "{id:guid}");
        AssertHttpGet(typeof(WorkflowsController), nameof(WorkflowsController.GetDefinitions), "definitions");
        AssertHttpGet(typeof(WorkflowsController), nameof(WorkflowsController.GetDefinition), "definitions/{id:guid}");
        AssertHttpGet(typeof(WorkflowsController), nameof(WorkflowsController.GetInstances), "instances");
        AssertHttpGet(typeof(SigningController), nameof(SigningController.GetQueue), "queue");
        Assert.NotNull(typeof(DocumentsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Single());
        Assert.NotNull(typeof(WorkflowsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Single());
        AssertHttpPost(typeof(DocumentsController), nameof(DocumentsController.Submit), "{id:guid}/submit");
        AssertHttpPut(typeof(WorkflowsController), nameof(WorkflowsController.UpdateDefinition), "definitions/{id:guid}");
        AssertHttpDelete(typeof(WorkflowsController), nameof(WorkflowsController.DeleteDefinition), "definitions/{id:guid}");
    }

    [Fact]
    public void Personal_signature_crud_routes_are_exposed()
    {
        AssertHttpPut(typeof(SigningController), nameof(SigningController.UpdateSignature), "signatures/{id:guid}");
        AssertHttpPut(typeof(SigningController), nameof(SigningController.SetDefaultSignature), "signatures/{id:guid}/default");
        AssertHttpDelete(typeof(SigningController), nameof(SigningController.DeleteSignature), "signatures/{id:guid}");
    }

    [Fact]
    public void Required_workflow_permissions_are_not_granted_to_unprivileged_users()
    {
        var employee = Principal("nhanvien");
        Assert.False(DocumentAccess.HasPermission(employee, "Documents.Review"));
        Assert.False(DocumentAccess.HasPermission(employee, "Documents.Approve"));
        Assert.True(DocumentAccess.HasPermission(Principal("bacsi"), "Documents.Review"));
        Assert.True(DocumentAccess.HasPermission(Principal("lanhdao"), "Documents.Approve"));
    }

    [Fact]
    public void Signing_idempotency_is_scoped_to_actor_and_operation()
    {
        var options = new DbContextOptionsBuilder<DocumentServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new DocumentServiceDbContext(options);
        var entity = db.Model.FindEntityType(typeof(Signing.SigningAttempt))!;
        var index = entity.GetIndexes().Single(x => x.IsUnique && x.Properties.Any(p => p.Name == "IdempotencyKey"));
        Assert.Equal(["UserId", "DocumentId", "FileId", "Kind", "IdempotencyKey"],
            index.Properties.Select(x => x.Name));
    }

    [Fact]
    public void Signing_request_validation_is_attached_to_record_constructor_parameter()
    {
        var constructor = typeof(SignDocumentRequest).GetConstructors().Single();
        var parameter = constructor.GetParameters().Single(x => x.Name == "IdempotencyKey");

        Assert.NotNull(parameter.GetCustomAttribute<RequiredAttribute>());
        var length = parameter.GetCustomAttribute<StringLengthAttribute>();
        Assert.NotNull(length);
        Assert.Equal(1, length!.MinimumLength);
        Assert.Equal(128, length.MaximumLength);
        Assert.Empty(typeof(SignDocumentRequest).GetProperty("IdempotencyKey")!
            .GetCustomAttributes<ValidationAttribute>());
    }

    [Fact]
    public void Document_service_validates_the_gateway_scope_audience()
    {
        var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false).Build();
        Assert.Equal("HCS", configuration["AuthServer:Audience"]);
    }

    [Fact]
    public void Bearer_apis_do_not_auto_validate_antiforgery_cookies()
    {
        var options = new Volo.Abp.AspNetCore.Mvc.AntiForgery.AbpAntiForgeryOptions { AutoValidate = true };
        BearerApiAntiforgery.DisableCookieValidation(options);
        Assert.False(options.AutoValidate);
        Assert.True(typeof(ControllerBase).IsAssignableFrom(typeof(WorkflowsController)));
        Assert.True(typeof(ControllerBase).IsAssignableFrom(typeof(DocumentsController)));
    }

    private static void AssertHttpGet(Type controller, string method, string? template)
    {
        var attribute = controller.GetMethod(method)!.GetCustomAttributes(typeof(HttpGetAttribute), true)
            .Cast<HttpGetAttribute>().Single();
        Assert.Equal(template, attribute.Template);
    }

    private static void AssertHttpPost(Type controller, string method, string? template)
    {
        var attribute = controller.GetMethod(method)!.GetCustomAttributes(typeof(HttpPostAttribute), true)
            .Cast<HttpPostAttribute>().Single();
        Assert.Equal(template, attribute.Template);
    }

    private static void AssertHttpPut(Type controller, string method, string? template)
    {
        var attribute = controller.GetMethod(method)!.GetCustomAttributes(typeof(HttpPutAttribute), true)
            .Cast<HttpPutAttribute>().Single();
        Assert.Equal(template, attribute.Template);
    }

    private static void AssertHttpDelete(Type controller, string method, string? template)
    {
        var attribute = controller.GetMethod(method)!.GetCustomAttributes(typeof(HttpDeleteAttribute), true)
            .Cast<HttpDeleteAttribute>().Single();
        Assert.Equal(template, attribute.Template);
    }

    private static ClaimsPrincipal Principal(string role) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)],
        "test", ClaimTypes.Name, ClaimTypes.Role));
}
