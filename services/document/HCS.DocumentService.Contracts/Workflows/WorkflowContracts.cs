namespace HCS.DocumentService.Workflows;

public enum WorkflowInstanceStatus { Running, Completed, Rejected, Cancelled, Returned }
public enum ApprovalTaskStatus { Pending, Approved, Rejected, Cancelled, Returned }

public static class WorkflowSignModes
{
    public const string Sequential = "SEQUENTIAL";
    public const string Parallel = "PARALLEL";

    public static string Normalize(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? Sequential : value.Trim().ToUpperInvariant();
        if (normalized is not (Sequential or Parallel))
            throw new ArgumentException("Invalid workflow sign mode.");
        return normalized;
    }
}

public static class WorkflowStepAssigneeTypes
{
    public const string SpecificUser = "SpecificUser";
    public const string RoleInSubmitterOu = "RoleInSubmitterOu";
    public const string ScopedAssignee = "ScopedAssignee";

    public static string Normalize(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? SpecificUser : value.Trim();
        if (normalized is not (SpecificUser or RoleInSubmitterOu or ScopedAssignee))
            throw new ArgumentException("Invalid workflow assignee type.");
        return normalized;
    }
}

public sealed record WorkflowStepInput(string Code, string Name, int Order, string RequiredPermission,
    string Type = "PROCESS", Guid? AssigneeUserId = null, string AssigneeType = WorkflowStepAssigneeTypes.SpecificUser,
    Guid? RoleId = null, IReadOnlyList<Guid>? UserIds = null, IReadOnlyList<Guid>? DepartmentIds = null,
    int? SlaDays = null, bool AllowReturn = false);

public sealed record WorkflowStepAssignmentDto(string AssigneeType, Guid? RoleId, IReadOnlyList<Guid> UserIds,
    IReadOnlyList<Guid> DepartmentIds);

public sealed record CreateWorkflowDefinitionRequest(string Code, string Name, IReadOnlyList<WorkflowStepInput> Steps,
    Guid? KindId = null, string? Description = null, bool IsActive = true, string SignMode = WorkflowSignModes.Sequential);
public sealed record UpdateWorkflowDefinitionRequest(string Name, IReadOnlyList<WorkflowStepInput> Steps,
    Guid? KindId = null, string? Description = null, bool IsActive = true, string SignMode = WorkflowSignModes.Sequential);
public sealed record WorkflowStepDto(Guid Id, string Code, string Name, int Order, string RequiredPermission,
    string Type, Guid? AssigneeUserId, string AssigneeType, Guid? RoleId, IReadOnlyList<Guid> UserIds,
    IReadOnlyList<Guid> DepartmentIds, int? SlaDays, bool AllowReturn);
public sealed record WorkflowDefinitionDto(Guid Id, string Code, string Name, Guid? KindId, string? Description,
    bool IsActive, IReadOnlyList<WorkflowStepDto> Steps, DateTime CreationTime,
    string SignMode = WorkflowSignModes.Sequential);
public sealed record WorkflowKindDto(Guid Id, string Code, string Name, string? Description, bool IsActive, DateTime CreationTime);
public sealed record CreateWorkflowKindRequest(string Code, string Name, string? Description, bool IsActive = true);
public sealed record UpdateWorkflowKindRequest(string Name, string? Description, bool IsActive);
public sealed record CreateWorkflowTemplateRequest(string Code, string Name, Guid DefinitionId, int Version, string TemplateJson, string OutputFormat = "PDF");
public sealed record UpdateWorkflowTemplateRequest(string Name, string TemplateJson, string OutputFormat = "PDF");
public sealed record WorkflowTemplateDto(Guid Id, string Code, string Name, Guid DefinitionId, int Version, bool IsActive,
    DateTime CreationTime, Guid? WordFileId, string? WordFileName, Guid? PdfFileId, string? PdfFileName,
    string TemplateJson = "{}", string OutputFormat = "PDF");
public sealed record WorkflowStepSignerSelection(string StepCode, Guid UserId);
public sealed record WorkflowViewScopeSelection(string StepCode, IReadOnlyList<Guid> DepartmentIds, IReadOnlyList<Guid> UserIds);
public sealed record WorkflowAssigneeCandidateDto(Guid UserId, string DisplayName, Guid? OrganizationUnitId = null,
    string? UserName = null);
public sealed record WorkflowStepCandidateGroupDto(string StepCode, string StepName, string AssigneeType, Guid? RoleId,
    IReadOnlyList<WorkflowAssigneeCandidateDto> Candidates);
public sealed record StartWorkflowRequest(Guid? DocumentId, Guid DefinitionId, string IdempotencyKey,
    IReadOnlyList<WorkflowStepSignerSelection>? Signers = null,
    IReadOnlyList<WorkflowViewScopeSelection>? ViewScopes = null,
    bool UseTemplateFile = false, bool UseWorkflowTemplateFile = false, string? SigningContent = null);

public static class WorkflowStartRequestRules
{
    public static bool HasExactlyOneSource(StartWorkflowRequest input) =>
        input.UseWorkflowTemplateFile ^ input.DocumentId.HasValue;
}
public sealed record DecideApprovalTaskRequest(bool Approve, string? Comment, string IdempotencyKey, bool Return = false,
    Guid? SigningAttemptId = null, Guid? SigningFileId = null);
public sealed record ExtendWorkflowDueDateRequest(int AdditionalDays, string? Reason = null);
public sealed record ApprovalTaskDto(Guid Id, Guid InstanceId, string StepCode, ApprovalTaskStatus Status, Guid? DecidedBy,
    DateTime? DecidedAt, Guid? AssigneeUserId, DateTime? DueAt, string? Comment = null);
public sealed record WorkflowInstanceDto(Guid Id, Guid DocumentId, Guid DefinitionId, WorkflowInstanceStatus Status,
    int CurrentStep, IReadOnlyList<ApprovalTaskDto> Tasks, DateTime CreationTime);

public interface IWorkflowAppService
{
    Task<IReadOnlyList<WorkflowKindDto>> GetKindsAsync(CancellationToken cancellationToken = default);
    Task<WorkflowKindDto?> GetKindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateKindAsync(CreateWorkflowKindRequest input, CancellationToken cancellationToken = default);
    Task UpdateKindAsync(Guid id, UpdateWorkflowKindRequest input, CancellationToken cancellationToken = default);
    Task DeleteKindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowDefinitionDto>> GetDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionDto?> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default);
    Task<WorkflowTemplateDto?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowInstanceDto>> GetInstancesAsync(Guid? documentId = null,
        WorkflowInstanceStatus? status = null, CancellationToken cancellationToken = default);
    Task<WorkflowInstanceDto?> GetInstanceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateDefinitionAsync(CreateWorkflowDefinitionRequest input, CancellationToken cancellationToken = default);
    Task UpdateDefinitionAsync(Guid id, UpdateWorkflowDefinitionRequest input, CancellationToken cancellationToken = default);
    Task DeleteDefinitionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkflowTemplateDto> CreateTemplateAsync(CreateWorkflowTemplateRequest input, CancellationToken cancellationToken = default);
    Task<WorkflowTemplateDto> UpdateTemplateAsync(Guid id, UpdateWorkflowTemplateRequest input, CancellationToken cancellationToken = default);
    Task<WorkflowTemplateDto> SetTemplateActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<WorkflowTemplateDto> UploadTemplateFileAsync(Guid id, string kind, string fileName, string contentType,
        Stream content, long size, CancellationToken cancellationToken = default);
    Task<(string FileName, string ContentType, Stream Content)> OpenTemplateFileAsync(Guid id, string kind,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowStepCandidateGroupDto>> GetAssigneeCandidatesAsync(Guid definitionId,
        CancellationToken cancellationToken = default);
    Task<WorkflowInstanceDto> StartAsync(StartWorkflowRequest input, CancellationToken cancellationToken = default);
    Task<WorkflowInstanceDto> DecideAsync(Guid taskId, DecideApprovalTaskRequest input, CancellationToken cancellationToken = default);
    Task<WorkflowInstanceDto> ExtendDueDateAsync(Guid taskId, ExtendWorkflowDueDateRequest input, CancellationToken cancellationToken = default);
    Task<WorkflowInstanceDto> ResubmitAsync(Guid instanceId, string idempotencyKey, CancellationToken cancellationToken = default);
}
