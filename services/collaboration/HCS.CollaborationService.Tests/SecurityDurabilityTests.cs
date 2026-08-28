using HCS.CollaborationService.Domain;
using HCS.CollaborationService.Contracts;
using HCS.IntegrationEvents.Auditing;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using HCS.CollaborationService.Data;
using HCS.CollaborationService.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HCS.CollaborationService.Tests;

public sealed class SecurityDurabilityTests
{
    [Fact]
    public void Direct_chat_requires_exactly_two_distinct_users()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        ConversationAccessRules.DemandExactlyTwoDirectUsers([first, second]);
        Should.Throw<BusinessException>(() => ConversationAccessRules.DemandExactlyTwoDirectUsers([first]));
        Should.Throw<BusinessException>(() => ConversationAccessRules.DemandExactlyTwoDirectUsers([first, second, Guid.NewGuid()]));
    }

    [Fact]
    public void Work_conversation_rejects_non_projected_caller_or_member()
    {
        var caller = Guid.NewGuid(); var member = Guid.NewGuid(); var outsider = Guid.NewGuid();
        ConversationAccessRules.DemandWorkMembership(caller, [caller, member], [caller, member]);
        Should.Throw<AbpAuthorizationException>(() => ConversationAccessRules.DemandWorkMembership(outsider, [caller, member], [outsider]));
        Should.Throw<AbpAuthorizationException>(() => ConversationAccessRules.DemandWorkMembership(caller, [caller, member], [outsider]));
    }

    [Fact]
    public void Outbox_lease_is_released_and_unknown_events_can_be_dead_lettered()
    {
        var item = new OutboxMessage(Guid.NewGuid(), "unknown", "{}", DateTime.UtcNow);
        var lease = Guid.NewGuid(); item.Lease(lease, DateTime.UtcNow.AddMinutes(1));
        item.LeaseId.ShouldBe(lease);
        item.RecordAttempt(false, DateTime.UtcNow, "transient");
        item.LeaseId.ShouldBeNull(); item.Attempts.ShouldBe(1); item.LastError.ShouldBe("transient");
        item.DeadLetter(DateTime.UtcNow, "unsupported");
        item.DeadLetteredAt.ShouldNotBeNull(); item.LastError.ShouldBe("unsupported");
    }

    [Fact]
    public void Audit_event_uses_the_canonical_event_name()
    {
        AuditRecordCapturedEto.EventName.ShouldBe("hcs.audit.record.v1");
    }

    [Fact]
    public void Conversation_shape_rejects_cross_type_subject_ids()
    {
        Should.Throw<BusinessException>(() => new Conversation(Guid.NewGuid(), ConversationType.User, null, null, Guid.NewGuid()));
        Should.Throw<BusinessException>(() => new Conversation(Guid.NewGuid(), ConversationType.Project, null, null, Guid.NewGuid(), Guid.NewGuid()));
        Should.Throw<BusinessException>(() => new Conversation(Guid.NewGuid(), ConversationType.Task, null, null, Guid.NewGuid(), Guid.NewGuid()));
        new Conversation(Guid.NewGuid(), ConversationType.Task, null, null, null, Guid.NewGuid()).Type.ShouldBe(ConversationType.Task);
    }

    [Fact]
    public void Work_projection_rejects_stale_or_duplicate_event_time()
    {
        var now = DateTimeOffset.UtcNow;
        var subject = new WorkSubjectProjection(Guid.NewGuid(), "Project", Guid.NewGuid(), null, now);
        subject.TryAdvance(now).ShouldBeFalse();
        subject.TryAdvance(now.AddTicks(-1)).ShouldBeFalse();
        subject.TryAdvance(now.AddTicks(1)).ShouldBeTrue();
    }

    [Fact]
    public void Deleted_work_projection_keeps_a_tombstone_against_older_events()
    {
        var now = DateTimeOffset.UtcNow;
        var subject = new WorkSubjectProjection(Guid.NewGuid(), "Project", Guid.NewGuid(), null, now);
        subject.MarkDeleted(now.AddMinutes(1)).ShouldBeTrue();
        subject.IsDeleted.ShouldBeTrue();
        subject.Restore(now.AddTicks(1)).ShouldBeFalse();
        subject.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public void Delete_first_work_projection_creates_a_tombstone_against_older_events()
    {
        var deletedAt = DateTimeOffset.UtcNow;
        var subject = WorkSubjectProjection.CreateDeleted(Guid.NewGuid(), "Project", Guid.NewGuid(), null, deletedAt);

        subject.IsDeleted.ShouldBeTrue();
        subject.Restore(deletedAt.AddTicks(-1)).ShouldBeFalse();
        subject.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public void Direct_conversation_has_a_canonical_user_pair()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var conversation = new Conversation(Guid.NewGuid(), ConversationType.User, null, null,
            directUserOne: first, directUserTwo: second);
        conversation.DirectUserLowId.ShouldNotBeNull();
        conversation.DirectUserHighId.ShouldNotBeNull();
        conversation.DirectUserLowId.ShouldNotBe(conversation.DirectUserHighId);
    }

    [Fact]
    public void Chat_hub_requires_the_chat_policy()
    {
        typeof(ChatHub).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Single().Policy.ShouldBe(CollaborationPermissions.Chat);
    }

    [Fact]
    public void PostgreSql_model_has_type_scoped_subject_uniqueness_and_shape_constraint()
    {
        using var db = new CollaborationDbContext(new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=postgres").Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var conversation = model.FindEntityType(typeof(Conversation))!;
        conversation.GetCheckConstraints().Any(x => x.Name == "CK_Conversation_SubjectShape").ShouldBeTrue();
        conversation.GetIndexes().Single(x => x.Properties.Count == 1 && x.Properties[0].Name == nameof(Conversation.ProjectId))
            .GetFilter().ShouldNotBeNull().ShouldContain("\"Type\" = 2");
        conversation.GetIndexes().Single(x => x.Properties.Count == 2 && x.Properties[0].Name == nameof(Conversation.DirectUserLowId))
            .IsUnique.ShouldBeTrue();
        var subject = model.FindEntityType(typeof(WorkSubjectProjection))!;
        subject.GetIndexes().Single(x => x.Properties.Count == 1 && x.Properties[0].Name == nameof(WorkSubjectProjection.ProjectId))
            .GetFilter().ShouldNotBeNull().ShouldContain("'Project'");
    }

    [Fact]
    public void Push_delivery_dead_letters_after_bounded_attempts()
    {
        var delivery = new PushDelivery(Guid.NewGuid(), Guid.NewGuid(), "title", "body", null, DateTime.UtcNow);
        delivery.Lease(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(1));
        delivery.ScheduleRetry(DateTime.UtcNow, 1, "timeout"); delivery.ReleaseLease();
        delivery.DeadLetteredAt.ShouldNotBeNull(); delivery.LastError.ShouldBe("timeout");
    }

}
