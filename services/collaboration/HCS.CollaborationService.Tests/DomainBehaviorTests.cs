using HCS.CollaborationService.Contracts;
using HCS.CollaborationService.Domain;
using Shouldly;
using Volo.Abp;

namespace HCS.CollaborationService.Tests;

public sealed class DomainBehaviorTests
{
    [Fact]
    public void Project_and_task_conversations_require_cross_domain_ids()
    {
        Should.Throw<BusinessException>(() => new Conversation(Guid.NewGuid(), ConversationType.Project, "p", null));
        Should.Throw<BusinessException>(() => new Conversation(Guid.NewGuid(), ConversationType.Task, "t", null));
        new Conversation(Guid.NewGuid(), ConversationType.Project, "p", null, Guid.NewGuid()).Type.ShouldBe(ConversationType.Project);
    }

    [Fact]
    public void Member_unread_state_is_monotonic_until_read()
    {
        var member = new ConversationMember(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationMemberRole.Member);
        member.IncrementUnread(); member.IncrementUnread(); member.UnreadCount.ShouldBe(2);
        var readAt = DateTime.UtcNow; member.MarkRead(readAt);
        member.UnreadCount.ShouldBe(0); member.LastReadAt.ShouldBe(readAt);
    }

    [Fact]
    public void Message_allows_attachment_only_empty_text()
    {
        var message = new ChatMessage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), string.Empty);
        message.Text.ShouldBeEmpty();
    }

    [Fact]
    public void Message_preserves_reply_forward_pin_and_soft_delete_audit_semantics()
    {
        var reply = Guid.NewGuid(); var forwarded = Guid.NewGuid(); var user = Guid.NewGuid();
        var message = new ChatMessage(Guid.NewGuid(), Guid.NewGuid(), user, "hello", Guid.NewGuid(), reply, forwarded);
        message.ReplyToMessageId.ShouldBe(reply); message.ForwardedFromMessageId.ShouldBe(forwarded);
        message.Pin(user, DateTime.UtcNow); message.IsPinned.ShouldBeTrue();
        message.Unpin(); message.IsPinned.ShouldBeFalse();
        message.SoftDeleteContent(); message.IsDeleted.ShouldBeTrue(); message.Text.ShouldBeEmpty();
    }

    [Fact]
    public void Attachment_can_only_be_bound_to_one_message()
    {
        var attachment = new MessageAttachment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "blob", "a.pdf", "application/pdf", 12, AttachmentKind.File);
        attachment.AttachTo(Guid.NewGuid());
        Should.Throw<BusinessException>(() => attachment.AttachTo(Guid.NewGuid()));
    }

    [Fact]
    public void Inbox_id_is_the_event_id_and_outbox_records_attempts()
    {
        var eventId = Guid.NewGuid();
        new InboxMessage(eventId, "event", DateTime.UtcNow).Id.ShouldBe(eventId);
        var outbox = new OutboxMessage(eventId, "event", "{}", DateTime.UtcNow);
        outbox.RecordAttempt(false, DateTime.UtcNow); outbox.Attempts.ShouldBe(1); outbox.PublishedAt.ShouldBeNull();
        outbox.RecordAttempt(true, DateTime.UtcNow); outbox.PublishedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Chat_notification_links_are_identified_for_toast_dedupe()
    {
        ChatNotificationRules.IsChatLink("/chat/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee").ShouldBeTrue();
        ChatNotificationRules.IsChatLink("/chat").ShouldBeTrue();
        ChatNotificationRules.IsChatLink("/document-signing").ShouldBeFalse();
        ChatNotificationRules.IsChatLink(null).ShouldBeFalse();
    }

    [Fact]
    public void System_or_conversation_admin_can_delete_other_people_messages()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        ChatModerationRules.CanDeleteMessage(me, me, false, ConversationMemberRole.Member).ShouldBeTrue();
        ChatModerationRules.CanDeleteMessage(me, other, false, ConversationMemberRole.Member).ShouldBeFalse();
        ChatModerationRules.CanDeleteMessage(me, other, true, ConversationMemberRole.Member).ShouldBeTrue();
        ChatModerationRules.CanDeleteMessage(me, other, false, ConversationMemberRole.Admin).ShouldBeTrue();
        ChatModerationRules.IsSystemAdmin(true, false).ShouldBeTrue();
        ChatModerationRules.IsSystemAdmin(false, true).ShouldBeTrue();
        ChatModerationRules.IsSystemAdmin(false, false).ShouldBeFalse();
        ChatModerationRules.ForwardBody("  note  ").ShouldBe("note");
        ChatModerationRules.ForwardBody(" ").ShouldBe(ChatModerationRules.ForwardedPlaceholder);
    }

    [Fact]
    public void Push_token_can_be_safely_reassigned_without_mutating_the_provider_token()
    {
        var firstUser = Guid.NewGuid(); var nextUser = Guid.NewGuid();
        var device = new PushDeviceToken(Guid.NewGuid(), firstUser, "provider-token", "ios");
        device.Deactivate(); device.AssignTo(nextUser, "android");
        device.UserId.ShouldBe(nextUser); device.Token.ShouldBe("provider-token"); device.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Display_name_prefers_vietnamese_full_name_over_generic_user_fallback()
    {
        UserDisplayNames.FromPerson("Nguyễn", "Văn A", "doctor").ShouldBe("Nguyễn Văn A");
        UserDisplayNames.FromPerson(null, null, "doctor", "User").ShouldBe("doctor");
        UserDisplayNames.FirstReal("User", "HCS", "  ").ShouldBeEmpty();
        UserDisplayNames.FirstReal("User", "Nguyễn Văn A").ShouldBe("Nguyễn Văn A");
    }

    [Fact]
    public void Notification_copy_localizes_by_culture_and_keeps_legacy_plain_text()
    {
        var body = NotificationLocalization.Encode(NotificationLocalization.ChatBody, "Nguyễn Văn A");
        NotificationLocalization.Format(body, "vi").ShouldBe("1 tin nhắn mới từ Nguyễn Văn A");
        NotificationLocalization.Format(body, "en").ShouldBe("1 new message from Nguyễn Văn A");
        NotificationLocalization.Format(NotificationLocalization.ChatTitle, "en").ShouldBe("You have a new message");
        NotificationLocalization.Format("Bạn có tin nhắn mới", "en").ShouldBe("Bạn có tin nhắn mới");
    }
}
