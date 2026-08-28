using System.Collections.Concurrent;
using Volo.Abp.DependencyInjection;

namespace HCS.CollaborationService.Application;

public interface IChatPresenceTracker
{
    /// <returns>true when the user transitions from offline to online.</returns>
    bool TryMarkOnline(string connectionId, Guid userId);

    /// <returns>true when the user transitions from online to offline.</returns>
    bool TryMarkOffline(string connectionId, out Guid userId);

    IReadOnlyCollection<Guid> GetOnlineUserIds();
}

/// <summary>
/// In-memory connection refcount for chat presence (multi-tab safe, single instance).
/// </summary>
public sealed class ChatPresenceTracker : IChatPresenceTracker, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, Guid> connections = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, int> connectionCounts = new();

    public bool TryMarkOnline(string connectionId, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || userId == Guid.Empty)
        {
            return false;
        }

        if (!connections.TryAdd(connectionId, userId))
        {
            return false;
        }

        var count = connectionCounts.AddOrUpdate(userId, 1, static (_, current) => current + 1);
        return count == 1;
    }

    public bool TryMarkOffline(string connectionId, out Guid userId)
    {
        userId = default;
        if (string.IsNullOrWhiteSpace(connectionId) || !connections.TryRemove(connectionId, out userId))
        {
            return false;
        }

        while (true)
        {
            if (!connectionCounts.TryGetValue(userId, out var current))
            {
                return false;
            }

            if (current <= 1)
            {
                if (connectionCounts.TryRemove(new KeyValuePair<Guid, int>(userId, current)))
                {
                    return true;
                }

                continue;
            }

            if (connectionCounts.TryUpdate(userId, current - 1, current))
            {
                return false;
            }
        }
    }

    public IReadOnlyCollection<Guid> GetOnlineUserIds() => connectionCounts.Keys.ToArray();
}
