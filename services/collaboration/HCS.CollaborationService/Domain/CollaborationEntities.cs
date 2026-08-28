using HCS.CollaborationService.Contracts;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;

namespace HCS.CollaborationService.Domain;

public static class ConversationAccessRules
{
    public static void DemandExactlyTwoDirectUsers(IEnumerable<Guid> userIds)
    {
        if (userIds.Distinct().Count() != 2)
            throw new BusinessException("Collaboration:DirectConversationRequiresExactlyTwoUsers");
    }

    public static void DemandWorkMembership(Guid callerUserId, IEnumerable<Guid> allowedUserIds,
        IEnumerable<Guid> requestedUserIds)
    {
        var allowed = allowedUserIds.ToHashSet();
        if (!allowed.Contains(callerUserId) || requestedUserIds.Any(x => !allowed.Contains(x)))
            throw new Volo.Abp.Authorization.AbpAuthorizationException("Work subject membership required.");
    }
}

public sealed class Conversation : FullAuditedAggregateRoot<Guid>
{
    public ConversationType Type { get; private set; }
    public string? Name { get; private set; }
    public string? Description { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Guid? TaskId { get; private set; }
    // A deterministic pair makes direct-message creation safe across replicas.
    public Guid? DirectUserLowId { get; private set; }
    public Guid? DirectUserHighId { get; private set; }
    public string? LastMessage { get; private set; }
    public DateTime? LastMessageAt { get; private set; }
    public ICollection<ConversationMember> Members { get; private set; } = [];

    private Conversation() { }
    public Conversation(Guid id, ConversationType type, string? name, string? description,
        Guid? projectId = null, Guid? taskId = null, Guid? directUserOne = null, Guid? directUserTwo = null) : base(id)
    {
        Type = type;
        Name = name?.Trim();
        Description = description?.Trim();
        ProjectId = projectId;
        TaskId = taskId;
        if (type == ConversationType.User && directUserOne.HasValue && directUserTwo.HasValue)
            (DirectUserLowId, DirectUserHighId) = directUserOne.Value.CompareTo(directUserTwo.Value) < 0
                ? (directUserOne, directUserTwo) : (directUserTwo, directUserOne);
        if (type == ConversationType.User && (projectId is not null || taskId is not null))
            throw new BusinessException("Collaboration:InvalidUserConversationShape");
        if (type == ConversationType.User && (!DirectUserLowId.HasValue || !DirectUserHighId.HasValue || DirectUserLowId == DirectUserHighId))
            throw new BusinessException("Collaboration:DirectConversationRequiresExactlyTwoUsers");
        if (type == ConversationType.Group && (projectId is not null || taskId is not null))
            throw new BusinessException("Collaboration:InvalidGroupConversationShape");
        if (type == ConversationType.Project && (projectId is null || taskId is not null))
            throw new BusinessException("Collaboration:ProjectIdRequired");
        if (type == ConversationType.Task && (taskId is null || projectId is not null))
            throw new BusinessException("Collaboration:TaskIdRequired");
    }

    public void Rename(string name) => Name = Check.NotNullOrWhiteSpace(name, nameof(name), 256);
    public void SetLastMessage(string text, DateTime at)
    {
        LastMessage = text.Length <= 512 ? text : text[..512];
        LastMessageAt = at;
    }
}

public sealed class ConversationMember : CreationAuditedEntity<Guid>
{
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public ConversationMemberRole Role { get; private set; }
    public int UnreadCount { get; private set; }
    public bool IsPinned { get; private set; }
    public DateTime? LastReadAt { get; private set; }

    private ConversationMember() { }
    public ConversationMember(Guid id, Guid conversationId, Guid userId, ConversationMemberRole role) : base(id)
    { ConversationId = conversationId; UserId = userId; Role = role; }
    public void SetRole(ConversationMemberRole role) => Role = role;
    public void IncrementUnread() => UnreadCount++;
    public void MarkRead(DateTime at) { UnreadCount = 0; LastReadAt = at; }
    public void SetPinned(bool value) => IsPinned = value;
}

public sealed class ChatMessage : CreationAuditedAggregateRoot<Guid>
{
    public Guid ConversationId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public Guid? ClientMessageId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public Guid? ReplyToMessageId { get; private set; }
    public Guid? ForwardedFromMessageId { get; private set; }
    public bool IsPinned { get; private set; }
    public DateTime? PinnedAt { get; private set; }
    public Guid? PinnedByUserId { get; private set; }
    public bool IsDeleted { get; private set; }
    public ICollection<MessageAttachment> Attachments { get; private set; } = [];

    private ChatMessage() { }
    public ChatMessage(Guid id, Guid conversationId, Guid senderUserId, string text,
        Guid? clientMessageId = null, Guid? replyToMessageId = null, Guid? forwardedFromMessageId = null) : base(id)
    {
        ConversationId = conversationId;
        SenderUserId = senderUserId;
        Text = Check.Length(text ?? string.Empty, nameof(text), maxLength: 4000) ?? string.Empty;
        ClientMessageId = clientMessageId;
        ReplyToMessageId = replyToMessageId;
        ForwardedFromMessageId = forwardedFromMessageId;
    }
    public void Pin(Guid userId, DateTime at) { IsPinned = true; PinnedByUserId = userId; PinnedAt = at; }
    public void Unpin() { IsPinned = false; PinnedByUserId = null; PinnedAt = null; }
    public void SoftDeleteContent() { IsDeleted = true; Text = ""; }
}

public sealed class MessageAttachment : CreationAuditedEntity<Guid>
{
    public Guid ConversationId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public Guid? MessageId { get; private set; }
    public string BlobName { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public AttachmentKind Kind { get; private set; }

    private MessageAttachment() { }
    public MessageAttachment(Guid id, Guid conversationId, Guid userId, string blobName,
        string fileName, string contentType, long size, AttachmentKind kind) : base(id)
    {
        ConversationId = conversationId; UploadedByUserId = userId; BlobName = blobName;
        FileName = fileName; ContentType = contentType; Size = size; Kind = kind;
    }
    public void AttachTo(Guid messageId)
    {
        if (MessageId.HasValue) throw new BusinessException("Collaboration:AttachmentAlreadyUsed");
        MessageId = messageId;
    }
}

public sealed class Notification : CreationAuditedAggregateRoot<Guid>
{
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? Link { get; private set; }
    public NotificationStatus Status { get; private set; }
    private Notification() { }
    public Notification(Guid id, string title, string body, string? link, DateTime? creationTimeUtc = null) : base(id)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), 256);
        Body = Check.NotNullOrWhiteSpace(body, nameof(body), 2000);
        Link = link;
        // Set explicitly: background event handlers may save without ABP audit property setters.
        CreationTime = NotificationTimes.ToUtc(creationTimeUtc ?? DateTime.UtcNow);
    }
    public void MarkDelivered() => Status = NotificationStatus.Delivered;
    public void MarkFailed() => Status = NotificationStatus.Failed;
}

public sealed class NotificationReceiver : CreationAuditedEntity<Guid>
{
    public Guid NotificationId { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }
    private NotificationReceiver() { }
    public NotificationReceiver(Guid id, Guid notificationId, Guid userId, DateTime? creationTimeUtc = null) : base(id)
    {
        NotificationId = notificationId;
        UserId = userId;
        CreationTime = NotificationTimes.ToUtc(creationTimeUtc ?? DateTime.UtcNow);
    }
    public void MarkRead(DateTime at) { IsRead = true; ReadAt = at; }
}

internal static class NotificationTimes
{
    private static readonly DateTime ValidAfter = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static DateTime ToUtc(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return utc < ValidAfter ? DateTime.UtcNow : utc;
    }
}

public sealed class PushDeviceToken : CreationAuditedAggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string Platform { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    private PushDeviceToken() { }
    public PushDeviceToken(Guid id, Guid userId, string token, string platform) : base(id)
    { UserId = userId; Token = Check.NotNullOrWhiteSpace(token, nameof(token), 2048); Platform = platform; }
    public void Deactivate() => IsActive = false;
    public void AssignTo(Guid userId, string platform) { UserId = userId; Platform = platform; IsActive = true; }
}

public sealed class InboxMessage : Entity<Guid>
{
    public string EventName { get; private set; } = string.Empty;
    public DateTime ProcessedAt { get; private set; }
    private InboxMessage() { }
    public InboxMessage(Guid id, string eventName, DateTime processedAt) : base(id)
    { EventName = eventName; ProcessedAt = processedAt; }
}

public sealed class OutboxMessage : Entity<Guid>
{
    public string EventName { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public int Attempts { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public DateTime? DeadLetteredAt { get; private set; }
    public string? LastError { get; private set; }
    private OutboxMessage() { }
    public OutboxMessage(Guid id, string eventName, string payload, DateTime occurredAt) : base(id)
    { EventName = eventName; Payload = payload; OccurredAt = occurredAt; }
    public void Lease(Guid leaseId, DateTime until) { LeaseId = leaseId; LeaseUntil = until; }
    public void RecordAttempt(bool succeeded, DateTime at, string? error = null)
    {
        Attempts++; LeaseId = null; LeaseUntil = null;
        if (succeeded) { PublishedAt = at; LastError = null; }
        else
        {
            LastError = error is null ? null : error[..Math.Min(error.Length, 1000)];
            if (Attempts >= 10) DeadLetteredAt = at;
        }
    }
    public void DeadLetter(DateTime at, string error)
    { DeadLetteredAt = at; LastError = error[..Math.Min(error.Length, 1000)]; LeaseId = null; LeaseUntil = null; }
}

public sealed class PushDelivery : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? Link { get; private set; }
    public int Attempts { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public DateTime? DeadLetteredAt { get; private set; }
    public string? LastError { get; private set; }
    private PushDelivery() { }
    public PushDelivery(Guid id, Guid userId, string title, string body, string? link, DateTime nextAttemptAt) : base(id)
    { UserId = userId; Title = title; Body = body; Link = link; NextAttemptAt = nextAttemptAt; }
    public void ScheduleRetry(DateTime at, int maxAttempts, string? error)
    {
        Attempts++; NextAttemptAt = at; LastError = error?[..Math.Min(error.Length, 1000)];
        if (Attempts >= maxAttempts) DeadLetteredAt = DateTime.UtcNow;
    }
    public void Complete(DateTime at) { Attempts++; DeliveredAt = at; }
    public void Lease(Guid leaseId, DateTime until) { LeaseId = leaseId; LeaseUntil = until; }
    public void ReleaseLease() { LeaseId = null; LeaseUntil = null; }
}

public sealed class WorkSubjectProjection : Entity<Guid>
{
    private WorkSubjectProjection() { }
    public WorkSubjectProjection(Guid id, string subjectType, Guid projectId, Guid? taskId, DateTimeOffset occurredAtUtc,
        bool isDeleted = false)
        : base(id) => (SubjectType, ProjectId, TaskId, LastOccurredAtUtc, IsDeleted) =
            (subjectType, projectId, taskId, occurredAtUtc, isDeleted);

    public static WorkSubjectProjection CreateDeleted(Guid id, string subjectType, Guid projectId, Guid? taskId,
        DateTimeOffset occurredAtUtc) => new(id, subjectType, projectId, taskId, occurredAtUtc, isDeleted: true);
    public string SubjectType { get; private set; } = string.Empty;
    public Guid ProjectId { get; private set; }
    public Guid? TaskId { get; private set; }
    public DateTimeOffset LastOccurredAtUtc { get; private set; }
    public bool IsDeleted { get; private set; }
    public ICollection<WorkSubjectMemberProjection> Members { get; private set; } = [];
    public bool TryAdvance(DateTimeOffset occurredAtUtc)
    {
        if (occurredAtUtc <= LastOccurredAtUtc) return false;
        LastOccurredAtUtc = occurredAtUtc; return true;
    }
    public bool MarkDeleted(DateTimeOffset occurredAtUtc)
    {
        if (occurredAtUtc <= LastOccurredAtUtc) return false;
        LastOccurredAtUtc = occurredAtUtc;
        IsDeleted = true;
        return true;
    }
    public bool Restore(DateTimeOffset occurredAtUtc)
    {
        if (occurredAtUtc <= LastOccurredAtUtc) return false;
        LastOccurredAtUtc = occurredAtUtc;
        IsDeleted = false;
        return true;
    }
}

public sealed class WorkSubjectMemberProjection : Entity<Guid>
{
    private WorkSubjectMemberProjection() { }
    public WorkSubjectMemberProjection(Guid id, Guid subjectId, Guid userId) : base(id)
        => (SubjectId, UserId) = (subjectId, userId);
    public Guid SubjectId { get; private set; }
    public Guid UserId { get; private set; }
}
