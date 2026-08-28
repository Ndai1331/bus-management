using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

namespace HCS.Bff;

/// <summary>
/// Keeps the BFF authentication ticket (tokens + claims) in Redis so the browser
/// cookie only carries a session id. Both WebGateway and the Blazor UI host must
/// share this store so F5 refresh can resolve permission claims server-side.
/// </summary>
public sealed class BffAuthTicketStore : ITicketStore
{
    internal const string KeyPrefix = "hcs:bff:ticket:";
    internal const string ProtectorPurpose = "HCS.Bff.AuthTicket.v1";
    private static readonly TimeSpan FallbackLifetime = TimeSpan.FromHours(8);

    private readonly IBffTicketCache cache;
    private readonly IDataProtector protector;
    private readonly TicketSerializer serializer = TicketSerializer.Default;
    private readonly TimeProvider timeProvider;

    public BffAuthTicketStore(
        IConnectionMultiplexer redis,
        IDataProtectionProvider dataProtection,
        TimeProvider timeProvider)
        : this(new RedisBffTicketCache(redis.GetDatabase()), dataProtection, timeProvider)
    {
    }

    internal BffAuthTicketStore(
        IBffTicketCache cache,
        IDataProtectionProvider dataProtection,
        TimeProvider timeProvider)
    {
        this.cache = cache;
        this.timeProvider = timeProvider;
        protector = dataProtection.CreateProtector(ProtectorPurpose);
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        await RenewAsync(key, ticket);
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var payload = protector.Protect(serializer.Serialize(ticket));
        await cache.SetAsync(KeyPrefix + key, payload, GetLifetime(ticket, timeProvider.GetUtcNow()));
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var protectedPayload = await cache.GetAsync(KeyPrefix + key);
        if (protectedPayload is null)
        {
            return null;
        }

        try
        {
            return serializer.Deserialize(protector.Unprotect(protectedPayload));
        }
        catch (Exception)
        {
            await cache.RemoveAsync(KeyPrefix + key);
            return null;
        }
    }

    public Task RemoveAsync(string key) =>
        string.IsNullOrWhiteSpace(key) ? Task.CompletedTask : cache.RemoveAsync(KeyPrefix + key);

    internal static TimeSpan GetLifetime(AuthenticationTicket ticket, DateTimeOffset now)
    {
        if (ticket.Properties.ExpiresUtc is not DateTimeOffset expires)
        {
            return FallbackLifetime;
        }

        var lifetime = expires - now;
        return lifetime > TimeSpan.Zero ? lifetime : TimeSpan.FromSeconds(1);
    }
}

internal interface IBffTicketCache
{
    Task SetAsync(string key, byte[] value, TimeSpan expiry);
    Task<byte[]?> GetAsync(string key);
    Task RemoveAsync(string key);
}

internal sealed class RedisBffTicketCache(IDatabase redis) : IBffTicketCache
{
    public async Task SetAsync(string key, byte[] value, TimeSpan expiry) =>
        await redis.StringSetAsync(key, value, expiry);

    public async Task<byte[]?> GetAsync(string key)
    {
        var value = await redis.StringGetAsync(key);
        return value.HasValue ? (byte[]?)value : null;
    }

    public async Task RemoveAsync(string key) => await redis.KeyDeleteAsync(key);
}

internal sealed class MemoryBffTicketCache : IBffTicketCache
{
    private readonly Dictionary<string, byte[]> items = new(StringComparer.Ordinal);

    public Task SetAsync(string key, byte[] value, TimeSpan expiry)
    {
        items[key] = value;
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(string key) =>
        Task.FromResult(items.TryGetValue(key, out var value) ? value : null);

    public Task RemoveAsync(string key)
    {
        items.Remove(key);
        return Task.CompletedTask;
    }
}
