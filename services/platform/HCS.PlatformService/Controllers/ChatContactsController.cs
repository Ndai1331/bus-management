using HCS.CollaborationService.Contracts;
using HCS.EntityFrameworkCore;
using HCS.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace HCS.PlatformService.Controllers;

[ApiController, Authorize(Policy = HCSPermissions.Collaboration.Chat), Route("api/chat/contacts")]
public sealed class ChatContactsController(
    IIdentityUserRepository identityUsers,
    ICurrentUser currentUser,
    HCSDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ChatContactDto>> GetAsync(
        [FromQuery] string? search,
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = NormalizeSearch(search);
        var users = await identityUsers.GetListAsync(
            sorting: "UserName",
            maxResultCount: Math.Clamp(take, 1, 50),
            filter: normalizedSearch,
            notActive: false,
            cancellationToken: cancellationToken);
        var currentUserId = currentUser.Id;

        var activeUsers = users
            .Where(user => user.IsActive && (!currentUserId.HasValue || user.Id != currentUserId.Value))
            .ToArray();
        var activeUserIds = activeUsers.Select(user => user.Id).ToArray();
        var avatarUserIds = await db.UserAvatars
            .AsNoTracking()
            .Where(avatar => activeUserIds.Contains(avatar.UserId))
            .Select(avatar => avatar.UserId)
            .ToHashSetAsync(cancellationToken);

        return activeUsers
            .Select(user => new ChatContactDto(
                user.Id,
                user.UserName,
                UserDisplayNames.FromPerson(user.Surname, user.Name, user.UserName),
                user.IsActive,
                user.Surname,
                user.Name,
                user.PhoneNumber,
                avatarUserIds.Contains(user.Id)
                    ? $"/api/identity/users/{user.Id:D}/avatar"
                    : null))
            .ToArray();
    }

    private static string? NormalizeSearch(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
