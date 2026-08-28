using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HCS.CollaborationService.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace HCS.Blazor.Client.Collaboration;

/// <summary>
/// Owns the one browser SignalR connection for chat messaging and presence.
/// Started from the main layout (via <c>NotificationToast</c>) so a user is online on any HCS page,
/// not only while the Chat workspace is open. Negotiate goes through
/// <see cref="Authentication.BffHttpMessageHandler"/> for BFF antiforgery + credentials.
/// </summary>
public sealed class ChatRealtimeConnection(Uri gatewayBaseAddress) : IAsyncDisposable
{
    private readonly SemaphoreSlim startLock = new(1, 1);
    private HubConnection? connection;

    public event Func<Task>? Changed;
    public event Func<ChatMessageDto, Task>? MessageReceived;
    public event Func<Guid, Guid, Task>? MessageDeleted;
    public event Func<PresenceChangedDto, Task>? PresenceChanged;
    public event Func<IReadOnlyList<Guid>, Task>? PresenceSnapshot;
    public event Func<HubConnectionState, Task>? StatusChanged;

    public Guid? ActiveConversationId { get; set; }

    public bool IsConnected => connection?.State == HubConnectionState.Connected;

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        await startLock.WaitAsync(cancellationToken);
        try
        {
            if (connection is null)
            {
                connection = new HubConnectionBuilder()
                    .WithUrl(new Uri(gatewayBaseAddress, "hubs/chat"), options =>
                    {
                        options.HttpMessageHandlerFactory = innerHandler => new Authentication.BffHttpMessageHandler(gatewayBaseAddress)
                        {
                            InnerHandler = innerHandler
                        };
                    })
                    .WithAutomaticReconnect()
                    .Build();

                connection.On<ChatMessageDto>("ReceiveMessage", NotifyMessageAsync);
                connection.On<ChatDeletedPayload>("MessageDeleted", NotifyDeletedAsync);
                connection.On<PresenceChangedDto>("PresenceChanged", NotifyPresenceChangedAsync);
                connection.Reconnecting += _ => NotifyStatusAsync(HubConnectionState.Reconnecting);
                connection.Reconnected += async _ =>
                {
                    await NotifyStatusAsync(HubConnectionState.Connected);
                    await RefreshPresenceSnapshotAsync(CancellationToken.None);
                };
                connection.Closed += _ => NotifyStatusAsync(HubConnectionState.Disconnected);
            }

            if (connection.State == HubConnectionState.Disconnected)
            {
                try
                {
                    await connection.StartAsync(cancellationToken);
                    await NotifyStatusAsync(HubConnectionState.Connected);
                }
                catch
                {
                    await NotifyStatusAsync(HubConnectionState.Disconnected);
                    throw;
                }
            }

            // Always re-fetch when connected so late subscribers (ChatWorkspace after layout
            // already started the hub) receive users who were already online — PresenceChanged
            // only fires on offline→online transitions.
            if (connection.State == HubConnectionState.Connected)
            {
                await RefreshPresenceSnapshotAsync(cancellationToken);
            }
        }
        finally
        {
            startLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        startLock.Dispose();
    }

    private async Task RefreshPresenceSnapshotAsync(CancellationToken cancellationToken)
    {
        if (connection is null || connection.State != HubConnectionState.Connected)
        {
            return;
        }

        try
        {
            var ids = await connection.InvokeAsync<Guid[]>("GetOnlineUserIds", cancellationToken);
            await NotifyPresenceSnapshotAsync(ids ?? []);
        }
        catch
        {
            // Presence is best-effort; chat messaging still works without a snapshot.
        }
    }

    private async Task NotifyMessageAsync(ChatMessageDto message)
    {
        var received = MessageReceived;
        if (received is not null)
        {
            foreach (var handler in received.GetInvocationList().Cast<Func<ChatMessageDto, Task>>())
            {
                await handler(message);
            }
        }

        await NotifyChangedAsync();
    }

    private async Task NotifyDeletedAsync(ChatDeletedPayload payload)
    {
        var deleted = MessageDeleted;
        if (deleted is not null)
        {
            foreach (var handler in deleted.GetInvocationList().Cast<Func<Guid, Guid, Task>>())
            {
                await handler(payload.ConversationId, payload.MessageId);
            }
        }

        await NotifyChangedAsync();
    }

    private async Task NotifyPresenceChangedAsync(PresenceChangedDto change)
    {
        var handlers = PresenceChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<PresenceChangedDto, Task>>())
        {
            await handler(change);
        }
    }

    private async Task NotifyPresenceSnapshotAsync(IReadOnlyList<Guid> userIds)
    {
        var handlers = PresenceSnapshot;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<IReadOnlyList<Guid>, Task>>())
        {
            await handler(userIds);
        }
    }

    private async Task NotifyChangedAsync()
    {
        var handlers = Changed;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<Task>>())
        {
            await handler();
        }
    }

    private async Task NotifyStatusAsync(HubConnectionState state)
    {
        var handlers = StatusChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<HubConnectionState, Task>>())
        {
            await handler(state);
        }
    }

    private sealed record ChatDeletedPayload(Guid ConversationId, Guid MessageId);
}
