using System.ComponentModel.DataAnnotations;

namespace HCS.CollaborationService.Contracts;

public enum ConversationType { User = 0, Group = 1, Project = 2, Task = 3 }
public enum ConversationMemberRole { Member = 0, Admin = 1 }
public enum AttachmentKind { File = 0, Image = 1, Video = 2, Audio = 3 }
public enum NotificationStatus { Pending = 0, Delivered = 1, Failed = 2 }

public static class CollaborationPermissions
{
    public const string Chat = "Collaboration.Chat";
    public const string Notifications = "Collaboration.Notifications";
    public const string Administration = "Collaboration.Administration";
}

public sealed record ConversationDto(Guid Id, ConversationType Type, string? Name, string? Description,
    Guid? ProjectId, Guid? TaskId, string? LastMessage, DateTime? LastMessageAt,
    int UnreadCount, bool IsPinned, IReadOnlyList<ConversationMemberDto> Members);

public sealed record ConversationMemberDto(Guid UserId, ConversationMemberRole Role, DateTime JoinedAt);
public sealed record PresenceChangedDto(Guid UserId, bool IsOnline);
public sealed record ChatContactDto(
    Guid Id,
    string UserName,
    string DisplayName,
    bool IsActive,
    string? Surname = null,
    string? Name = null,
    string? PhoneNumber = null,
    string? AvatarUrl = null);
public sealed record ConversationPermissionDto(bool CanSend, bool CanManageMembers, bool CanRename, bool CanLeave,
    bool CanModerateMessages = false);

public sealed class CreateConversationInput
{
    public ConversationType Type { get; init; }
    [StringLength(256)] public string? Name { get; init; }
    [StringLength(1024)] public string? Description { get; init; }
    public Guid? TargetUserId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? TaskId { get; init; }
    public IReadOnlyCollection<Guid> MemberUserIds { get; init; } = [];
}

public sealed class SendMessageInput
{
    public Guid ConversationId { get; init; }
    [StringLength(4000)] public string Text { get; init; } = string.Empty;
    public Guid? ClientMessageId { get; init; }
    public Guid? ReplyToMessageId { get; init; }
    public IReadOnlyCollection<Guid> AttachmentIds { get; init; } = [];
}

public sealed record MessageAttachmentDto(Guid Id, string FileName, string ContentType, long Size,
    AttachmentKind Kind, Guid? MessageId);

public sealed record ChatMessagePreviewDto(Guid Id, Guid SenderUserId, string Text, bool IsDeleted);

public sealed record ChatMessageDto(Guid Id, Guid ConversationId, Guid SenderUserId, string Text,
    DateTime CreatedAt, Guid? ReplyToMessageId, Guid? ForwardedFromMessageId, bool IsPinned,
    bool IsDeleted, IReadOnlyList<MessageAttachmentDto> Attachments,
    ChatMessagePreviewDto? ReplyTo = null, ChatMessagePreviewDto? ForwardedFrom = null);

public sealed record MessageContextDto(ChatMessageDto Target, IReadOnlyList<ChatMessageDto> Before,
    IReadOnlyList<ChatMessageDto> After, bool HasMoreBefore = false, bool HasMoreAfter = false);

public static class ChatModerationRules
{
    public const string ForwardedPlaceholder = "📤";

    public static bool IsSystemAdmin(bool isAdmin, bool isBdAdmin) => isAdmin || isBdAdmin;

    public static bool CanDeleteMessage(
        Guid currentUserId,
        Guid senderUserId,
        bool isSystemAdmin,
        ConversationMemberRole memberRole) =>
        senderUserId == currentUserId
        || isSystemAdmin
        || memberRole == ConversationMemberRole.Admin;

    public static string ForwardBody(string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? ForwardedPlaceholder : comment.Trim();
}

public sealed record PagedMessagesDto(long TotalCount, IReadOnlyList<ChatMessageDto> Items);

public sealed record UploadAttachmentResult(Guid Id, string FileName, string ContentType, long Size,
    AttachmentKind Kind);

public sealed record AuthorizedDownload(string FileName, string ContentType, Stream Content);

public sealed record NotificationDto(Guid Id, Guid UserId, string Title, string Body, string? Link,
    bool IsRead, DateTime CreatedAt);

public static class ChatNotificationRules
{
    public static bool IsChatLink(string? link) =>
        !string.IsNullOrWhiteSpace(link)
        && (link.Equals("/chat", StringComparison.OrdinalIgnoreCase)
            || link.StartsWith("/chat/", StringComparison.OrdinalIgnoreCase)
            || link.StartsWith("/chat1/", StringComparison.OrdinalIgnoreCase));
}

public static class UserDisplayNames
{
    public static string FromPerson(string? surname, string? givenName, string? userName, string? jwtFullName = null)
    {
        var full = string.Join(' ', new[] { surname, givenName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
        return FirstReal(full, jwtFullName, userName);
    }

    public static string FirstReal(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var value = candidate?.Trim();
            if (!string.IsNullOrWhiteSpace(value) && !IsPlaceholder(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    public static bool IsPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || string.Equals(value.Trim(), "User", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value.Trim(), "HCS", StringComparison.OrdinalIgnoreCase);
}

public static class NotificationLocalization
{
    public const string ChatTitle = "Notification:ChatNewMessage";
    public const string ChatBody = "Notification:ChatNewMessageBody";
    public const string ChatBodyUnknown = "Notification:ChatNewMessageBodyUnknown";
    public const string TaskAssignedTitle = "Notification:TaskAssigned";
    public const string TaskAssignedBody = "Notification:TaskAssignedBody";
    public const string ProjectAssignedTitle = "Notification:ProjectAssigned";
    public const string ProjectAssignedBody = "Notification:ProjectAssignedBody";
    public const string GenericTitle = "Notification:Generic";
    public const string GenericBody = "Notification:GenericBody";

    private const char Separator = '\u001f';

    public static string Encode(string key, params string[] args) =>
        args.Length == 0 ? key : string.Join(Separator, new[] { key }.Concat(args));

    public static string Format(
        string stored,
        Func<string, string> localizeKey,
        Func<string, object[], string> localizeKeyed)
    {
        if (string.IsNullOrWhiteSpace(stored)
            || !stored.StartsWith("Notification:", StringComparison.Ordinal))
        {
            return stored;
        }

        var parts = stored.Split(Separator);
        return parts.Length == 1
            ? localizeKey(parts[0])
            : localizeKeyed(parts[0], parts.Skip(1).Cast<object>().ToArray());
    }

    public static string Format(string stored, string culture)
    {
        var vietnamese = culture.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
        return Format(
            stored,
            key => Template(key, vietnamese),
            (key, args) => string.Format(Template(key, vietnamese), args));
    }

    private static string Template(string key, bool vietnamese) => (key, vietnamese) switch
    {
        (ChatTitle, true) => "Bạn có tin nhắn mới",
        (ChatTitle, false) => "You have a new message",
        (ChatBody, true) => "1 tin nhắn mới từ {0}",
        (ChatBody, false) => "1 new message from {0}",
        (ChatBodyUnknown, true) => "Bạn có tin nhắn mới",
        (ChatBodyUnknown, false) => "You have a new message",
        (TaskAssignedTitle, true) => "Có công việc mới",
        (TaskAssignedTitle, false) => "New task assigned",
        (TaskAssignedBody, true) => "Bạn được gán vào công việc {0}",
        (TaskAssignedBody, false) => "You were assigned to task {0}",
        (ProjectAssignedTitle, true) => "Có dự án mới",
        (ProjectAssignedTitle, false) => "New project assigned",
        (ProjectAssignedBody, true) => "Bạn được gán vào dự án {0}",
        (ProjectAssignedBody, false) => "You were added to project {0}",
        (GenericTitle, true) => "Thông báo",
        (GenericTitle, false) => "Notification",
        (GenericBody, true) => "Thông báo cho {0}",
        (GenericBody, false) => "Notification for {0}",
        _ => key
    };
}

public sealed class CreateNotificationInput
{
    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
    [Required, StringLength(256)] public string Title { get; init; } = string.Empty;
    [Required, StringLength(2000)] public string Body { get; init; } = string.Empty;
    [StringLength(1024)] public string? Link { get; init; }
}

public sealed record RegisterPushDeviceInput([property: Required, StringLength(2048)] string Token,
    [property: StringLength(32)] string Platform);

public sealed record TaskFromMessageRequestedEto(Guid EventId, Guid MessageId, Guid ConversationId,
    Guid RequestedByUserId, string Title, string? Description, DateTime OccurredAt);
