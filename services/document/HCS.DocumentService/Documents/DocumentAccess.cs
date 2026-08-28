using System.Security.Claims;
using Volo.Abp.Authorization;

namespace HCS.DocumentService.Documents;

internal static class DocumentAccess
{
    private static readonly string[] ElevatedRoles = ["admin", "lanhdao"];

    public static Guid RequireUser(ClaimsPrincipal? principal)
    {
        var userIdValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal?.FindFirstValue("sub");
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new AbpAuthorizationException("An authenticated user is required.");
        }

        return userId;
    }

    public static void RequirePermission(ClaimsPrincipal principal, string permission)
    {
        if (!HasPermission(principal, permission))
        {
            throw new AbpAuthorizationException($"Permission '{permission}' is required.");
        }
    }

    public static bool HasPermission(ClaimsPrincipal principal, string permission)
    {
        if (principal.IsInRole("admin") || principal.Claims.Any(claim =>
                (claim.Type is "permission" or "permissions") && claim.Value == permission))
        {
            return true;
        }

        return permission switch
        {
            DocumentPermissions.View or DocumentPermissions.Create or DocumentPermissions.Update or
                DocumentPermissions.ManageFiles or DocumentPermissions.WorkflowView or
                DocumentPermissions.SigningConfigure => principal.Identity?.IsAuthenticated == true,
            DocumentPermissions.Assign or DocumentPermissions.WorkflowStart or
                DocumentPermissions.SigningReport => principal.IsInRole("lanhdao"),
            DocumentPermissions.WorkflowDecide or DocumentPermissions.SigningExecute =>
                principal.IsInRole("lanhdao") || principal.IsInRole("bacsi"),
            "Documents.Review" => principal.IsInRole("lanhdao") || principal.IsInRole("bacsi"),
            "Documents.Approve" => principal.IsInRole("lanhdao"),
            _ => false
        };
    }

    public static bool IsElevated(ClaimsPrincipal principal) => ElevatedRoles.Any(principal.IsInRole);

    public static bool CanView(DocumentAggregate document, Guid userId, ClaimsPrincipal principal) =>
        IsElevated(principal) ||
        document.Assignments.Any(x => x.AssigneeUserId == userId) ||
        document.History.Any(x => x.Action == "Created" && x.ActorUserId == userId);

    public static bool CanManage(DocumentAggregate document, Guid userId, ClaimsPrincipal principal) =>
        IsElevated(principal) ||
        document.History.Any(x => x.Action == "Created" && x.ActorUserId == userId);

    public static void EnsureCanView(DocumentAggregate document, Guid userId, ClaimsPrincipal principal)
    {
        if (!CanView(document, userId, principal)) throw new AbpAuthorizationException("Document access denied.");
    }

    public static void EnsureCanManage(DocumentAggregate document, Guid userId, ClaimsPrincipal principal)
    {
        if (!CanManage(document, userId, principal)) throw new AbpAuthorizationException("Document modification denied.");
    }
}
