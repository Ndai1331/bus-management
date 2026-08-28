using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HCS.Auditing;
using HCS.Localization;
using HCS.IntegrationEvents.Auditing;
using HCS.PlatformService;
using Shouldly;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;
using Xunit;

namespace HCS.EntityFrameworkCore;

[Collection(HCSTestConsts.CollectionDefinitionName)]
public class PlatformFeatureTests : HCSEntityFrameworkCoreTestBase
{
    private readonly ILanguageAppService _languages;
    private readonly ILanguageTextAppService _texts;
    private readonly IAuditViewerAppService _auditViewer;
    private readonly IAuditRecordProjectionRepository _auditRepository;
    private readonly AuditRecordIntegrationEventHandler _auditHandler;
    private readonly ILocalizationStore _localizationStore;

    public PlatformFeatureTests()
    {
        _languages = GetRequiredService<ILanguageAppService>();
        _texts = GetRequiredService<ILanguageTextAppService>();
        _auditViewer = GetRequiredService<IAuditViewerAppService>();
        _auditRepository = GetRequiredService<IAuditRecordProjectionRepository>();
        _auditHandler = GetRequiredService<AuditRecordIntegrationEventHandler>();
        _localizationStore = GetRequiredService<ILocalizationStore>();
    }

    [Fact]
    public async Task Should_Keep_Only_One_Default_Language()
    {
        await _languages.CreateAsync(new CreateLanguageDto
        {
            CultureName = "fr-FR", DisplayName = "Français", IsEnabled = true, IsDefault = true
        });
        await _languages.CreateAsync(new CreateLanguageDto
        {
            CultureName = "de-DE", DisplayName = "Deutsch", IsEnabled = true, IsDefault = true
        });

        var result = await _languages.GetListAsync(new GetLanguagesInput { MaxResultCount = 100 });
        result.Items.Count(x => x.IsDefault).ShouldBe(1);
        result.Items.Single(x => x.IsDefault).CultureName.ShouldBe("de-DE");
    }

    [Fact]
    public async Task Should_Update_Translation_Value()
    {
        await _languages.CreateAsync(new CreateLanguageDto
        {
            CultureName = "es-ES", DisplayName = "Español", IsEnabled = true
        });
        var created = await _texts.CreateAsync(new CreateLanguageTextDto
        {
            ResourceName = "HCS", CultureName = "es-ES", Name = "Welcome", Value = "Hola"
        });
        (await _localizationStore.GetTextsAsync("HCS", "es-ES"))["Welcome"].ShouldBe("Hola");

        var updated = await _texts.UpdateAsync(created.Id, new UpdateLanguageTextDto { Value = "Bienvenido" });

        updated.Value.ShouldBe("Bienvenido");
        (await _texts.GetAsync(created.Id)).Value.ShouldBe("Bienvenido");
        (await _localizationStore.GetTextsAsync("HCS", "es-ES"))["Welcome"].ShouldBe("Bienvenido");
    }

    [Fact]
    public async Task Should_Filter_Audit_Logs_By_User_Time_Status_And_Correlation()
    {
        var userId = Guid.NewGuid();
        var executionTime = DateTime.UtcNow;
        var matching = CreateAuditRecord(Guid.NewGuid(), userId, executionTime, 201, "platform-filter", "Documents.Approve");
        var other = CreateAuditRecord(Guid.NewGuid(), Guid.NewGuid(), executionTime, 500, "other-filter", "Chat.Send");

        await _auditHandler.HandleEventAsync(matching);
        await _auditHandler.HandleEventAsync(matching);
        await _auditHandler.HandleEventAsync(other);

        var result = await _auditViewer.GetListAsync(new GetAuditLogsInput
        {
            UserId = userId,
            StartTime = executionTime.AddMinutes(-1),
            EndTime = executionTime.AddMinutes(1),
            HttpStatusCode = 201,
            CorrelationId = "platform-filter",
            Action = "Approve",
            MaxResultCount = 10
        });

        result.TotalCount.ShouldBe(1);
        result.Items.Single().Id.ShouldBe(matching.Id);
        result.Items.Single().SourceService.ShouldBe("DocumentService");
        result.Items.Single().ApplicationName.ShouldBe("HCS");
        (await _auditRepository.GetCountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Should_Search_Audit_Logs_By_Request_Metadata()
    {
        var executionTime = DateTime.UtcNow;
        var source = CreateAuditRecord(Guid.NewGuid(), Guid.NewGuid(), executionTime, 202, "metadata-filter", "Users.Update");
        await _auditHandler.HandleEventAsync(source);

        var result = await _auditViewer.GetListAsync(new GetAuditLogsInput
        {
            Filter = "test-browser",
            HttpMethod = "get",
            ClientIpAddress = "127.0.0.1",
            BrowserInfo = "browser",
            SourceService = "DocumentService",
            ApplicationName = "HCS",
            Url = "/api/test",
            HasException = false,
            CorrelationId = "metadata-filter",
            MaxResultCount = 100
        });

        result.TotalCount.ShouldBe(1);
        result.Items.Single().ActionName.ShouldBe("Users.Update");

        var byUserId = await _auditViewer.GetListAsync(new GetAuditLogsInput
        {
            Filter = source.UserId!.Value.ToString("D"),
            MaxResultCount = 100
        });
        byUserId.Items.ShouldContain(item => item.Id == source.Id);
    }

    [Fact]
    public async Task Should_Treat_Audit_End_Time_As_Exclusive()
    {
        var endTime = DateTime.UtcNow;
        var atEnd = CreateAuditRecord(Guid.NewGuid(), Guid.NewGuid(), endTime, 200, "end-exclusive", "Users.AtEnd");
        var beforeEnd = CreateAuditRecord(Guid.NewGuid(), Guid.NewGuid(), endTime.AddTicks(-1), 200, "before-end", "Users.BeforeEnd");

        await _auditHandler.HandleEventAsync(atEnd);
        await _auditHandler.HandleEventAsync(beforeEnd);

        var result = await _auditViewer.GetListAsync(new GetAuditLogsInput
        {
            StartTime = endTime.AddMinutes(-1),
            EndTimeExclusive = endTime,
            MaxResultCount = 100
        });

        result.Items.ShouldContain(item => item.Id == beforeEnd.Id);
        result.Items.ShouldNotContain(item => item.Id == atEnd.Id);

        var legacyResult = await _auditViewer.GetListAsync(new GetAuditLogsInput
        {
            StartTime = endTime.AddMinutes(-1),
            EndTime = endTime,
            CorrelationId = "end-exclusive",
            MaxResultCount = 100
        });
        legacyResult.Items.ShouldContain(item => item.Id == atEnd.Id);
    }

    [Fact]
    public async Task Should_Not_Project_Raw_Exception_Text()
    {
        var source = CreateAuditRecord(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, 500, "safe-audit", "Documents.Fail")
            with { Exceptions = "database password=not-for-viewer" };

        await _auditHandler.HandleEventAsync(source);

        var result = await _auditViewer.GetAsync(source.Id);
        result.Exceptions.ShouldBe(AuditExceptionSanitizer.RequestFailed);
        result.Exceptions!.ShouldNotContain("password");
    }

    [Fact]
    public async Task Should_Not_Project_Audit_Action_Parameters()
    {
        var source = CreateAuditRecord(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, 200, "safe-action", "Users.Update")
            with
            {
                Actions = [new AuditActionCapturedEto(Guid.NewGuid(), "Users", "Update", "password=secret", DateTime.UtcNow, 4)]
            };

        await _auditHandler.HandleEventAsync(source);

        var result = await _auditViewer.GetAsync(source.Id);
        result.Actions.Single().Parameters.ShouldBeNull();
    }

    [Fact]
    public void Should_Resolve_Audit_Display_Name_From_Profile_Claims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("name", "Nguyễn Văn A"),
            new Claim("preferred_username", "user-a")
        ], "test"));

        AuditUserNameResolver.Resolve(principal).ShouldBe("Nguyễn Văn A");
    }

    [Fact]
    public void Should_Register_RabbitMq_Event_Bus_For_Audit_Projection()
    {
        var dependencies = typeof(HCSPlatformServiceModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .SelectMany(attribute => attribute.GetDependedTypes())
            .ToArray();

        dependencies.ShouldContain(typeof(AbpEventBusRabbitMqModule));
    }

    private static AuditRecordCapturedEto CreateAuditRecord(
        Guid id,
        Guid userId,
        DateTime executionTime,
        int statusCode,
        string correlationId,
        string action) =>
        new(id, "DocumentService", "HCS", userId, "tester", executionTime, 25, action,
            "GET", "/api/test", statusCode, correlationId, "127.0.0.1", "test-browser", null, null, [], []);
}
