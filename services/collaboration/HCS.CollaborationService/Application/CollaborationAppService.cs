using System.Text.Json;
using HCS.CollaborationService.Contracts;
using HCS.CollaborationService.Data;
using HCS.CollaborationService.Domain;
using HCS.IntegrationEvents.Collaboration;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace HCS.CollaborationService.Application;

public class CollaborationAppService(
    CollaborationDbContext db,
    ICurrentUser currentUser,
    IGuidGenerator guidGenerator,
    IClock clock,
    IChatRealtimeNotifier notifier,
    ILogger<CollaborationAppService> logger) : ApplicationService
{
    private Guid UserId => currentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");

    public async Task<ConversationDto> CreateConversationAsync(CreateConversationInput input, CancellationToken ct = default)
    {
        var me = UserId;
        if (input.Type == ConversationType.User && input.TargetUserId is null)
            throw new BusinessException("Collaboration:TargetUserRequired");
        if (input.Type == ConversationType.User && input.TargetUserId == me)
            throw new BusinessException("Collaboration:DirectConversationRequiresTwoUsers");

        Conversation? existing = null;
        if (input.Type == ConversationType.Project)
            existing = await db.Conversations.Include(x => x.Members).FirstOrDefaultAsync(x => x.Type == ConversationType.Project && x.ProjectId == input.ProjectId, ct);
        else if (input.Type == ConversationType.Task)
            existing = await db.Conversations.Include(x => x.Members).FirstOrDefaultAsync(x => x.Type == ConversationType.Task && x.TaskId == input.TaskId, ct);
        else if (input.Type == ConversationType.User)
        {
            var target = input.TargetUserId!.Value;
            var (low, high) = me.CompareTo(target) < 0 ? (me, target) : (target, me);
            existing = await db.Conversations.Include(x => x.Members)
                .FirstOrDefaultAsync(x => x.Type == ConversationType.User && x.DirectUserLowId == low && x.DirectUserHighId == high, ct);
        }
        if (existing is not null)
        {
            if (existing.Members.All(x => x.UserId != me))
                throw new AbpAuthorizationException("Conversation membership required.");
            return await ToConversationDto(existing, me, ct);
        }

        var requestedMembers = input.MemberUserIds.Take(101).ToArray();
        if (requestedMembers.Length > 100) throw new BusinessException("Collaboration:TooManyMembers");
        var memberIds = requestedMembers.Append(me)
            .Concat(input.TargetUserId.HasValue ? [input.TargetUserId.Value] : [])
            .Distinct().ToArray();
        if (input.Type == ConversationType.User && (memberIds.Length != 2 ||
                input.MemberUserIds.Any(x => x != me && x != input.TargetUserId)))
            throw new BusinessException("Collaboration:DirectConversationRequiresExactlyTwoUsers");
        if (input.Type == ConversationType.User) ConversationAccessRules.DemandExactlyTwoDirectUsers(memberIds);
        if (input.Type != ConversationType.User && memberIds.Length < 1)
            throw new BusinessException("Collaboration:MemberRequired");
        if (input.Type is ConversationType.Project or ConversationType.Task)
            await DemandWorkSubjectAccess(input.Type, input.ProjectId, input.TaskId, memberIds, ct);
        var conversation = new Conversation(guidGenerator.Create(), input.Type, input.Name, input.Description,
            input.ProjectId, input.TaskId,
            input.Type == ConversationType.User ? memberIds[0] : null,
            input.Type == ConversationType.User ? memberIds[1] : null);
        foreach (var userId in memberIds)
            conversation.Members.Add(new ConversationMember(guidGenerator.Create(), conversation.Id, userId,
                userId == me ? ConversationMemberRole.Admin : ConversationMemberRole.Member));
        db.Conversations.Add(conversation);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception) &&
            input.Type is ConversationType.User or ConversationType.Project)
        {
            db.ChangeTracker.Clear();
            Conversation concurrent;
            if (input.Type == ConversationType.Project)
            {
                concurrent = await db.Conversations.Include(x => x.Members).SingleAsync(x =>
                    x.Type == ConversationType.Project && x.ProjectId == input.ProjectId, ct);
            }
            else
            {
                var target = input.TargetUserId!.Value;
                var (low, high) = me.CompareTo(target) < 0 ? (me, target) : (target, me);
                concurrent = await db.Conversations.Include(x => x.Members).SingleAsync(x =>
                    x.Type == ConversationType.User && x.DirectUserLowId == low && x.DirectUserHighId == high, ct);
            }
            if (concurrent.Members.All(x => x.UserId != me))
                throw new AbpAuthorizationException("Conversation membership required.");
            return MapConversation(concurrent, me);
        }
        return await ToConversationDto(conversation, me, ct);
    }

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(ConversationType? type = null, bool pinnedOnly = false, CancellationToken ct = default)
    {
        var me = UserId;
        var query = db.Conversations.AsNoTracking().Include(x => x.Members)
            .Where(x => x.Members.Any(m => m.UserId == me));
        if (type.HasValue) query = query.Where(x => x.Type == type);
        if (pinnedOnly) query = query.Where(x => x.Members.Any(m => m.UserId == me && m.IsPinned));
        var items = await query.OrderByDescending(x => x.LastMessageAt).ToListAsync(ct);
        return items.Select(x => MapConversation(x, me)).ToArray();
    }

    public async Task<ConversationDto> GetConversationAsync(Guid id, CancellationToken ct = default)
    {
        var conversation = await RequireMember(id, UserId, ct);
        return MapConversation(conversation, UserId);
    }

    public async Task<ConversationDto?> FindConversationByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        var me = UserId;
        var conversation = await db.Conversations.AsNoTracking().Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Type == ConversationType.Project && x.ProjectId == projectId, ct);
        if (conversation is null || conversation.Members.All(x => x.UserId != me))
            return null;
        return MapConversation(conversation, me);
    }

    public async Task RenameAsync(Guid id, string name, CancellationToken ct = default)
    {
        var conversation = await RequireAdmin(id, UserId, ct); conversation.Rename(name); await db.SaveChangesAsync(ct);
    }

    public async Task SetConversationPinnedAsync(Guid id, bool pinned, CancellationToken ct = default)
    {
        var me = UserId;
        var member = await db.ConversationMembers.SingleOrDefaultAsync(x => x.ConversationId == id && x.UserId == me, ct)
            ?? throw new AbpAuthorizationException();
        member.SetPinned(pinned); await db.SaveChangesAsync(ct);
    }

    public async Task AddMembersAsync(Guid id, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count > 100) throw new BusinessException("Collaboration:TooManyMembers");
        var conversation = await RequireAdmin(id, UserId, ct);
        if (conversation.Type == ConversationType.User)
            throw new BusinessException("Collaboration:DirectConversationMembershipIsFixed");
        if (conversation.Type is ConversationType.Project or ConversationType.Task)
            await DemandWorkSubjectAccess(conversation.Type, conversation.ProjectId, conversation.TaskId, userIds, ct);
        var existing = conversation.Members.Select(x => x.UserId).ToHashSet();
        foreach (var userId in userIds.Where(x => !existing.Contains(x)))
            conversation.Members.Add(new ConversationMember(guidGenerator.Create(), id, userId, ConversationMemberRole.Member));
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var conversation = await RequireAdmin(id, UserId, ct);
        var member = conversation.Members.SingleOrDefault(x => x.UserId == userId)
            ?? throw new BusinessException("Collaboration:MemberNotFound");
        if (member.Role == ConversationMemberRole.Admin && conversation.Members.Count(x => x.Role == ConversationMemberRole.Admin) == 1)
            throw new BusinessException("Collaboration:LastAdminCannotLeave");
        db.ConversationMembers.Remove(member); await db.SaveChangesAsync(ct);
    }

    public async Task SetMemberRoleAsync(Guid id, Guid userId, ConversationMemberRole role, CancellationToken ct = default)
    {
        var conversation = await RequireAdmin(id, UserId, ct);
        var member = conversation.Members.SingleOrDefault(x => x.UserId == userId)
            ?? throw new BusinessException("Collaboration:MemberNotFound");
        if (member.Role == ConversationMemberRole.Admin && role != ConversationMemberRole.Admin && conversation.Members.Count(x => x.Role == ConversationMemberRole.Admin) == 1)
            throw new BusinessException("Collaboration:LastAdminRequired");
        member.SetRole(role); await db.SaveChangesAsync(ct);
    }

    public async Task LeaveAsync(Guid id, Guid? transferAdminTo = null, CancellationToken ct = default)
    {
        var conversation = await RequireMember(id, UserId, ct);
        var me = conversation.Members.Single(x => x.UserId == UserId);
        if (me.Role == ConversationMemberRole.Admin && conversation.Members.Count(x => x.Role == ConversationMemberRole.Admin) == 1)
        {
            var others = conversation.Members.Where(x => x.UserId != UserId).ToList();
            if (others.Count > 0)
            {
                var target = others.SingleOrDefault(x => x.UserId == transferAdminTo)
                    ?? throw new BusinessException("Collaboration:AdminTransferRequired");
                target.SetRole(ConversationMemberRole.Admin);
            }
        }
        db.ConversationMembers.Remove(me); await db.SaveChangesAsync(ct);
    }

    public async Task<ConversationPermissionDto> GetPermissionsAsync(Guid id, CancellationToken ct = default)
    {
        var conversation = await RequireMember(id, UserId, ct);
        var isConversationAdmin = conversation.Members.Single(x => x.UserId == UserId).Role == ConversationMemberRole.Admin;
        var canManage = isConversationAdmin || IsSystemAdmin;
        return new ConversationPermissionDto(true, canManage, canManage,
            conversation.Type != ConversationType.User, canManage);
    }

    public async Task<ChatMessageDto> SendMessageAsync(SendMessageInput input, CancellationToken ct = default)
    {
        var conversation = await RequireMember(input.ConversationId, UserId, ct);
        var text = input.Text?.Trim() ?? string.Empty;
        if (text.Length == 0 && input.AttachmentIds.Count == 0)
            throw new BusinessException("Collaboration:EmptyMessage");
        if (input.ClientMessageId.HasValue)
        {
            var duplicate = await db.Messages.AsNoTracking().Include(x => x.Attachments)
                .SingleOrDefaultAsync(x => x.ConversationId == input.ConversationId && x.ClientMessageId == input.ClientMessageId, ct);
            if (duplicate is not null) return await MapMessageAsync(duplicate, ct);
        }
        if (input.ReplyToMessageId.HasValue && !await db.Messages.AnyAsync(x => x.Id == input.ReplyToMessageId && x.ConversationId == input.ConversationId, ct))
            throw new BusinessException("Collaboration:ReplyMessageNotFound");

        var now = clock.Now.ToUniversalTime();
        var message = new ChatMessage(guidGenerator.Create(), input.ConversationId, UserId, text,
            input.ClientMessageId, input.ReplyToMessageId);
        if (input.AttachmentIds.Count > 20) throw new BusinessException("Collaboration:TooManyAttachments");
        var attachmentIds = input.AttachmentIds.Distinct().ToArray();
        var attachments = await db.Attachments.Where(x => attachmentIds.Contains(x.Id)).ToListAsync(ct);
        if (attachments.Count != attachmentIds.Length) throw new BusinessException("Collaboration:InvalidAttachment");
        if (attachments.Any(x => x.ConversationId != input.ConversationId || x.UploadedByUserId != UserId || x.MessageId.HasValue))
            throw new BusinessException("Collaboration:InvalidAttachment");
        foreach (var attachment in attachments) { attachment.AttachTo(message.Id); message.Attachments.Add(attachment); }
        db.Messages.Add(message);
        conversation.SetLastMessage(text.Length > 0 ? text : attachments[0].FileName, now);
        var recipientIds = conversation.Members.Where(x => x.UserId != UserId).Select(x => x.UserId).ToArray();

        var evt = new ChatMessageSentEto(guidGenerator.Create(), now, CurrentCorrelationId(), conversation.Id,
            message.Id, UserId, recipientIds, CurrentDisplayName());
        db.OutboxMessages.Add(new OutboxMessage(evt.EventId, nameof(ChatMessageSentEto), JsonSerializer.Serialize(evt), now));
        try { await SaveMessageAndIncrementUnreadAsync(message.ConversationId, recipientIds, ct); }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception) && input.ClientMessageId.HasValue)
        {
            db.ChangeTracker.Clear();
            var concurrentDuplicate = await db.Messages.AsNoTracking().Include(x => x.Attachments)
                .SingleAsync(x => x.ConversationId == input.ConversationId && x.ClientMessageId == input.ClientMessageId, ct);
            return await MapMessageAsync(concurrentDuplicate, ct);
        }
        var dto = await MapMessageAsync(message, ct);
        await TryNotifyAsync(() => notifier.MessageSentAsync(dto, recipientIds, ct), "message sent", message.Id);
        return dto;
    }

    public async Task<ChatMessageDto> ForwardMessageAsync(Guid messageId, Guid targetConversationId, string? comment, CancellationToken ct = default)
    {
        var source = await db.Messages.Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == messageId, ct)
            ?? throw new BusinessException("Collaboration:MessageNotFound");
        await RequireMember(source.ConversationId, UserId, ct);
        var target = await RequireMember(targetConversationId, UserId, ct);
        var text = ChatModerationRules.ForwardBody(comment);
        var now = clock.Now.ToUniversalTime();
        var forwarded = new ChatMessage(guidGenerator.Create(), target.Id, UserId, text, forwardedFromMessageId: source.Id);
        db.Messages.Add(forwarded); target.SetLastMessage(text, now);
        var recipientIds = target.Members.Where(x => x.UserId != UserId).Select(x => x.UserId).ToArray();
        var evt = new ChatMessageSentEto(guidGenerator.Create(), now, CurrentCorrelationId(), target.Id,
            forwarded.Id, UserId, recipientIds, CurrentDisplayName());
        db.OutboxMessages.Add(new OutboxMessage(evt.EventId, nameof(ChatMessageSentEto), JsonSerializer.Serialize(evt), now));
        await SaveMessageAndIncrementUnreadAsync(target.Id, recipientIds, ct);
        var dto = await MapMessageAsync(forwarded, ct);
        await TryNotifyAsync(() => notifier.MessageSentAsync(dto, recipientIds, ct), "forwarded message", forwarded.Id);
        return dto;
    }

    public async Task DeleteMessageAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await db.Messages.SingleOrDefaultAsync(x => x.Id == messageId, ct) ?? throw new BusinessException("Collaboration:MessageNotFound");
        var conversation = await RequireMember(message.ConversationId, UserId, ct);
        var memberRole = conversation.Members.Single(x => x.UserId == UserId).Role;
        if (!ChatModerationRules.CanDeleteMessage(UserId, message.SenderUserId, IsSystemAdmin, memberRole))
            throw new AbpAuthorizationException();
        message.SoftDeleteContent(); await db.SaveChangesAsync(ct);
        var recipients = await db.ConversationMembers.AsNoTracking().Where(x => x.ConversationId == message.ConversationId)
            .Select(x => x.UserId).ToArrayAsync(ct);
        await TryNotifyAsync(() => notifier.MessageDeletedAsync(message.ConversationId, message.Id, recipients, ct), "message deletion", message.Id);
    }

    public async Task SetMessagePinnedAsync(Guid messageId, bool pinned, CancellationToken ct = default)
    {
        var message = await db.Messages.SingleOrDefaultAsync(x => x.Id == messageId, ct) ?? throw new BusinessException("Collaboration:MessageNotFound");
        await RequireMember(message.ConversationId, UserId, ct);
        if (pinned) message.Pin(UserId, clock.Now.ToUniversalTime()); else message.Unpin();
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkReadAsync(Guid conversationId, CancellationToken ct = default)
    {
        var me = UserId;
        var member = await db.ConversationMembers.SingleOrDefaultAsync(x => x.ConversationId == conversationId && x.UserId == me, ct)
            ?? throw new AbpAuthorizationException();
        await db.ConversationMembers.Where(x => x.Id == member.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.UnreadCount, 0)
                .SetProperty(x => x.LastReadAt, clock.Now.ToUniversalTime()), ct);
    }

    public Task<int> GetTotalUnreadAsync(CancellationToken ct = default)
    {
        var me = UserId;
        return db.ConversationMembers.Where(x => x.UserId == me).SumAsync(x => x.UnreadCount, ct);
    }

    public async Task<PagedMessagesDto> SearchMessagesAsync(Guid conversationId, string? keyword, int skip = 0, int take = 50, bool pinnedOnly = false, CancellationToken ct = default)
    {
        await RequireMember(conversationId, UserId, ct);
        take = Math.Clamp(take, 1, 100);
        var query = db.Messages.AsNoTracking().Include(x => x.Attachments).Where(x => x.ConversationId == conversationId);
        if (pinnedOnly) query = query.Where(x => x.IsPinned && !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(x => !x.IsDeleted && EF.Functions.ILike(x.Text, $"%{keyword.Trim()}%"));
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreationTime).Skip(Math.Max(skip, 0)).Take(take).ToListAsync(ct);
        return new PagedMessagesDto(count, await MapMessagesAsync(items, ct));
    }

    public async Task<MessageContextDto> GetMessageContextAsync(Guid conversationId, Guid messageId, int before = 20, int after = 20, CancellationToken ct = default)
    {
        await RequireMember(conversationId, UserId, ct);
        var target = await db.Messages.AsNoTracking().Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == messageId && x.ConversationId == conversationId, ct)
            ?? throw new BusinessException("Collaboration:MessageNotFound");
        before = Math.Clamp(before, 0, 50); after = Math.Clamp(after, 0, 50);
        var previous = await db.Messages.AsNoTracking().Include(x => x.Attachments)
            .Where(x => x.ConversationId == conversationId &&
                (x.CreationTime < target.CreationTime || (x.CreationTime == target.CreationTime && x.Id.CompareTo(target.Id) < 0)))
            .OrderByDescending(x => x.CreationTime).ThenByDescending(x => x.Id).Take(before + 1).ToListAsync(ct);
        var following = await db.Messages.AsNoTracking().Include(x => x.Attachments)
            .Where(x => x.ConversationId == conversationId &&
                (x.CreationTime > target.CreationTime || (x.CreationTime == target.CreationTime && x.Id.CompareTo(target.Id) > 0)))
            .OrderBy(x => x.CreationTime).ThenBy(x => x.Id).Take(after + 1).ToListAsync(ct);
        var hasMoreBefore = previous.Count > before;
        var hasMoreAfter = following.Count > after;
        var beforeItems = previous.Take(before).AsEnumerable().Reverse().ToList();
        var afterItems = following.Take(after).ToList();
        var mapped = await MapMessagesAsync(beforeItems.Concat([target]).Concat(afterItems).ToList(), ct);
        return new MessageContextDto(
            mapped[beforeItems.Count],
            mapped.Take(beforeItems.Count).ToArray(),
            mapped.Skip(beforeItems.Count + 1).ToArray(),
            hasMoreBefore,
            hasMoreAfter);
    }

    public async Task RequestTaskFromMessageAsync(Guid messageId, string title, string? description, CancellationToken ct = default)
    {
        var message = await db.Messages.SingleOrDefaultAsync(x => x.Id == messageId, ct) ?? throw new BusinessException("Collaboration:MessageNotFound");
        await RequireMember(message.ConversationId, UserId, ct);
        var evt = new TaskFromMessageRequestedEto(guidGenerator.Create(), message.Id, message.ConversationId, UserId,
            Check.NotNullOrWhiteSpace(title, nameof(title), 256), description, clock.Now.ToUniversalTime());
        db.OutboxMessages.Add(new OutboxMessage(evt.EventId, nameof(TaskFromMessageRequestedEto), JsonSerializer.Serialize(evt), evt.OccurredAt));
        await db.SaveChangesAsync(ct);
    }

    private async Task<Conversation> RequireMember(Guid id, Guid userId, CancellationToken ct) =>
        await db.Conversations.Include(x => x.Members).SingleOrDefaultAsync(x => x.Id == id && x.Members.Any(m => m.UserId == userId), ct)
        ?? throw new AbpAuthorizationException("Conversation membership required.");

    private async Task<Conversation> RequireAdmin(Guid id, Guid userId, CancellationToken ct)
    {
        var conversation = await RequireMember(id, userId, ct);
        if (IsSystemAdmin) return conversation;
        if (conversation.Members.Single(x => x.UserId == userId).Role != ConversationMemberRole.Admin) throw new AbpAuthorizationException("Conversation admin required.");
        return conversation;
    }

    private async Task DemandWorkSubjectAccess(ConversationType type, Guid? projectId, Guid? taskId,
        IEnumerable<Guid> requestedUsers, CancellationToken ct)
    {
        var query = db.WorkSubjects.AsNoTracking().Include(x => x.Members)
            .Where(x => x.SubjectType == type.ToString() && !x.IsDeleted);
        query = type == ConversationType.Project
            ? query.Where(x => x.ProjectId == projectId && x.TaskId == null)
            : query.Where(x => x.TaskId == taskId);
        var subject = await query.SingleOrDefaultAsync(ct)
            ?? throw new BusinessException("Collaboration:WorkSubjectNotProvisioned");
        ConversationAccessRules.DemandWorkMembership(UserId, subject.Members.Select(x => x.UserId), requestedUsers);
    }

    private async Task SaveMessageAndIncrementUnreadAsync(Guid conversationId, IReadOnlyCollection<Guid> recipientIds,
        CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        await db.SaveChangesAsync(ct);
        if (recipientIds.Count != 0)
            await db.ConversationMembers
                .Where(x => x.ConversationId == conversationId && recipientIds.Contains(x.UserId))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UnreadCount, x => x.UnreadCount + 1), ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    private async Task TryNotifyAsync(Func<Task> action, string operation, Guid messageId)
    {
        try { await action(); }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Realtime {Operation} failed after durable commit for {MessageId}", operation, messageId);
        }
    }

    private async Task<ConversationDto> ToConversationDto(Conversation c, Guid userId, CancellationToken ct)
    { await db.Entry(c).Collection(x => x.Members).LoadAsync(ct); return MapConversation(c, userId); }
    private static ConversationDto MapConversation(Conversation c, Guid userId)
    {
        var me = c.Members.Single(x => x.UserId == userId);
        return new(c.Id, c.Type, c.Name, c.Description, c.ProjectId, c.TaskId, c.LastMessage, c.LastMessageAt,
            me.UnreadCount, me.IsPinned, c.Members.Select(x => new ConversationMemberDto(x.UserId, x.Role, x.CreationTime)).ToArray());
    }
    private bool IsSystemAdmin => ChatModerationRules.IsSystemAdmin(currentUser.IsInRole("admin"), currentUser.IsInRole("bd-admin"));

    private async Task<ChatMessageDto> MapMessageAsync(ChatMessage message, CancellationToken ct) =>
        (await MapMessagesAsync([message], ct))[0];

    private async Task<IReadOnlyList<ChatMessageDto>> MapMessagesAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct)
    {
        var relatedIds = messages
            .SelectMany(message => new Guid?[] { message.ReplyToMessageId, message.ForwardedFromMessageId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var related = relatedIds.Length == 0
            ? new Dictionary<Guid, ChatMessage>()
            : await db.Messages.AsNoTracking()
                .Where(x => relatedIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
        return messages.Select(message => MapMessage(message, related)).ToArray();
    }

    internal static ChatMessageDto MapMessage(ChatMessage x, IReadOnlyDictionary<Guid, ChatMessage>? related = null)
    {
        static ChatMessagePreviewDto? Preview(Guid? id, IReadOnlyDictionary<Guid, ChatMessage>? lookup)
        {
            if (id is not { } value || lookup is null || !lookup.TryGetValue(value, out var source))
            {
                return null;
            }

            return new ChatMessagePreviewDto(source.Id, source.SenderUserId,
                source.IsDeleted ? string.Empty : source.Text, source.IsDeleted);
        }

        return new ChatMessageDto(x.Id, x.ConversationId, x.SenderUserId,
            x.Text, x.CreationTime, x.ReplyToMessageId, x.ForwardedFromMessageId, x.IsPinned, x.IsDeleted,
            x.Attachments.Select(a => new MessageAttachmentDto(a.Id, a.FileName, a.ContentType, a.Size, a.Kind, a.MessageId)).ToArray(),
            Preview(x.ReplyToMessageId, related),
            Preview(x.ForwardedFromMessageId, related));
    }

    private string CurrentDisplayName() =>
        UserDisplayNames.FromPerson(
            currentUser.SurName,
            currentUser.Name,
            currentUser.UserName,
            currentUser.FindClaim("name")?.Value);

    private string? CurrentCorrelationId() => CurrentUnitOfWork?.Items.TryGetValue("CorrelationId", out var value) == true ? value?.ToString() : null;
}

public interface IChatRealtimeNotifier
{
    Task MessageSentAsync(ChatMessageDto message, IEnumerable<Guid> recipientUserIds, CancellationToken ct = default);
    Task MessageDeletedAsync(Guid conversationId, Guid messageId, IEnumerable<Guid> recipientUserIds, CancellationToken ct = default);
}
