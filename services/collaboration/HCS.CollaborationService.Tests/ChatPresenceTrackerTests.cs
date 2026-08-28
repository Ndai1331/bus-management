using HCS.CollaborationService.Application;
using Shouldly;

namespace HCS.CollaborationService.Tests;

public sealed class ChatPresenceTrackerTests
{
    [Fact]
    public void First_connection_marks_user_online()
    {
        var tracker = new ChatPresenceTracker();
        var userId = Guid.NewGuid();

        tracker.TryMarkOnline("c1", userId).ShouldBeTrue();
        tracker.GetOnlineUserIds().ShouldBe([userId]);
    }

    [Fact]
    public void Second_tab_does_not_emit_online_again()
    {
        var tracker = new ChatPresenceTracker();
        var userId = Guid.NewGuid();

        tracker.TryMarkOnline("c1", userId).ShouldBeTrue();
        tracker.TryMarkOnline("c2", userId).ShouldBeFalse();
        tracker.GetOnlineUserIds().ShouldBe([userId]);
    }

    [Fact]
    public void Offline_only_after_last_connection_closes()
    {
        var tracker = new ChatPresenceTracker();
        var userId = Guid.NewGuid();

        tracker.TryMarkOnline("c1", userId);
        tracker.TryMarkOnline("c2", userId);

        tracker.TryMarkOffline("c1", out var closedUser).ShouldBeFalse();
        closedUser.ShouldBe(userId);
        tracker.GetOnlineUserIds().ShouldBe([userId]);

        tracker.TryMarkOffline("c2", out closedUser).ShouldBeTrue();
        closedUser.ShouldBe(userId);
        tracker.GetOnlineUserIds().ShouldBeEmpty();
    }

    [Fact]
    public void Unknown_connection_offline_is_ignored()
    {
        var tracker = new ChatPresenceTracker();

        tracker.TryMarkOffline("missing", out var userId).ShouldBeFalse();
        userId.ShouldBe(Guid.Empty);
        tracker.GetOnlineUserIds().ShouldBeEmpty();
    }

    [Fact]
    public void Duplicate_connection_id_is_ignored()
    {
        var tracker = new ChatPresenceTracker();
        var userId = Guid.NewGuid();

        tracker.TryMarkOnline("c1", userId).ShouldBeTrue();
        tracker.TryMarkOnline("c1", userId).ShouldBeFalse();
        tracker.TryMarkOffline("c1", out _).ShouldBeTrue();
        tracker.GetOnlineUserIds().ShouldBeEmpty();
    }
}
