using System;
using System.Collections.Generic;

namespace HCS.Blazor.Client.Documents;

public enum DocumentStatus { Draft, Submitted, InReview, Approved, Rejected, Archived }
public enum DocumentSourceType { Archive = 0, Personal = 1, SentToMe = 2, Workflow = 3 }
public enum WorkflowInstanceStatus { Running, Completed, Rejected, Cancelled, Returned }
public enum ApprovalTaskStatus { Pending, Approved, Rejected, Cancelled, Returned }
public enum SigningStatus { Pending, Completed, Failed }
public enum UserSignatureType { Electronic, Digital }
public enum SigningKind { Electronic = 0, RemoteCa = 1, Hsm = 2, UsbToken = 3 }

public static class DocumentStatusUi
{
    public static string TextKey(DocumentStatus status) => $"Document:Status.{status}";

    public static string BadgeClass(DocumentStatus status) => status switch
    {
        DocumentStatus.Approved => "hcs-status-badge--success",
        DocumentStatus.Rejected => "hcs-status-badge--danger",
        DocumentStatus.InReview or DocumentStatus.Submitted => "hcs-status-badge--warning",
        DocumentStatus.Archived => "hcs-status-badge--todo",
        _ => "hcs-status-badge--planning"
    };
}

public sealed record PagedDocumentsResponse(long TotalCount, List<DocumentDto> Items);

public sealed record DocumentFileDto(Guid Id, string FileName, string ContentType, long Size, string Sha256, DateTime CreationTime, Guid? PairedFileId = null);
public sealed record DocumentFileContent(byte[] Bytes, string ContentType, string FileName);
public sealed record DocumentAssignmentDto(Guid Id, Guid AssigneeUserId, string Responsibility, DateTime AssignedAt,
    bool IsCurrent = true, string? StepCode = null);
public sealed record DocumentHistoryDto(Guid Id, string Action, Guid? ActorUserId, string? Detail, DateTime OccurredAt);
public sealed record DocumentDto(
    Guid Id, string Number, string Title, string? Description, DocumentStatus Status,
    Guid? DocumentTypeId, Guid? SectorId, Guid? UrgencyId, Guid? ConfidentialityId,
    List<DocumentFileDto> Files, List<DocumentAssignmentDto> Assignments,
    List<DocumentHistoryDto> History, DateTime CreationTime,
    DocumentSourceType SourceType = DocumentSourceType.Archive, Guid? ParentDocumentId = null,
    Guid? FromUserId = null, Guid? OrganizationUnitId = null);

public sealed record CreateDocumentRequest(
    string? Number, string Title, string? Description,
    Guid? DocumentTypeId, Guid? SectorId, Guid? UrgencyId, Guid? ConfidentialityId,
    DocumentSourceType SourceType = DocumentSourceType.Archive);
public sealed record SendDocumentRequest(Guid? ReceiverUserId = null, Guid? OrganizationUnitId = null);
public sealed record UpdateDocumentRequest(
    string Title, string? Description,
    Guid? DocumentTypeId, Guid? SectorId, Guid? UrgencyId, Guid? ConfidentialityId);
public sealed record AssignDocumentRequest(Guid AssigneeUserId, string Responsibility);

public sealed record WorkflowStepInput(string Code, string Name, int Order, string RequiredPermission, string Type = "PROCESS",
    Guid? AssigneeUserId = null, string AssigneeType = "SpecificUser", Guid? RoleId = null,
    List<Guid>? UserIds = null, List<Guid>? DepartmentIds = null, int? SlaDays = null, bool AllowReturn = false);
public sealed record WorkflowStepDto(Guid Id, string Code, string Name, int Order, string RequiredPermission, string Type, Guid? AssigneeUserId,
    string AssigneeType = "SpecificUser", Guid? RoleId = null, List<Guid>? UserIds = null, List<Guid>? DepartmentIds = null,
    int? SlaDays = null, bool AllowReturn = false);
public sealed record WorkflowKindDto(Guid Id, string Code, string Name, string? Description, bool IsActive, DateTime CreationTime);
public sealed record CreateWorkflowKindRequest(string Code, string Name, string? Description, bool IsActive = true);
public sealed record UpdateWorkflowKindRequest(string Name, string? Description, bool IsActive);
public sealed record WorkflowDefinitionDto(Guid Id, string Code, string Name, List<WorkflowStepDto> Steps, DateTime CreationTime,
    Guid? KindId = null, string? Description = null, bool IsActive = true, string SignMode = "SEQUENTIAL");
public sealed record CreateWorkflowDefinitionRequest(string Code, string Name, List<WorkflowStepInput> Steps,
    Guid? KindId = null, string? Description = null, bool IsActive = true, string SignMode = "SEQUENTIAL");
public sealed record UpdateWorkflowDefinitionRequest(string Name, List<WorkflowStepInput> Steps,
    Guid? KindId = null, string? Description = null, bool IsActive = true, string SignMode = "SEQUENTIAL");
public sealed record WorkflowTemplateDto(Guid Id, string Code, string Name, Guid DefinitionId, int Version, bool IsActive, DateTime CreationTime, Guid? WordFileId, string? WordFileName, Guid? PdfFileId, string? PdfFileName, string TemplateJson = "{}", string OutputFormat = "PDF");
public sealed record CreateWorkflowTemplateRequest(string Code, string Name, Guid DefinitionId, int Version, string TemplateJson, string OutputFormat = "PDF");
public sealed record UpdateWorkflowTemplateRequest(string Name, string TemplateJson, string OutputFormat = "PDF");
public sealed record ApprovalTaskDto(Guid Id, Guid InstanceId, string StepCode, ApprovalTaskStatus Status, Guid? DecidedBy, DateTime? DecidedAt, Guid? AssigneeUserId, DateTime? DueAt = null, string? Comment = null);
public sealed record WorkflowInstanceDto(
    Guid Id, Guid DocumentId, Guid DefinitionId, WorkflowInstanceStatus Status, int CurrentStep,
    List<ApprovalTaskDto> Tasks, DateTime CreationTime);
public sealed record SigningQueueDocumentDto(Guid Id, string Number, string Title, string? Description, DocumentStatus Status,
    List<DocumentFileDto> Files, DateTime CreationTime, DocumentSourceType SourceType = DocumentSourceType.Workflow,
    Guid? FromUserId = null);
public sealed record SigningQueueItemDto(SigningQueueDocumentDto Document, ApprovalTaskDto Task, WorkflowInstanceDto Instance,
    WorkflowDefinitionDto Definition);
public sealed record WorkflowStepSignerSelection(string StepCode, Guid UserId);
public sealed record WorkflowViewScopeSelection(string StepCode, List<Guid> DepartmentIds, List<Guid> UserIds);
public sealed record WorkflowAssigneeCandidateDto(Guid UserId, string DisplayName, Guid? OrganizationUnitId = null,
    string? UserName = null);
public sealed record WorkflowStepCandidateGroupDto(string StepCode, string StepName, string AssigneeType, Guid? RoleId,
    List<WorkflowAssigneeCandidateDto> Candidates);
public sealed record StartWorkflowRequest(Guid? DocumentId, Guid DefinitionId, string IdempotencyKey,
    List<WorkflowStepSignerSelection>? Signers = null, List<WorkflowViewScopeSelection>? ViewScopes = null,
    bool UseTemplateFile = false, bool UseWorkflowTemplateFile = false, string? SigningContent = null);
public sealed record DecideApprovalTaskRequest(bool Approve, string? Comment, string IdempotencyKey, bool Return = false,
    Guid? SigningAttemptId = null, Guid? SigningFileId = null);
public sealed record ExtendWorkflowDueDateRequest(int AdditionalDays, string? Reason = null);

public sealed record SigningCredentialDto(Guid Id, int Kind, string ProviderCode, string Endpoint, string MaskedSecret,
    int ApiTimeoutSeconds, int SignWidth, int SignHeight, bool AllowElectronicSign, bool AllowDigitalSign,
    bool RequireOtp, DateTime UpdatedAt, bool HasLayoutImage = false);
public sealed record SigningProviderDefinitionDto(
    string Code,
    string DisplayName,
    List<SigningKind> SupportedKinds,
    string? DefaultEndpoint,
    bool RequiresLayoutImage,
    bool RequiresSealImage,
    bool RequiresBase64Secret,
    int DefaultApiTimeoutSeconds,
    int DefaultSignWidth,
    int DefaultSignHeight);
public sealed record ConfigureSigningCredentialRequest(int Kind, string Endpoint, string Secret,
    string ProviderCode = "", string? LayoutImageBase64 = null, int ApiTimeoutSeconds = 30,
    int SignWidth = 150, int SignHeight = 70, bool AllowElectronicSign = true,
    bool AllowDigitalSign = true, bool RequireOtp = false);
public sealed record SignDocumentRequest(Guid DocumentId, Guid FileId, int Kind, string IdempotencyKey,
    Guid? SignatureId = null, string? Placeholder = null, string? SignerName = null, string? Note = null);
public sealed record SigningAttemptDto(
    Guid Id, Guid DocumentId, Guid FileId, int Kind, SigningStatus Status, string InputSha256,
    string? OutputSha256, string? Error, DateTime CreationTime, DateTime? CompletedAt);
public sealed record SigningReportDto(Guid DocumentId, int Completed, int Failed, List<SigningAttemptDto> Attempts);
public sealed record UserSignatureDto(Guid Id, string FileName, string ContentType, long Size, bool IsDefault, DateTime CreationTime,
    UserSignatureType Type = UserSignatureType.Electronic, string ProviderCode = "", string TokenRef = "",
    DateTime? ValidFrom = null, DateTime? ValidTo = null, bool IsActive = true, bool HasSealImage = false);

public sealed record DocumentListQuery(string? Filter, string? Status, bool Mine, int SkipCount, int MaxResultCount, int? SourceType = null,
    Guid? DocumentTypeId = null, Guid? SectorId = null, Guid? UrgencyId = null, Guid? ConfidentialityId = null,
    DateTime? From = null, DateTime? To = null);
