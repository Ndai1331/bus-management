using System.Net.Http.Headers;
using System.Net.Http.Json;
using HCS.CollaborationService.Contracts;
using HCS.CollaborationService.Data;
using Microsoft.EntityFrameworkCore;

namespace HCS.CollaborationService.Push;

public interface IPushSender
{
    Task<bool> SendAsync(string token, string title, string body, string? link, CancellationToken ct);
}

public sealed class FirebasePushSender(HttpClient client, IConfiguration configuration, ILogger<FirebasePushSender> logger) : IPushSender
{
    public async Task<bool> SendAsync(string token, string title, string body, string? link, CancellationToken ct)
    {
        var projectId = configuration["Firebase:ProjectId"];
        var accessToken = configuration["Firebase:AccessToken"];
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogInformation("Firebase is not configured; retaining the in-app notification as fallback.");
            return false;
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { message = new { token, notification = new { title, body }, data = new { link = link ?? string.Empty } } });
        using var response = await client.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }
}

public sealed class PushDeliveryWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration,
    ILogger<PushDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CollaborationDbContext>();
                var sender = scope.ServiceProvider.GetRequiredService<IPushSender>();
                var maxAttempts = configuration.GetValue<int?>("Firebase:MaxAttempts") ?? 5;
                var now = DateTime.UtcNow;
                var leaseId = Guid.NewGuid();
                var ids = await db.PushDeliveries.Where(x => x.DeliveredAt == null && x.DeadLetteredAt == null && x.Attempts < maxAttempts &&
                        x.NextAttemptAt <= now && (x.LeaseUntil == null || x.LeaseUntil < now))
                    .OrderBy(x => x.NextAttemptAt).Select(x => x.Id).Take(25).ToListAsync(stoppingToken);
                if (ids.Count != 0)
                    await db.PushDeliveries.Where(x => ids.Contains(x.Id) && x.DeliveredAt == null && x.DeadLetteredAt == null &&
                            x.Attempts < maxAttempts && x.NextAttemptAt <= now && (x.LeaseUntil == null || x.LeaseUntil < now))
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseId, leaseId)
                            .SetProperty(x => x.LeaseUntil, now.AddMinutes(1)), stoppingToken);
                var deliveries = await db.PushDeliveries.Where(x => x.LeaseId == leaseId).ToListAsync(stoppingToken);
                foreach (var delivery in deliveries)
                {
                    var tokens = await db.PushDeviceTokens.Where(x => x.UserId == delivery.UserId && x.IsActive)
                        .Select(x => x.Token).Take(20).ToListAsync(stoppingToken);
                    var delivered = false;
                    string? error = null;
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(configuration.GetValue<int?>("Firebase:DeliveryTimeoutSeconds") ?? 20));
                    try
                    {
                        var results = await Task.WhenAll(tokens.Select(token =>
                            sender.SendAsync(
                                token,
                                NotificationLocalization.Format(delivery.Title, "vi"),
                                NotificationLocalization.Format(delivery.Body, "vi"),
                                delivery.Link,
                                timeout.Token)));
                        delivered = results.Any(x => x);
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    { error = exception.Message; }
                    if (delivered) delivery.Complete(now);
                    else delivery.ScheduleRetry(now.AddSeconds(Math.Pow(2, delivery.Attempts + 1) * 5), maxAttempts,
                        error ?? (tokens.Count == 0 ? "No active device token" : "Provider rejected delivery"));
                    delivery.ReleaseLease();
                }
                if (deliveries.Count != 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception exception) { logger.LogError(exception, "Push delivery cycle failed; in-app notifications remain available"); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
