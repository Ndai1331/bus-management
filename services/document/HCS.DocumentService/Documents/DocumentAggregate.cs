using HCS.DocumentService.Integration;

namespace HCS.DocumentService.Documents;

public sealed class DocumentAggregate
{
    private readonly List<DocumentFile> _files = [];
    private readonly List<DocumentAssignment> _assignments = [];
    private readonly List<DocumentHistory> _history = [];

    private DocumentAggregate() { }

    public DocumentAggregate(Guid id, string number, string title, string? description, Guid? actorUserId, DateTime now,
        DocumentSourceType sourceType = DocumentSourceType.Archive)
    {
        if (id == Guid.Empty) throw new ArgumentException("Document id is required.", nameof(id));
        Id = id;
        Number = Required(number, 64, nameof(number));
        Title = Required(title, 256, nameof(title));
        Description = Trim(description, 2000);
        Status = DocumentStatus.Draft;
        SourceType = sourceType;
        CreationTime = now;
        AddHistory("Created", actorUserId, null, now);
    }

    public Guid Id { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DocumentStatus Status { get; private set; }
    public DocumentSourceType SourceType { get; private set; }
    public Guid? ParentDocumentId { get; private set; }
    public Guid? FromUserId { get; private set; }
    public Guid? OrganizationUnitId { get; private set; }
    public Guid? DocumentTypeId { get; private set; }
    public Guid? SectorId { get; private set; }
    public Guid? UrgencyId { get; private set; }
    public Guid? ConfidentialityId { get; private set; }
    public DateTime CreationTime { get; private set; }
    public uint Version { get; private set; }
    public IReadOnlyCollection<DocumentFile> Files => _files;
    public IReadOnlyCollection<DocumentAssignment> Assignments => _assignments;
    public IReadOnlyCollection<DocumentHistory> History => _history;

    public void Update(string title, string? description, Guid? actorUserId, DateTime now)
    {
        EnsureMutable();
        Title = Required(title, 256, nameof(title));
        Description = Trim(description, 2000);
        AddHistory("Updated", actorUserId, null, now);
    }

    public void Classify(Guid? documentTypeId, Guid? sectorId, Guid? urgencyId, Guid? confidentialityId, Guid? actorUserId, DateTime now)
    {
        EnsureMutable();
        DocumentTypeId = documentTypeId;
        SectorId = sectorId;
        UrgencyId = urgencyId;
        ConfidentialityId = confidentialityId;
        AddHistory("Classified", actorUserId, null, now);
    }

    public DocumentFile AddFile(Guid id, string fileName, string contentType, long size, string sha256,
        string blobName, Guid? actorUserId, DateTime now)
    {
        EnsureMutable();
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        if (!Hashing.IsSha256(sha256)) throw new ArgumentException("A lowercase or uppercase SHA-256 hex digest is required.", nameof(sha256));
        if (_files.Any(x => x.Sha256.Equals(sha256, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The same file content is already attached.");
        var file = new DocumentFile(id, Id, Required(fileName, 256, nameof(fileName)),
            Required(contentType, 128, nameof(contentType)), size, sha256.ToLowerInvariant(), blobName, now);
        _files.Add(file);
        AddHistory("FileAdded", actorUserId, fileName, now);
        return file;
    }

    public DocumentAssignment Assign(Guid id, Guid userId, string responsibility, Guid? actorUserId, DateTime now, string? stepCode = null)
    {
        EnsureMutable();
        if (userId == Guid.Empty) throw new ArgumentException("Assignee is required.", nameof(userId));
        var role = Required(responsibility, 128, nameof(responsibility));
        var existing = _assignments.FirstOrDefault(x => x.AssigneeUserId == userId && x.Responsibility == role && x.StepCode == stepCode);
        if (existing is not null)
        {
            existing.Restore();
            return existing;
        }
        var assignment = new DocumentAssignment(id, Id, userId, role, now, stepCode);
        _assignments.Add(assignment);
        AddHistory("Assigned", actorUserId, $"{userId}:{role}", now);
        return assignment;
    }

    public void Send(Guid? receiverUserId, Guid? organizationUnitId, Guid fromUserId, DateTime now)
    {
        if (SourceType == DocumentSourceType.Workflow)
            throw new InvalidOperationException("Workflow documents cannot be sent as inbox items.");
        if (receiverUserId is null && organizationUnitId is null)
            throw new ArgumentException("A receiver or organization unit is required.");
        FromUserId = fromUserId;
        OrganizationUnitId = organizationUnitId;
        if (receiverUserId is { } userId)
            Assign(Guid.NewGuid(), userId, "VIEW", fromUserId, now);
        AddHistory("Sent", fromUserId, receiverUserId?.ToString() ?? organizationUnitId?.ToString(), now);
    }

    public void SetWorkflowSubmitter(Guid submitterUserId)
    {
        if (SourceType != DocumentSourceType.Workflow)
            throw new InvalidOperationException("Only workflow documents can have a workflow submitter.");
        if (submitterUserId == Guid.Empty)
            throw new ArgumentException("Submitter is required.", nameof(submitterUserId));

        FromUserId = submitterUserId;
    }

    public void RevokeInbox(Guid actorUserId, DateTime now)
    {
        foreach (var assignment in _assignments.Where(x => x.Responsibility == "VIEW" && x.StepCode == null && x.IsCurrent))
            assignment.Revoke();
        AddHistory("Revoked", actorUserId, null, now);
    }

    public DocumentAggregate DuplicateAsWorkflow(Guid newId, string number, Guid? actorUserId, DateTime now)
    {
        var copy = new DocumentAggregate(newId, number, Title, Description, actorUserId, now, DocumentSourceType.Workflow);
        copy.ParentDocumentId = Id;
        copy.DocumentTypeId = DocumentTypeId;
        copy.SectorId = SectorId;
        copy.UrgencyId = UrgencyId;
        copy.ConfidentialityId = ConfidentialityId;
        copy.AddHistory("DuplicatedForWorkflow", actorUserId, Id.ToString(), now);
        return copy;
    }


    public DocumentFile BeginFileDeletion(Guid fileId, Guid? actorUserId, DateTime now)
    {
        EnsureMutable();
        var file = _files.SingleOrDefault(x => x.Id == fileId) ?? throw new KeyNotFoundException("Document file not found.");
        if (!file.IsPendingDeletion)
        {
            file.MarkPendingDeletion();
            AddHistory("FileDeletionStarted", actorUserId, file.FileName, now);
        }
        return file;
    }

    public void CompleteFileDeletion(Guid fileId, Guid? actorUserId, DateTime now)
    {
        var file = _files.SingleOrDefault(x => x.Id == fileId) ?? throw new KeyNotFoundException("Document file not found.");
        if (!file.IsPendingDeletion) throw new InvalidOperationException("File deletion was not started.");
        _files.Remove(file);
        AddHistory("FileRemoved", actorUserId, file.FileName, now);
    }

    public void Submit(Guid? actorUserId, DateTime now)
    {
        if (Status != DocumentStatus.Draft) throw new InvalidOperationException("Only draft documents can be submitted.");
        if (_files.Count == 0) throw new InvalidOperationException("A document must have at least one file before submission.");
        Status = DocumentStatus.Submitted;
        AddHistory("Submitted", actorUserId, null, now);
    }

    public void StartReview(Guid? actorUserId, DateTime now, string? detail = null)
    {
        if (Status is not (DocumentStatus.Submitted or DocumentStatus.InReview))
            throw new InvalidOperationException("The document is not ready for review.");
        Status = DocumentStatus.InReview;
        AddHistory("ReviewStarted", actorUserId, Trim(detail, 2000), now);
    }

    public void CompleteReview(bool approved, Guid? actorUserId, string? detail, DateTime now)
    {
        if (Status != DocumentStatus.InReview) throw new InvalidOperationException("The document is not in review.");
        Status = approved ? DocumentStatus.Approved : DocumentStatus.Rejected;
        AddHistory(approved ? "Approved" : "Rejected", actorUserId, detail, now);
    }

    private void EnsureMutable()
    {
        if (Status is DocumentStatus.Approved or DocumentStatus.Archived)
            throw new InvalidOperationException("Approved or archived documents are immutable.");
    }

    private void AddHistory(string action, Guid? actorUserId, string? detail, DateTime now) =>
        _history.Add(new DocumentHistory(Guid.NewGuid(), Id, action, actorUserId, detail, now));

    private static string Required(string value, int max, string parameter)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result)) throw new ArgumentException("Value is required.", parameter);
        if (result.Length > max) throw new ArgumentOutOfRangeException(parameter);
        return result;
    }

    private static string? Trim(string? value, int max)
    {
        var result = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (result?.Length > max) throw new ArgumentOutOfRangeException(nameof(value));
        return result;
    }
}

public sealed class DocumentFile
{
    private DocumentFile() { }
    internal DocumentFile(Guid id, Guid documentId, string fileName, string contentType, long size, string sha256, string blobName, DateTime now)
        => (Id, DocumentId, FileName, ContentType, Size, Sha256, BlobName, CreationTime) = (id, documentId, fileName, contentType, size, sha256, blobName, now);
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string BlobName { get; private set; } = string.Empty;
    public DateTime CreationTime { get; private set; }
    public bool IsPendingDeletion { get; private set; }
    public Guid? PairedFileId { get; private set; }
    internal void MarkPendingDeletion() => IsPendingDeletion = true;
    internal void SetPairedFileId(Guid pairedFileId) => PairedFileId = pairedFileId;
    internal void ReplaceContent(long size, string sha256, string blobName)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        if (!Hashing.IsSha256(sha256)) throw new ArgumentException("A SHA-256 hex digest is required.", nameof(sha256));
        Size = size;
        Sha256 = sha256.ToLowerInvariant();
        BlobName = blobName;
    }
}

public sealed class DocumentAssignment
{
    private DocumentAssignment() { }
    internal DocumentAssignment(Guid id, Guid documentId, Guid userId, string responsibility, DateTime now, string? stepCode = null)
        => (Id, DocumentId, AssigneeUserId, Responsibility, AssignedAt, IsCurrent, StepCode) = (id, documentId, userId, responsibility, now, true, stepCode);
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid AssigneeUserId { get; private set; }
    public string Responsibility { get; private set; } = string.Empty;
    public DateTime AssignedAt { get; private set; }
    public bool IsCurrent { get; private set; } = true;
    public string? StepCode { get; private set; }
    internal void Revoke() => IsCurrent = false;
    internal void Restore() => IsCurrent = true;
}

public sealed class DocumentHistory
{
    private DocumentHistory() { }
    internal DocumentHistory(Guid id, Guid documentId, string action, Guid? actorUserId, string? detail, DateTime now)
        => (Id, DocumentId, Action, ActorUserId, Detail, OccurredAt) = (id, documentId, action, actorUserId, detail, now);
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public Guid? ActorUserId { get; private set; }
    public string? Detail { get; private set; }
    public DateTime OccurredAt { get; private set; }
}

internal static class Hashing
{
    public static bool IsSha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
