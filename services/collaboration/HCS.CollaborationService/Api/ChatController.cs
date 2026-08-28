using HCS.CollaborationService.Application;
using HCS.CollaborationService.Contracts;
using HCS.CollaborationService.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace HCS.CollaborationService.Api;

[ApiController, Authorize(Policy = CollaborationPermissions.Chat), Route("api/chat")]
public sealed class ChatController(CollaborationAppService app, CollaborationAttachmentStore attachments) : AbpControllerBase
{
    [HttpPost("conversations")]
    public Task<ConversationDto> CreateConversation(CreateConversationInput input, CancellationToken ct) => app.CreateConversationAsync(input, ct);

    [HttpGet("conversations")]
    public Task<IReadOnlyList<ConversationDto>> GetConversations([FromQuery] ConversationType? type, [FromQuery] bool pinnedOnly, CancellationToken ct) => app.GetConversationsAsync(type, pinnedOnly, ct);

    [HttpGet("conversations/by-project/{projectId:guid}")]
    public async Task<ActionResult<ConversationDto>> FindByProject(Guid projectId, CancellationToken ct)
    {
        var found = await app.FindConversationByProjectIdAsync(projectId, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpGet("conversations/{id:guid}")]
    public Task<ConversationDto> GetConversation(Guid id, CancellationToken ct) => app.GetConversationAsync(id, ct);

    [HttpPut("conversations/{id:guid}/name")]
    public Task Rename(Guid id, [FromBody] RenameConversationInput input, CancellationToken ct) => app.RenameAsync(id, input.Name, ct);

    [HttpPut("conversations/{id:guid}/pin")]
    public Task PinConversation(Guid id, [FromBody] PinInput input, CancellationToken ct) => app.SetConversationPinnedAsync(id, input.Pinned, ct);

    [HttpPost("conversations/{id:guid}/members")]
    public Task AddMembers(Guid id, [FromBody] MemberIdsInput input, CancellationToken ct) => app.AddMembersAsync(id, input.UserIds, ct);

    [HttpDelete("conversations/{id:guid}/members/{userId:guid}")]
    public Task RemoveMember(Guid id, Guid userId, CancellationToken ct) => app.RemoveMemberAsync(id, userId, ct);

    [HttpPut("conversations/{id:guid}/members/{userId:guid}/role")]
    public Task SetRole(Guid id, Guid userId, [FromBody] MemberRoleInput input, CancellationToken ct) => app.SetMemberRoleAsync(id, userId, input.Role, ct);

    [HttpPost("conversations/{id:guid}/leave")]
    public Task Leave(Guid id, [FromBody] LeaveInput? input, CancellationToken ct) => app.LeaveAsync(id, input?.TransferAdminTo, ct);

    [HttpGet("conversations/{id:guid}/permissions")]
    public Task<ConversationPermissionDto> Permissions(Guid id, CancellationToken ct) => app.GetPermissionsAsync(id, ct);

    [HttpPost("messages")]
    public Task<ChatMessageDto> SendMessage(SendMessageInput input, CancellationToken ct) => app.SendMessageAsync(input, ct);

    [HttpPost("messages/{messageId:guid}/forward")]
    public Task<ChatMessageDto> Forward(Guid messageId, ForwardMessageInput input, CancellationToken ct) => app.ForwardMessageAsync(messageId, input.TargetConversationId, input.Comment, ct);

    [HttpDelete("messages/{messageId:guid}")]
    public Task DeleteMessage(Guid messageId, CancellationToken ct) => app.DeleteMessageAsync(messageId, ct);

    [HttpPut("messages/{messageId:guid}/pin")]
    public Task PinMessage(Guid messageId, PinInput input, CancellationToken ct) => app.SetMessagePinnedAsync(messageId, input.Pinned, ct);

    [HttpPost("conversations/{conversationId:guid}/read")]
    public Task MarkRead(Guid conversationId, CancellationToken ct) => app.MarkReadAsync(conversationId, ct);

    [HttpGet("unread-count")]
    public Task<int> Unread(CancellationToken ct) => app.GetTotalUnreadAsync(ct);

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public Task<PagedMessagesDto> Search(Guid conversationId, [FromQuery] string? keyword, [FromQuery] int skip = 0, [FromQuery] int take = 50, [FromQuery] bool pinnedOnly = false, CancellationToken ct = default) => app.SearchMessagesAsync(conversationId, keyword, skip, take, pinnedOnly, ct);

    [HttpGet("conversations/{conversationId:guid}/messages/{messageId:guid}/context")]
    public Task<MessageContextDto> Context(Guid conversationId, Guid messageId, [FromQuery] int before = 20, [FromQuery] int after = 20, CancellationToken ct = default) => app.GetMessageContextAsync(conversationId, messageId, before, after, ct);

    [HttpPost("messages/{messageId:guid}/create-task")]
    public Task CreateTask(Guid messageId, CreateTaskFromMessageInput input, CancellationToken ct) => app.RequestTaskFromMessageAsync(messageId, input.Title, input.Description, ct);

    [HttpPost("conversations/{conversationId:guid}/attachments")]
    [RequestSizeLimit(26_214_400)]
    public async Task<UploadAttachmentResult> Upload(Guid conversationId, IFormFile file, CancellationToken ct)
    { await using var stream = file.OpenReadStream(); return await attachments.UploadAsync(conversationId, file.FileName, file.ContentType, stream, file.Length, ct); }

    [HttpGet("attachments/{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    { var file = await attachments.DownloadAsync(id, ct); return File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true); }

    [HttpDelete("attachments/{id:guid}")]
    public Task DeleteAttachment(Guid id, CancellationToken ct) => attachments.DeleteAsync(id, ct);
}

public sealed record RenameConversationInput(string Name);
public sealed record PinInput(bool Pinned);
public sealed record MemberIdsInput(IReadOnlyCollection<Guid> UserIds);
public sealed record MemberRoleInput(ConversationMemberRole Role);
public sealed record LeaveInput(Guid? TransferAdminTo);
public sealed record ForwardMessageInput(Guid TargetConversationId, string? Comment);
public sealed record CreateTaskFromMessageInput(string Title, string? Description);
