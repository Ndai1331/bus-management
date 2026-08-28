using HCS.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace HCS.PlatformService.Controllers;

[ApiController, Authorize, Route("api/identity/workflow-assignees")]
public sealed class WorkflowAssigneeCandidatesController(
    IIdentityUserRepository identityUsers,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<WorkflowAssigneeCandidateDto>> GetAsync(
        [FromQuery] Guid roleId,
        CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty || currentUser.Id is not { } submitterUserId)
            return [];

        var candidates = await identityUsers.GetListAsync(
            sorting: "UserName",
            maxResultCount: 200,
            roleId: roleId,
            notActive: false,
            includeDetails: true,
            cancellationToken: cancellationToken);

        var submitter = await identityUsers.FindAsync(submitterUserId, includeDetails: true, cancellationToken: cancellationToken);
        var submitterOuIds = submitter?.OrganizationUnits.Select(x => x.OrganizationUnitId).ToHashSet() ?? [];
        var scoped = submitterOuIds.Count == 0
            ? candidates
            : candidates.Where(user => user.OrganizationUnits.Any(ou => submitterOuIds.Contains(ou.OrganizationUnitId))).ToList();

        return scoped
            .Where(user => user.IsActive)
            .Select(user => new WorkflowAssigneeCandidateDto(
                user.Id,
                DisplayName(user),
                user.OrganizationUnits.Select(x => x.OrganizationUnitId).FirstOrDefault(),
                user.UserName))
            .DistinctBy(x => x.UserId)
            .ToArray();
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<IReadOnlyList<WorkflowAssigneeCandidateDto>>> LookupUsersAsync(
        [FromQuery] Guid[] userIds,
        CancellationToken cancellationToken = default)
    {
        if (!CanResolveUserLookup()) return Forbid();
        var ids = userIds.Where(x => x != Guid.Empty).Distinct().Take(200).ToArray();
        if (ids.Length == 0) return Ok(Array.Empty<WorkflowAssigneeCandidateDto>());

        // Query the scoped Identity DbContext once. Running one FindAsync per ID
        // made this endpoint an N+1 query from list pages such as document-signing.
        var users = await identityUsers.GetListByIdsAsync(ids, includeDetails: false, cancellationToken: cancellationToken);

        return Ok(users
            .Where(user => user.IsActive)
            .Select(user => new WorkflowAssigneeCandidateDto(
                user.Id, DisplayName(user), null, user.UserName))
            .ToArray());
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<WorkflowAssigneeCandidateDto>> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return NotFound();
        if (!User.HasClaim("permission", HCSPermissions.Documents.WorkflowStart))
            return Forbid();

        var user = await identityUsers.FindAsync(userId, includeDetails: true, cancellationToken: cancellationToken);
        if (user is null || !user.IsActive)
            return NotFound();

        return Ok(new WorkflowAssigneeCandidateDto(
            user.Id,
            DisplayName(user),
            user.OrganizationUnits.Select(x => x.OrganizationUnitId).FirstOrDefault(),
            user.UserName));
    }

    private static string DisplayName(IdentityUser user)
    {
        var name = string.Join(' ', new[] { user.Surname, user.Name }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
        return string.IsNullOrWhiteSpace(name) ? user.UserName : name;
    }

    private bool CanResolveUserLookup() =>
        User.HasClaim("permission", HCSPermissions.Documents.SigningExecute)
        || User.HasClaim("permission", HCSPermissions.Documents.WorkflowStart)
        || User.HasClaim("permission", HCSPermissions.Collaboration.Chat);
}

public sealed record WorkflowAssigneeCandidateDto(Guid UserId, string DisplayName, Guid? OrganizationUnitId,
    string? UserName = null);
