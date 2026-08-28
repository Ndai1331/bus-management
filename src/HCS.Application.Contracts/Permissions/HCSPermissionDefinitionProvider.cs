using HCS.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace HCS.Permissions;

public class HCSPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(HCSPermissions.GroupName, L("Permission:HCS"));
        var languages = group.AddPermission(HCSPermissions.Languages.Default, L("Permission:Languages"));
        languages.AddChild(HCSPermissions.Languages.Create, L("Permission:Languages.Create"));
        languages.AddChild(HCSPermissions.Languages.Update, L("Permission:Languages.Update"));
        languages.AddChild(HCSPermissions.Languages.Delete, L("Permission:Languages.Delete"));
        languages.AddChild(HCSPermissions.Languages.ManageTexts, L("Permission:Languages.ManageTexts"));
        group.AddPermission(HCSPermissions.AuditViewer.Default, L("Permission:AuditViewer"));

        var organization = context.AddGroup(HCSPermissions.Organization.Default, L("Permission:Organization"));
        AddCrud(organization.AddPermission(HCSPermissions.Organization.Departments, L("Permission:Organization.Departments")));
        AddCrud(organization.AddPermission(HCSPermissions.Organization.Units, L("Permission:Organization.Units")));
        AddCrud(organization.AddPermission(HCSPermissions.Organization.Positions, L("Permission:Organization.Positions")));
        organization.AddPermission(HCSPermissions.Organization.MasterData, L("Permission:Organization.MasterData"));
        organization.AddPermission(HCSPermissions.Organization.UserMappings, L("Permission:Organization.UserMappings"));

        var catalogs = context.AddGroup(HCSPermissions.Catalogs.Default, L("Permission:Catalogs"));
        AddCrud(catalogs.AddPermission(HCSPermissions.Catalogs.MasterData, L("Permission:Catalogs.MasterData")));
        AddCrud(catalogs.AddPermission(HCSPermissions.Catalogs.DocumentTypes, L("Permission:Catalogs.DocumentTypes")));
        AddCrud(catalogs.AddPermission(HCSPermissions.Catalogs.Sectors, L("Permission:Catalogs.Sectors")));
        AddCrud(catalogs.AddPermission(HCSPermissions.Catalogs.UrgencyLevels, L("Permission:Catalogs.UrgencyLevels")));
        AddCrud(catalogs.AddPermission(HCSPermissions.Catalogs.ConfidentialityLevels, L("Permission:Catalogs.ConfidentialityLevels")));
        AddCrud(catalogs.AddPermission(HCSPermissions.Catalogs.ProcessingMethods, L("Permission:Catalogs.ProcessingMethods")));
        AddCrud(catalogs.AddPermission(HCSPermissions.Catalogs.DocumentStatuses, L("Permission:Catalogs.DocumentStatuses")));
        AddCrud(catalogs.AddPermission(HCSPermissions.Catalogs.SigningMethods, L("Permission:Catalogs.SigningMethods")));
        AddCrud(catalogs.AddPermission(HCSPermissions.Catalogs.EventTypes, L("Permission:Catalogs.EventTypes")));

        var workManagement = context.AddGroup(HCSPermissions.WorkManagement.Default, L("Permission:WorkManagement"));
        workManagement.AddPermission(HCSPermissions.WorkManagement.Projects, L("Permission:WorkManagement.Projects"));
        workManagement.AddPermission(HCSPermissions.WorkManagement.Tasks, L("Permission:WorkManagement.ProjectTasks"));
        workManagement.AddPermission(HCSPermissions.WorkManagement.Calendar, L("Permission:WorkManagement.Calendar"));
        workManagement.AddPermission(HCSPermissions.WorkManagement.Surveys, L("Permission:WorkManagement.Surveys"));
        workManagement.AddPermission(HCSPermissions.WorkManagement.SurveyManagement, L("Permission:WorkManagement.SurveyManagement"));
        workManagement.AddPermission(HCSPermissions.WorkManagement.Reports, L("Permission:WorkManagement.Reports"));
        workManagement.AddPermission(HCSPermissions.WorkManagement.Dashboard, L("Permission:WorkManagement.Dashboard"));

        var documents = context.AddGroup(HCSPermissions.Documents.Default, L("Permission:Documents"));
        documents.AddPermission(HCSPermissions.Documents.View, L("Permission:Documents.View"));
        documents.AddPermission(HCSPermissions.Documents.Create, L("Permission:Documents.Create"));
        documents.AddPermission(HCSPermissions.Documents.Update, L("Permission:Documents.Update"));
        documents.AddPermission(HCSPermissions.Documents.Assign, L("Permission:Documents.Assign"));
        documents.AddPermission(HCSPermissions.Documents.ManageFiles, L("Permission:Documents.ManageFiles"));
        documents.AddPermission(HCSPermissions.Documents.WorkflowView, L("Permission:Documents.Workflow.View"));
        documents.AddPermission(HCSPermissions.Documents.WorkflowManage, L("Permission:Documents.Workflow.Manage"));
        documents.AddPermission(HCSPermissions.Documents.WorkflowStart, L("Permission:Documents.Workflow.Start"));
        documents.AddPermission(HCSPermissions.Documents.WorkflowDecide, L("Permission:Documents.Workflow.Decide"));
        documents.AddPermission(HCSPermissions.Documents.SigningConfigure, L("Permission:Documents.Signing.Configure"));
        documents.AddPermission(HCSPermissions.Documents.SigningExecute, L("Permission:Documents.Signing.Execute"));
        documents.AddPermission(HCSPermissions.Documents.SigningReport, L("Permission:Documents.Signing.Report"));

        var collaboration = context.AddGroup(HCSPermissions.Collaboration.Default, L("Permission:Collaboration"));
        collaboration.AddPermission(HCSPermissions.Collaboration.Chat, L("Permission:Collaboration.Chat"));
        collaboration.AddPermission(HCSPermissions.Collaboration.Notifications, L("Permission:Collaboration.Notifications"));
        collaboration.AddPermission(HCSPermissions.Collaboration.Administration, L("Permission:Collaboration.Administration"));

        var bus = context.AddGroup(HCSPermissions.BusManagement.Default, L("Permission:BusManagement"));
        AddCrud(bus.AddPermission(HCSPermissions.BusManagement.Stations, L("Permission:BusManagement.Stations")));
        AddCrud(bus.AddPermission(HCSPermissions.BusManagement.MasterData, L("Permission:BusManagement.MasterData")));
        AddCrud(bus.AddPermission(HCSPermissions.BusManagement.OperatorsContracts, L("Permission:BusManagement.OperatorsContracts")));
        AddCrud(bus.AddPermission(HCSPermissions.BusManagement.FleetCompliance, L("Permission:BusManagement.FleetCompliance")));
        AddCrud(bus.AddPermission(HCSPermissions.BusManagement.Departures, L("Permission:BusManagement.Departures")));
        AddCrud(bus.AddPermission(HCSPermissions.BusManagement.Revenue, L("Permission:BusManagement.Revenue")));
        var expenses = bus.AddPermission(HCSPermissions.BusManagement.Expenses, L("Permission:BusManagement.Expenses"));
        AddCrud(expenses);
        AddCrud(bus.AddPermission(HCSPermissions.BusManagement.Premises, L("Permission:BusManagement.Premises")));
        var reconciliation = bus.AddPermission(HCSPermissions.BusManagement.Reconciliation, L("Permission:BusManagement.Reconciliation"));
        AddCrud(reconciliation);
        reconciliation.AddChild(HCSPermissions.BusManagement.ReconciliationCheck, L("Permission:BusManagement.Reconciliation.Check"));
        reconciliation.AddChild(HCSPermissions.BusManagement.ReconciliationClose, L("Permission:BusManagement.Reconciliation.Close"));
        reconciliation.AddChild(HCSPermissions.BusManagement.ReconciliationAdjust, L("Permission:BusManagement.Reconciliation.Adjust"));
        reconciliation.AddChild(HCSPermissions.BusManagement.ReconciliationApprove, L("Permission:BusManagement.Reconciliation.Approve"));
        var reports = bus.AddPermission(HCSPermissions.BusManagement.Reports, L("Permission:BusManagement.Reports"));
        AddCrud(reports);
        reports.AddChild(HCSPermissions.BusManagement.ReportsExport, L("Permission:BusManagement.Reports.Export"));
        AddCrud(bus.AddPermission(HCSPermissions.BusManagement.StationAssignments, L("Permission:BusManagement.StationAssignments")));
        expenses.AddChild(HCSPermissions.BusManagement.ExpensesApprove, L("Permission:BusManagement.Expenses.Approve"));
    }

    private static void AddCrud(PermissionDefinition permission)
    {
        permission.AddChild(HcsCrudPermissions.Create(permission.Name), L("Permission:Create"));
        permission.AddChild(HcsCrudPermissions.Update(permission.Name), L("Permission:Update"));
        permission.AddChild(HcsCrudPermissions.Delete(permission.Name), L("Permission:Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<HCSResource>(name);
    }
}
