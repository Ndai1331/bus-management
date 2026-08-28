using System;
using System.Collections.Generic;

namespace HCS.Blazor.Client.Pages;

internal sealed class IdentityAdminPagedResult<T>
{
    public long TotalCount { get; set; }
    public List<T> Items { get; set; } = [];
}

internal sealed class IdentityAdminUserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool LockoutEnabled { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? CreationTime { get; set; }
    public DateTimeOffset? LastModificationTime { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
}

internal sealed class IdentityAdminRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsStatic { get; set; }
    public bool IsPublic { get; set; }
}

internal sealed class IdentityAdminRoleCreateForm
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsPublic { get; set; }
}

internal sealed class IdentityAdminUserForm
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PositionId { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool LockoutEnabled { get; set; } = true;
    public bool EmailConfirmed { get; set; }
    public bool ShouldChangePasswordOnNextLogin { get; set; }
    public HashSet<string> RoleNames { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class IdentityAdminPermissionResult
{
    public string EntityDisplayName { get; set; } = string.Empty;
    public List<IdentityAdminPermissionGroup> Groups { get; set; } = [];
}

internal sealed class IdentityAdminPermissionGroup
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<IdentityAdminPermission> Permissions { get; set; } = [];
}

internal sealed class IdentityAdminPermission
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
    // Older permission-management responses do not include this field. Treat
    // an omitted value as editable and let the API enforce the final policy.
    public bool IsEditable { get; set; } = true;
}

internal sealed class IdentityAdminUserMappingDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? PositionId { get; set; }
    public bool IsPrimary { get; set; }
}

internal sealed class IdentityAdminApiException(System.Net.HttpStatusCode statusCode, string? responseBody)
    : Exception($"Identity administration request failed with HTTP {(int)statusCode}.")
{
    public System.Net.HttpStatusCode StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = responseBody;
}
