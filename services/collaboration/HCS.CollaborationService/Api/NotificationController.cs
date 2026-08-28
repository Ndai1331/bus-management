using HCS.CollaborationService.Application;
using HCS.CollaborationService.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace HCS.CollaborationService.Api;

[ApiController, Authorize(Policy = CollaborationPermissions.Notifications), Route("api/notifications")]
public sealed class NotificationController(NotificationAppService app) : AbpControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<NotificationDto>> GetMine([FromQuery] bool unreadOnly = false, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default) => app.GetMineAsync(unreadOnly, skip, take, ct);

    [HttpGet("unread-count")]
    public Task<int> UnreadCount(CancellationToken ct) => app.CountUnreadAsync(ct);
    [HttpGet("count")]
    public Task<int> CountMine([FromQuery] bool unreadOnly = false, CancellationToken ct = default) =>
        app.CountMineAsync(unreadOnly, ct);
    [HttpPost, Authorize(Policy = CollaborationPermissions.Administration)]
    public Task Create(CreateNotificationInput input, CancellationToken ct) => app.CreateAsync(input, ct);
    [HttpPost("read-all")]
    public Task MarkAllRead(CancellationToken ct) => app.MarkAllReadAsync(ct);
    [HttpPost("{notificationId:guid}/read")]
    public Task MarkRead(Guid notificationId, CancellationToken ct) => app.MarkReadAsync(notificationId, ct);
    [HttpPost("devices")]
    public Task RegisterDevice(RegisterPushDeviceInput input, CancellationToken ct) => app.RegisterDeviceAsync(input, ct);
}
