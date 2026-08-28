namespace HCS.DocumentService.Documents;

public enum DocumentStatus { Draft, Submitted, InReview, Approved, Rejected, Archived }

public enum DocumentSourceType { Archive = 0, Personal = 1, SentToMe = 2, Workflow = 3 }

public static class DocumentPermissions
{
    public const string View = "Documents.View";
    public const string Create = "Documents.Create";
    public const string Update = "Documents.Update";
    public const string Assign = "Documents.Assign";
    public const string ManageFiles = "Documents.ManageFiles";
    public const string WorkflowView = "Documents.Workflow.View";
    public const string WorkflowManage = "Documents.Workflow.Manage";
    public const string WorkflowStart = "Documents.Workflow.Start";
    public const string WorkflowDecide = "Documents.Workflow.Decide";
    public const string SigningConfigure = "Documents.Signing.Configure";
    public const string SigningExecute = "Documents.Signing.Execute";
    public const string SigningReport = "Documents.Signing.Report";
}

public sealed record CreateDocumentRequest(string? Number, string Title, string? Description,
    Guid? DocumentTypeId = null, Guid? SectorId = null, Guid? UrgencyId = null, Guid? ConfidentialityId = null,
    DocumentSourceType SourceType = DocumentSourceType.Archive);
public sealed record SendDocumentRequest(Guid? ReceiverUserId = null, Guid? OrganizationUnitId = null);
public sealed record UpdateDocumentRequest(string Title, string? Description,
    Guid? DocumentTypeId = null, Guid? SectorId = null, Guid? UrgencyId = null, Guid? ConfidentialityId = null);
public sealed record AddDocumentFileRequest(string FileName, string ContentType, long Size, string Sha256);
public sealed record AssignDocumentRequest(Guid AssigneeUserId, string Responsibility);
public sealed record DocumentFileDto(Guid Id, string FileName, string ContentType, long Size, string Sha256, DateTime CreationTime, Guid? PairedFileId = null);
public sealed record DocumentAssignmentDto(Guid Id, Guid AssigneeUserId, string Responsibility, DateTime AssignedAt,
    bool IsCurrent = true, string? StepCode = null);
public sealed record DocumentHistoryDto(Guid Id, string Action, Guid? ActorUserId, string? Detail, DateTime OccurredAt);
public sealed record DocumentDto(Guid Id, string Number, string Title, string? Description, DocumentStatus Status,
    Guid? DocumentTypeId, Guid? SectorId, Guid? UrgencyId, Guid? ConfidentialityId,
    IReadOnlyList<DocumentFileDto> Files, IReadOnlyList<DocumentAssignmentDto> Assignments,
    IReadOnlyList<DocumentHistoryDto> History, DateTime CreationTime,
    DocumentSourceType SourceType = DocumentSourceType.Archive, Guid? ParentDocumentId = null,
    Guid? FromUserId = null, Guid? OrganizationUnitId = null);
public sealed record PagedDocumentsDto(long TotalCount, IReadOnlyList<DocumentDto> Items);

public interface IDocumentAppService
{
    Task<PagedDocumentsDto> GetListAsync(string? filter = null, DocumentStatus? status = null,
        bool mine = false, int skip = 0, int take = 50, int? sourceType = null,
        Guid? documentTypeId = null, Guid? sectorId = null, Guid? urgencyId = null, Guid? confidentialityId = null,
        DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<DocumentDto> CreateAsync(CreateDocumentRequest input, CancellationToken cancellationToken = default);
    Task<DocumentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DocumentDto> UpdateAsync(Guid id, UpdateDocumentRequest input, CancellationToken cancellationToken = default);
    Task<DocumentDto> AssignAsync(Guid id, AssignDocumentRequest input, CancellationToken cancellationToken = default);
    Task<DocumentDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DocumentDto> SendAsync(Guid id, SendDocumentRequest input, CancellationToken cancellationToken = default);
    Task<DocumentDto> RevokeAsync(Guid id, CancellationToken cancellationToken = default);
}
