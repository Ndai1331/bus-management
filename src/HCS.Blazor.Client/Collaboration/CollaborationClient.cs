using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HCS.CollaborationService.Contracts;
using HCS.Blazor.Client.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace HCS.Blazor.Client.Collaboration;

internal sealed class CollaborationClient(IHttpClientFactory httpClientFactory)
{
    private const long MaxAttachmentSize = 25 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<IReadOnlyList<ChatContactDto>> GetContactsAsync(
        string? search,
        int take = 30,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<ChatContactDto>>(
            $"api/chat/contacts?search={Uri.EscapeDataString(SearchText.Normalize(search))}&take={Math.Clamp(take, 1, 50)}",
            cancellationToken);

    public Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<ConversationDto>>("api/chat/conversations", cancellationToken);

    public Task<ConversationDto> GetConversationAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<ConversationDto>($"api/chat/conversations/{id:D}", cancellationToken);

    public async Task<ConversationDto?> FindByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        using var response = await CreateClient().GetAsync($"api/chat/conversations/by-project/{projectId:D}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ConversationDto>(JsonOptions, cancellationToken);
    }

    public Task<ConversationPermissionDto> GetPermissionsAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<ConversationPermissionDto>($"api/chat/conversations/{id:D}/permissions", cancellationToken);

    public Task<PagedMessagesDto> GetMessagesAsync(
        Guid conversationId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedMessagesDto>(
            $"api/chat/conversations/{conversationId:D}/messages?skip={Math.Max(skip, 0)}&take={Math.Clamp(take, 1, 100)}",
            cancellationToken);

    public Task<PagedMessagesDto> GetPinnedMessagesAsync(
        Guid conversationId,
        int take = 50,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedMessagesDto>(
            $"api/chat/conversations/{conversationId:D}/messages?skip=0&take={Math.Clamp(take, 1, 100)}&pinnedOnly=true",
            cancellationToken);

    public Task SetMessagePinnedAsync(
        Guid messageId,
        bool pinned,
        CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Put, $"api/chat/messages/{messageId:D}/pin", new { pinned }, cancellationToken);

    public Task<ConversationDto> CreateConversationAsync(
        CreateConversationInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync<CreateConversationInput, ConversationDto>(HttpMethod.Post, "api/chat/conversations", input, cancellationToken);

    public Task<ChatMessageDto> SendMessageAsync(
        SendMessageInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync<SendMessageInput, ChatMessageDto>(HttpMethod.Post, "api/chat/messages", input, cancellationToken);

    public Task MarkReadAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Post, $"api/chat/conversations/{conversationId:D}/read", cancellationToken: cancellationToken);

    public Task SetConversationPinnedAsync(Guid id, bool pinned, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Put, $"api/chat/conversations/{id:D}/pin", new { pinned }, cancellationToken);

    public Task RenameConversationAsync(Guid id, string name, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Put, $"api/chat/conversations/{id:D}/name", new { name }, cancellationToken);

    public Task AddMembersAsync(Guid id, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Post, $"api/chat/conversations/{id:D}/members", new { userIds }, cancellationToken);

    public Task LeaveAsync(Guid id, Guid? transferAdminTo = null, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Post, $"api/chat/conversations/{id:D}/leave", new { transferAdminTo }, cancellationToken);

    public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default) =>
        GetAsync<int>("api/chat/unread-count", cancellationToken);

    public Task RemoveMemberAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/chat/conversations/{id:D}/members/{userId:D}", cancellationToken: cancellationToken);

    public Task DeleteMessageAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/chat/messages/{messageId:D}", cancellationToken: cancellationToken);

    public Task<ChatMessageDto> ForwardMessageAsync(
        Guid messageId,
        Guid targetConversationId,
        string? comment,
        CancellationToken cancellationToken = default) =>
        SendAsync<object, ChatMessageDto>(
            HttpMethod.Post,
            $"api/chat/messages/{messageId:D}/forward",
            new { targetConversationId, comment },
            cancellationToken);

    public Task<MessageContextDto> GetMessageContextAsync(
        Guid conversationId,
        Guid messageId,
        int before = 20,
        int after = 20,
        CancellationToken cancellationToken = default) =>
        GetAsync<MessageContextDto>(
            $"api/chat/conversations/{conversationId:D}/messages/{messageId:D}/context?before={Math.Clamp(before, 0, 50)}&after={Math.Clamp(after, 0, 50)}",
            cancellationToken);

    public Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
        bool unreadOnly = false,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<NotificationDto>>(
            $"api/notifications?unreadOnly={unreadOnly}&skip={Math.Max(skip, 0)}&take={Math.Clamp(take, 1, 100)}",
            cancellationToken);

    public Task<int> GetNotificationUnreadCountAsync(CancellationToken cancellationToken = default) =>
        GetAsync<int>("api/notifications/unread-count", cancellationToken);

    public Task<int> GetNotificationCountAsync(bool unreadOnly = false, CancellationToken cancellationToken = default) =>
        GetAsync<int>($"api/notifications/count?unreadOnly={unreadOnly}", cancellationToken);

    public Task MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Post, $"api/notifications/{notificationId:D}/read", cancellationToken: cancellationToken);

    public Task MarkAllNotificationsReadAsync(CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Post, "api/notifications/read-all", cancellationToken: cancellationToken);

    public async Task<UploadAttachmentResult> UploadAttachmentAsync(
        Guid conversationId,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Size > MaxAttachmentSize)
        {
            throw new CollaborationApiException(HttpStatusCode.RequestEntityTooLarge, "Attachment exceeds the 25 MB limit.");
        }

        using var content = new MultipartFormDataContent();
        await using var source = file.OpenReadStream(MaxAttachmentSize, cancellationToken);
        await using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        using var fileContent = new StreamContent(buffer);
        fileContent.Headers.ContentLength = buffer.Length;
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(fileContent, "file", file.Name);

        using var response = await CreateClient().PostAsync(
            $"api/chat/conversations/{conversationId:D}/attachments", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<UploadAttachmentResult>(JsonOptions, cancellationToken)
            ?? throw new CollaborationApiException(HttpStatusCode.NoContent, "The gateway returned an empty attachment response.");
    }

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using var response = await CreateClient().GetAsync(uri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new CollaborationApiException(HttpStatusCode.NoContent, "The gateway returned an empty response.");
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string uri,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(payload, options: JsonOptions) };
        using var response = await CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken)
            ?? throw new CollaborationApiException(HttpStatusCode.NoContent, "The gateway returned an empty response.");
    }

    private async Task SendNoContentAsync(
        HttpMethod method,
        string uri,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, options: JsonOptions);
        }

        using var response = await CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new CollaborationApiException(response.StatusCode, body);
    }

    private HttpClient CreateClient() => httpClientFactory.CreateClient("HCS.Bff");
}

public sealed class CollaborationApiException(HttpStatusCode statusCode, string? responseBody)
    : Exception($"Collaboration request failed with HTTP {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = responseBody;
}
