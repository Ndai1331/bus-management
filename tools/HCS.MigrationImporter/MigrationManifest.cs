namespace HCS.MigrationImporter;

public static class MigrationManifest
{
    public static TableMigrationSpec UserMappingSource { get; } = T("AbpUsers", TargetDatabase.Identity, "AbpUsers", ["Id"]);

    // Only custom/OSS-owned tables are eligible. A table not listed here cannot be read.
    public static IReadOnlyList<TableMigrationSpec> Tables { get; } =
    [
        T("AppDepartments", TargetDatabase.Organization, "Departments", ["Id"], ["ManagerId"]),
        T("AppUnits", TargetDatabase.Organization, "Units", ["Id"], relationships: [R("DepartmentId", "Departments")]),
        T("AppPositions", TargetDatabase.Organization, "Positions", ["Id"]),
        T("AppMasterDatas", TargetDatabase.Organization, "MasterDataItems", ["Id"]),
        T("AppUserDepartments", TargetDatabase.Organization, "UserOrganizationMappings", ["Id"], ["UserId"], relationships: [R("DepartmentId", "Departments"), R("UnitId", "Units"), R("PositionId", "Positions")]),
        T("AppDocuments", TargetDatabase.Document, "Documents", ["Id"], ["AssigneeId"]),
        T("AppDocumentFiles", TargetDatabase.Document, "DocumentFiles", ["Id"], null, ["Path"], [R("DocumentId", "Documents")]),
        T("AppDocumentHistories", TargetDatabase.Document, "DocumentHistories", ["Id"], ["UserId"], relationships: [R("DocumentId", "Documents")]),
        T("AppDocumentAssignments", TargetDatabase.Document, "DocumentAssignments", ["Id"], ["UserId", "AssigneeId"], relationships: [R("DocumentId", "Documents")]),
        T("AppWorkflowDefinitions", TargetDatabase.Document, "WorkflowDefinitions", ["Id"]),
        T("AppWorkflowTemplates", TargetDatabase.Document, "WorkflowTemplates", ["Id"]),
        T("AppWorkflowStepTemplates", TargetDatabase.Document, "WorkflowSteps", ["Id"]),
        T("AppDocumentWorkflowInstances", TargetDatabase.Document, "WorkflowInstances", ["Id"], ["CreatorId"]),
        T("AppSignatureSettings", TargetDatabase.Document, "SigningCredentials", ["Id"], ["UserId"]),
        T("AppUserSignatures", TargetDatabase.Document, "SigningAttempts", ["Id"], ["UserId"], ["Path"]),
        T("AppProjects", TargetDatabase.Work, "Projects", ["Id"], ["OwnerId"]),
        T("AppProjectMembers", TargetDatabase.Work, "ProjectMembers", ["Id"], ["UserId"], relationships: [R("ProjectId", "Projects")]),
        T("AppProjectTasks", TargetDatabase.Work, "ProjectTasks", ["Id"], ["AssigneeId"], relationships: [R("ProjectId", "Projects")]),
        T("AppProjectTaskAssignments", TargetDatabase.Work, "ProjectTaskAssignments", ["Id"], ["UserId", "AssigneeId"], relationships: [R("ProjectTaskId", "ProjectTasks")]),
        T("AppProjectTaskDocuments", TargetDatabase.Work, "ProjectTaskDocuments", ["Id"], relationships: [R("ProjectTaskId", "ProjectTasks")]),
        T("AppCalendarEvents", TargetDatabase.Work, "CalendarEvents", ["Id"], ["OwnerId"]),
        T("AppCalendarEventParticipants", TargetDatabase.Work, "CalendarEventParticipants", ["Id"], ["UserId"], relationships: [R("CalendarEventId", "CalendarEvents")]),
        T("AppSurveySessions", TargetDatabase.Work, "SurveySessions", ["Id"]),
        T("AppSurveyCriterias", TargetDatabase.Work, "SurveyCriteria", ["Id"]),
        T("AppSurveyLocations", TargetDatabase.Work, "SurveyLocations", ["Id"]),
        T("AppSurveyResults", TargetDatabase.Work, "SurveyResults", ["Id"], ["UserId"]),
        T("AppSurveyFiles", TargetDatabase.Work, "SurveyFiles", ["Id"], null, ["FilePath"]),
        T("AppReports", TargetDatabase.Work, "ReportReadModels", ["Id"]),
        T("ChatConversations", TargetDatabase.Collaboration, "CollaborationConversations", ["Id"]),
        T("ChatConversationMembers", TargetDatabase.Collaboration, "CollaborationConversationMembers", ["Id"], ["UserId"], relationships: [R("ConversationId", "CollaborationConversations")]),
        T("ChatMessages", TargetDatabase.Collaboration, "CollaborationMessages", ["Id"], ["SenderId"], relationships: [R("ConversationId", "CollaborationConversations")]),
        T("ChatMessageFiles", TargetDatabase.Collaboration, "CollaborationAttachments", ["Id"], null, ["Path"], [R("MessageId", "CollaborationMessages")]),
        T("ChatUserMessages", TargetDatabase.Collaboration, "CollaborationInbox", ["Id"], ["UserId"]),
        T("AppNotifications", TargetDatabase.Collaboration, "CollaborationNotifications", ["Id"], ["CreatorId"]),
        T("AppNotificationReceivers", TargetDatabase.Collaboration, "CollaborationNotificationReceivers", ["Id"], ["UserId"]),
        T("AppUserPushDeviceTokens", TargetDatabase.Collaboration, "CollaborationPushDeviceTokens", ["Id"], ["UserId"]),
        T("AbpAuditLogs", TargetDatabase.Identity, "AbpAuditLogs", ["Id"], ["UserId"])
    ];

    public static readonly string[] ExcludedNameFragments =
    ["Saas", "Tenant", "Gdpr", "TextTemplate", "FileManagement", "Form", "OpenIddictPro"];

    public static IReadOnlyList<TableMigrationSpec> Select(IReadOnlySet<string>? requested)
    {
        if (requested is null || requested.Count == 0) return Tables;
        var unknown = requested.Except(Tables.Select(x => x.SourceTable), StringComparer.OrdinalIgnoreCase).ToArray();
        if (unknown.Length > 0) throw new InvalidOperationException($"Tables are not allowlisted: {string.Join(", ", unknown)}");
        return Tables.Where(x => requested.Contains(x.SourceTable)).ToArray();
    }

    public static void EnsureAllowed(string table)
    {
        if (ExcludedNameFragments.Any(x => table.Contains(x, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Excluded commercial/module table rejected: {table}");
        if (!Tables.Append(UserMappingSource).Any(x => x.SourceTable.Equals(table, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Table is not in the explicit migration allowlist: {table}");
    }

    private static TableMigrationSpec T(string source, TargetDatabase db, string target, string[] keys,
        string[]? users = null, string[]? blobs = null, RelationshipSpec[]? relationships = null)
        => new(source, db, target, keys, users, blobs, relationships);
    private static RelationshipSpec R(string column, string table) => new(column, table);
}
