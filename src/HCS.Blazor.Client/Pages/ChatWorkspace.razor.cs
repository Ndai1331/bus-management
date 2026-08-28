using System;
using System.Linq;
using System.Threading.Tasks;
using HCS.CollaborationService.Contracts;
using Microsoft.JSInterop;

namespace HCS.Blazor.Client.Pages;

public partial class ChatWorkspace
{
    private const string MessagesPaneId = "hcs-chat-messages";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (scrollToBottomAfterRender)
        {
            scrollToBottomAfterRender = false;
            await ScrollMessagesAsync(toBottom: true);
            allowOlderScroll = true;
        }

        if (jumpToMessageIdAfterRender is { } messageId)
        {
            jumpToMessageIdAfterRender = null;
            highlightedMessageId = messageId;
            await TryChatScriptAsync(() => Js.InvokeVoidAsync("hcsChat.scrollToMessage", MessageElementId(messageId)));
            allowOlderScroll = true;
            StateHasChanged();
        }
    }

    private async Task LoadOlderMessagesAsync()
    {
        if (selected is null || isLoadingMessages || messages.Count == 0 || messages.Count >= totalMessageCount)
        {
            return;
        }

        var previousHeight = await TryChatScriptAsync(() => Js.InvokeAsync<double>("hcsChat.scrollHeight", MessagesPaneId));
        var oldest = messages[0];
        isLoadingMessages = true;
        messageError = null;
        try
        {
            var context = await Client.GetMessageContextAsync(selected.Id, oldest.Id, before: 50, after: 0);
            foreach (var item in context.Before)
            {
                if (messages.All(message => message.Id != item.Id))
                {
                    messages.Insert(0, item);
                }
            }

            if (!context.HasMoreBefore)
            {
                totalMessageCount = messages.Count;
            }

            messageSkip = messages.Count;
            if (previousHeight is { } height)
            {
                await TryChatScriptAsync(() => Js.InvokeVoidAsync("hcsChat.preserveScrollAfterPrepend", MessagesPaneId, height));
            }
        }
        catch (Exception exception)
        {
            messageError = MapChatError(exception, "Chat:MessagesError");
        }
        finally
        {
            isLoadingMessages = false;
        }
    }

    private async Task OnMessagesScrollAsync()
    {
        if (!allowOlderScroll || selected is null || isLoadingMessages || messages.Count >= totalMessageCount)
        {
            return;
        }

        if (await TryChatScriptAsync(() => Js.InvokeAsync<bool>("hcsChat.isNearTop", MessagesPaneId)) == true)
        {
            await LoadOlderMessagesAsync();
        }
    }

    private void StartReply(ChatMessageDto message)
    {
        replyingTo = message;
        forwardingMessage = null;
    }

    private void CancelReply() => replyingTo = null;

    private void CloseMessageMenu() => messageMenuId = null;

    private void ToggleMessageMenu(Guid messageId) =>
        messageMenuId = messageMenuId == messageId ? null : messageId;

    private void ReplyFromMenu(ChatMessageDto message)
    {
        CloseMessageMenu();
        StartReply(message);
    }

    private void ForwardFromMenu(ChatMessageDto message)
    {
        CloseMessageMenu();
        OpenForward(message);
    }

    private async Task DeleteFromMenuAsync(ChatMessageDto message)
    {
        CloseMessageMenu();
        await DeleteMessageAsync(message);
    }

    private void TogglePinnedPanel() => pinnedPanelOpen = !pinnedPanelOpen;

    private async Task OpenPinnedMessageAsync(ChatMessageDto message)
    {
        pinnedPanelOpen = false;
        await JumpToMessageAsync(message.Id);
    }

    private async Task LoadPinnedMessagesAsync()
    {
        if (selected is null)
        {
            return;
        }

        try
        {
            var response = await Client.GetPinnedMessagesAsync(selected.Id);
            pinnedMessages.Clear();
            pinnedMessages.AddRange(response.Items.OrderByDescending(item => item.CreatedAt));
            if (pinnedMessages.Count == 0)
            {
                pinnedPanelOpen = false;
            }
        }
        catch (Exception)
        {
            pinnedMessages.Clear();
        }
    }

    private async Task ToggleMessagePinnedAsync(ChatMessageDto message)
    {
        if (isPinningMessage)
        {
            return;
        }

        isPinningMessage = true;
        CloseMessageMenu();
        try
        {
            var pinned = !message.IsPinned;
            await Client.SetMessagePinnedAsync(message.Id, pinned);
            ApplyMessagePinned(message.Id, pinned);
            await LoadPinnedMessagesAsync();
        }
        catch (Exception exception)
        {
            messageError = MapChatError(exception, "Chat:PinError");
        }
        finally
        {
            isPinningMessage = false;
        }
    }

    private void ApplyMessagePinned(Guid messageId, bool pinned)
    {
        var index = messages.FindIndex(item => item.Id == messageId);
        if (index >= 0)
        {
            messages[index] = messages[index] with { IsPinned = pinned };
        }
    }

    private void OpenForward(ChatMessageDto message)
    {
        forwardingMessage = message;
        forwardComment = string.Empty;
        forwardSearch = string.Empty;
        forwardTargetId = null;
    }

    private void CloseForward()
    {
        forwardingMessage = null;
        forwardComment = string.Empty;
        forwardTargetId = null;
    }

    private async Task ForwardAsync()
    {
        if (forwardingMessage is null || forwardTargetId is not { } targetId || isForwarding)
        {
            return;
        }

        isForwarding = true;
        messageError = null;
        try
        {
            var sent = await Client.ForwardMessageAsync(forwardingMessage.Id, targetId, forwardComment);
            if (selected?.Id == targetId)
            {
                UpsertMessage(sent);
                scrollToBottomAfterRender = true;
            }

            CloseForward();
            await LoadConversationsAsync();
        }
        catch (Exception exception)
        {
            messageError = MapChatError(exception, "Chat:ForwardError");
        }
        finally
        {
            isForwarding = false;
        }
    }

    private async Task DeleteMessageAsync(ChatMessageDto message)
    {
        if (!CanDeleteMessage(message) || isDeletingMessage || !await UiMessageService.Confirm(T("Chat:DeleteConfirm")))
        {
            return;
        }

        isDeletingMessage = true;
        try
        {
            await Client.DeleteMessageAsync(message.Id);
            MarkDeleted(message.Id);
            await LoadConversationsAsync();
        }
        catch (Exception exception)
        {
            messageError = MapChatError(exception, "Chat:DeleteError");
        }
        finally
        {
            isDeletingMessage = false;
        }
    }

    private async Task JumpToMessageAsync(Guid messageId)
    {
        if (selected is null || messageId == Guid.Empty)
        {
            return;
        }

        if (messages.Any(item => item.Id == messageId))
        {
            jumpToMessageIdAfterRender = messageId;
            return;
        }

        try
        {
            var context = await Client.GetMessageContextAsync(selected.Id, messageId);
            var merged = messages
                .Concat(context.Before)
                .Concat([context.Target])
                .Concat(context.After)
                .GroupBy(item => item.Id)
                .Select(group => group.First())
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .ToList();
            messages.Clear();
            messages.AddRange(merged);
            messageSkip = messages.Count;
            totalMessageCount = Math.Max(totalMessageCount, messages.Count);
            jumpToMessageIdAfterRender = messageId;
        }
        catch (Exception exception)
        {
            messageError = MapChatError(exception, "Chat:MessagesError");
        }
    }

    private async Task RemoveMemberAsync(Guid userId)
    {
        if (selected is null || isRemovingMember || !await UiMessageService.Confirm(T("Chat:RemoveMemberConfirm")))
        {
            return;
        }

        isRemovingMember = true;
        try
        {
            await Client.RemoveMemberAsync(selected.Id, userId);
            selected = await Client.GetConversationAsync(selected.Id);
            permissions = await Client.GetPermissionsAsync(selected.Id);
            await LoadConversationsAsync();
        }
        catch (Exception exception)
        {
            messageError = MapChatError(exception, "Chat:RemoveMemberError");
        }
        finally
        {
            isRemovingMember = false;
        }
    }

    private async Task HandleMessageReceivedAsync(ChatMessageDto message)
    {
        await InvokeAsync(() =>
        {
            if (selected?.Id == message.ConversationId)
            {
                UpsertMessage(message);
                scrollToBottomAfterRender = true;
            }

            StateHasChanged();
            return Task.CompletedTask;
        });
    }

    private async Task HandleMessageDeletedAsync(Guid conversationId, Guid messageId)
    {
        await InvokeAsync(() =>
        {
            if (selected?.Id == conversationId)
            {
                MarkDeleted(messageId);
            }

            StateHasChanged();
            return Task.CompletedTask;
        });
    }

    private bool CanDeleteMessage(ChatMessageDto message) =>
        !message.IsDeleted &&
        currentUserId is { } userId &&
        ChatModerationRules.CanDeleteMessage(
            userId,
            message.SenderUserId,
            permissions?.CanModerateMessages == true,
            selected?.Members.FirstOrDefault(member => member.UserId == userId)?.Role ?? ConversationMemberRole.Member);

    private bool CanRemoveMember(ConversationMemberDto member) =>
        selected is not null &&
        permissions?.CanManageMembers == true &&
        selected.Type != ConversationType.User &&
        member.UserId != currentUserId;

    private void UpsertMessage(ChatMessageDto message)
    {
        var index = messages.FindIndex(item => item.Id == message.Id);
        if (index >= 0)
        {
            messages[index] = message;
            return;
        }

        messages.Add(message);
        messageSkip = messages.Count;
        totalMessageCount = Math.Max(totalMessageCount, messages.Count);
    }

    private void MarkDeleted(Guid messageId)
    {
        var index = messages.FindIndex(item => item.Id == messageId);
        if (index < 0)
        {
            return;
        }

        var current = messages[index];
        messages[index] = current with { IsDeleted = true, Text = string.Empty, Attachments = [], IsPinned = false };
        pinnedMessages.RemoveAll(item => item.Id == messageId);
        if (pinnedMessages.Count == 0)
        {
            pinnedPanelOpen = false;
        }
        if (replyingTo?.Id == messageId)
        {
            replyingTo = null;
        }
    }

    private async Task ScrollMessagesAsync(bool toBottom)
    {
        if (toBottom)
        {
            await TryChatScriptAsync(() => Js.InvokeVoidAsync("hcsChat.scrollToBottom", MessagesPaneId));
        }
    }

    private static async Task TryChatScriptAsync(Func<ValueTask> action)
    {
        try
        {
            await action();
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
    }

    private static async Task<T?> TryChatScriptAsync<T>(Func<ValueTask<T>> action)
    {
        try
        {
            return await action();
        }
        catch (JSDisconnectedException)
        {
            return default;
        }
        catch (JSException)
        {
            return default;
        }
    }

    private static string MessageElementId(Guid id) => $"hcs-chat-msg-{id:N}";

    private static string PreviewText(string? text, string? fallback = null)
    {
        var value = string.IsNullOrWhiteSpace(text) ? fallback : text;
        if (string.IsNullOrWhiteSpace(value) || value == ChatModerationRules.ForwardedPlaceholder)
        {
            return "…";
        }

        return value.Length <= 140 ? value : $"{value[..140]}…";
    }
}
