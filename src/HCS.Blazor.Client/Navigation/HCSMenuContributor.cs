using System;
using System.Threading.Tasks;
using HCS.Permissions;
using Volo.Abp.Account.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.UI.Navigation;

namespace HCS.Blazor.Client.Navigation;

public sealed class HCSMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            ConfigureMainMenu(context);
        }
        else if (context.Menu.Name == StandardMenus.User)
        {
            ConfigureUserMenu(context);
        }

        return Task.CompletedTask;
    }

    private static void ConfigureMainMenu(MenuConfigurationContext context)
    {
        context.Menu.AddItem(
            Item("HCS.Workspace", "Không gian làm việc", "/workspace", "fa fa-house", 10)
                .RequireAuthenticated());

        // context.Menu.AddItem(
        //     Item("HCS.Chat", "Trao đổi", "/chat", "fa fa-comments", 100)
        //         .RequirePermissions(HCSPermissions.Collaboration.Chat));

        var documents = Item("HCS.Documents", "Văn bản", icon: "fa fa-file-lines", order: 120);
        documents.AddItem(Item("HCS.Documents.Archive", "Quản lý tài liệu", "/manage-documents?sourceType=0", "fa fa-folder-open", 10)
            .RequirePermissions(HCSPermissions.Documents.View));
        documents.AddItem(Item("HCS.Documents.Personal", "Văn bản của tôi", "/manage-documents?sourceType=1", "fa fa-user", 20)
            .RequirePermissions(HCSPermissions.Documents.View));
        documents.AddItem(Item("HCS.Documents.SentToMe", "Văn bản gửi đến tôi", "/manage-documents?sourceType=2", "fa fa-inbox", 30)
            .RequirePermissions(new[] { HCSPermissions.Documents.View, HCSPermissions.Documents.Assign }));
        documents.AddItem(Item("HCS.Documents.Signing", "Ký duyệt", "/document-signing", "fa fa-signature", 40)
            .RequirePermissions(HCSPermissions.Documents.SigningExecute));
        context.Menu.AddItem(documents);

        var workflows = Item("HCS.Workflows", "Quy trình", icon: "fa fa-arrow-trend-up", order: 150);
        workflows.AddItem(Item("HCS.Workflows.Kinds", "Loại quy trình", "/workflow-definitions", "fa fa-diagram-project", 10)
            .RequirePermissions(HCSPermissions.Documents.WorkflowView));
        workflows.AddItem(Item("HCS.Workflows.List", "Quy trình", "/workflow-lists", "fa fa-code-branch", 20)
            .RequirePermissions(HCSPermissions.Documents.WorkflowView));
        workflows.AddItem(Item("HCS.Workflows.Instances", "Hồ sơ quy trình", "/document-workflow-instances", "fa fa-folder-open", 30)
            .RequirePermissions(HCSPermissions.Documents.WorkflowView));
        context.Menu.AddItem(workflows);

        var organization = Item("HCS.Organization", "Tổ chức", icon: "fa fa-sitemap", order: 200);
        organization.AddItem(Item("HCS.Organization.Departments", "Phòng ban", "/departments", "fa fa-diagram-project", 10)
            .RequirePermissions(HCSPermissions.Organization.Departments));
        organization.AddItem(Item("HCS.Organization.Units", "Đơn vị", "/unit-lists", "fa fa-building", 20)
            .RequirePermissions(HCSPermissions.Organization.Units));
        organization.AddItem(Item("HCS.Organization.Positions", "Chức vụ", "/positions", "fa fa-id-badge", 30)
            .RequirePermissions(HCSPermissions.Organization.Positions));
        context.Menu.AddItem(organization);

        context.Menu.AddItem(Item("HCS.BusManagement", "Quản lý bến xe", "/bus-management", "fa fa-bus", 250)
            .RequirePermissions(HCSPermissions.BusManagement.Reports));

        var catalogs = Item("HCS.Catalogs", "Danh mục", icon: "fa fa-tags", order: 300);
        catalogs.AddItem(Item("HCS.Catalogs.MasterData", "Danh mục dùng chung", "/master-datas", "fa fa-list", 10)
            .RequirePermissions(false, HCSPermissions.Catalogs.MasterData, HCSPermissions.Organization.MasterData));
        var documentCatalogs = Item("HCS.Catalogs.Documents", "Danh mục văn bản", icon: "fa fa-file-lines", order: 20);
        documentCatalogs.AddItem(Item("HCS.Catalogs.DocumentTypes", "Loại văn bản", "/document-types", order: 10)
            .RequirePermissions(false, HCSPermissions.Catalogs.DocumentTypes, HCSPermissions.Organization.MasterData, HCSPermissions.Catalogs.MasterData));
        documentCatalogs.AddItem(Item("HCS.Catalogs.Sectors", "Lĩnh vực", "/sectors", order: 20)
            .RequirePermissions(false, HCSPermissions.Catalogs.Sectors, HCSPermissions.Organization.MasterData, HCSPermissions.Catalogs.MasterData));
        documentCatalogs.AddItem(Item("HCS.Catalogs.UrgencyLevels", "Độ khẩn", "/urgency-levels", order: 30)
            .RequirePermissions(false, HCSPermissions.Catalogs.UrgencyLevels, HCSPermissions.Organization.MasterData, HCSPermissions.Catalogs.MasterData));
        documentCatalogs.AddItem(Item("HCS.Catalogs.ConfidentialityLevels", "Độ mật", "/confidentiality-levels", order: 40)
            .RequirePermissions(false, HCSPermissions.Catalogs.ConfidentialityLevels, HCSPermissions.Organization.MasterData, HCSPermissions.Catalogs.MasterData));
        documentCatalogs.AddItem(Item("HCS.Catalogs.ProcessingMethods", "Phương thức xử lý", "/processing-methods", order: 50)
            .RequirePermissions(false, HCSPermissions.Catalogs.ProcessingMethods, HCSPermissions.Organization.MasterData, HCSPermissions.Catalogs.MasterData));
        documentCatalogs.AddItem(Item("HCS.Catalogs.DocumentStatus", "Trạng thái văn bản", "/document-status", order: 60)
            .RequirePermissions(false, HCSPermissions.Catalogs.DocumentStatuses, HCSPermissions.Organization.MasterData, HCSPermissions.Catalogs.MasterData));
        documentCatalogs.AddItem(Item("HCS.Catalogs.SigningMethods", "Phương thức ký", "/signing-methods", order: 70)
            .RequirePermissions(false, HCSPermissions.Catalogs.SigningMethods, HCSPermissions.Organization.MasterData, HCSPermissions.Catalogs.MasterData));
        documentCatalogs.AddItem(Item("HCS.Catalogs.EventTypes", "Loại sự kiện", "/event-types", order: 80)
            .RequirePermissions(false, HCSPermissions.Catalogs.EventTypes, HCSPermissions.Organization.MasterData, HCSPermissions.Catalogs.MasterData));
        catalogs.AddItem(documentCatalogs);
        context.Menu.AddItem(catalogs);
    }

    private void ConfigureUserMenu(MenuConfigurationContext context)
    {
        // Remove default relative Account links that 404 on the BFF UI host.
        context.Menu.Items.RemoveAll(item =>
            !string.IsNullOrWhiteSpace(item.Url) &&
            item.Url.Contains("/Account/", StringComparison.OrdinalIgnoreCase));

        var accountResource = context.GetLocalizer<AccountResource>();
        context.Menu.AddItem(new ApplicationMenuItem(
                "Account.Manage",
                accountResource["MyAccount"],
                "/account",
                "fa fa-user-gear",
                900)
            .RequireAuthenticated());
    }

    private static ApplicationMenuItem Item(
        string name,
        string displayName,
        string? url = null,
        string? icon = null,
        int order = 0) => new ApplicationMenuItem(name, displayName, url, icon, order);

}
