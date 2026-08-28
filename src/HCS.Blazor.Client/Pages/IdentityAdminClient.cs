using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HCS.Blazor.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HCS.Blazor.Client.Pages;

internal sealed class IdentityAdminClient(IHttpClientFactory httpClientFactory)
{
    public Task<IdentityAdminPagedResult<IdentityAdminUserDto>> GetUsersAsync(
        string? filter,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default) =>
        GetAsync<IdentityAdminPagedResult<IdentityAdminUserDto>>(
            BuildQuery("api/identity/users", filter, skipCount, maxResultCount), cancellationToken);

    public Task<IdentityAdminPagedResult<IdentityAdminRoleDto>> GetRolesAsync(
        string? filter = null,
        int skipCount = 0,
        int maxResultCount = 100,
        CancellationToken cancellationToken = default) =>
        GetAsync<IdentityAdminPagedResult<IdentityAdminRoleDto>>(
            BuildQuery("api/identity/roles", filter, skipCount, maxResultCount), cancellationToken);

    public Task<IdentityAdminRoleDto> CreateRoleAsync(
        string name,
        bool isDefault,
        bool isPublic,
        CancellationToken cancellationToken = default) =>
        SendAsync<IdentityAdminRoleDto>(HttpMethod.Post, "api/identity/roles", new
        {
            name = name.Trim(),
            isDefault,
            isPublic
        }, cancellationToken);

    public async Task<List<IdentityAdminRoleDto>> GetAssignableRolesAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<ListResult<IdentityAdminRoleDto>>(
            "api/identity/users/assignable-roles", cancellationToken);
        return result.Items;
    }

    public Task<IdentityAdminUserDto> CreateUserAsync(IdentityAdminUserForm form, CancellationToken cancellationToken = default) =>
        SendAsync<IdentityAdminUserDto>(HttpMethod.Post, "api/identity/users", new
        {
            userName = form.UserName.Trim(),
            password = form.Password,
            surname = form.Surname.Trim(),
            name = form.Name.Trim(),
            email = form.Email.Trim(),
            phoneNumber = NullIfWhiteSpace(form.PhoneNumber),
            isActive = form.IsActive,
            lockoutEnabled = form.LockoutEnabled,
            roleNames = form.RoleNames.ToArray()
        }, cancellationToken);

    public Task<IdentityAdminUserDto> UpdateUserAsync(
        Guid id,
        IdentityAdminUserForm form,
        string concurrencyStamp,
        CancellationToken cancellationToken = default) =>
        SendAsync<IdentityAdminUserDto>(HttpMethod.Put, $"api/identity/users/{id:D}", new
        {
            userName = form.UserName.Trim(),
            password = NullIfWhiteSpace(form.Password),
            surname = form.Surname.Trim(),
            name = form.Name.Trim(),
            email = form.Email.Trim(),
            phoneNumber = NullIfWhiteSpace(form.PhoneNumber),
            isActive = form.IsActive,
            lockoutEnabled = form.LockoutEnabled,
            concurrencyStamp,
            roleNames = form.RoleNames.ToArray()
        }, cancellationToken);

    public Task DeleteUserAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/identity/users/{id:D}", cancellationToken);

    public Task DeleteRoleAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/identity/roles/{id:D}", cancellationToken);

    public async Task<List<IdentityAdminRoleDto>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<ListResult<IdentityAdminRoleDto>>(
            $"api/identity/users/{userId:D}/roles", cancellationToken);
        return result.Items;
    }

    public Task UpdateUserRolesAsync(Guid userId, IEnumerable<string> roleNames, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"api/identity/users/{userId:D}/roles", new { roleNames = roleNames.ToArray() }, cancellationToken);

    public Task<IdentityAdminPermissionResult> GetRolePermissionsAsync(string roleName, CancellationToken cancellationToken = default) =>
        GetAsync<IdentityAdminPermissionResult>(
            $"api/permission-management/permissions?providerName=R&providerKey={Uri.EscapeDataString(roleName)}",
            cancellationToken);

    public Task UpdateRolePermissionsAsync(
        string roleName,
        IEnumerable<IdentityAdminPermission> permissions,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Put,
            $"api/admin/roles/{Uri.EscapeDataString(roleName)}/permissions",
            new
            {
                permissions = permissions.Select(permission => new
                {
                    name = permission.Name,
                    isGranted = permission.IsGranted
                })
            },
            cancellationToken);

    public async Task<List<IdentityAdminUserMappingDto>> GetUserMappingsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<ListResult<IdentityAdminUserMappingDto>>(
            $"api/organization/user-mappings?userId={userId:D}&skipCount=0&maxResultCount=100", cancellationToken);
        return result.Items;
    }

    public Task<IdentityAdminUserMappingDto> CreateUserMappingAsync(
        Guid userId,
        Guid departmentId,
        Guid? positionId,
        CancellationToken cancellationToken = default) =>
        SendAsync<IdentityAdminUserMappingDto>(HttpMethod.Post, "api/organization/user-mappings", new
        {
            userId,
            departmentId,
            unitId = (Guid?)null,
            positionId,
            isPrimary = true
        }, cancellationToken);

    public Task<IdentityAdminUserMappingDto> UpdateUserMappingAsync(
        Guid mappingId,
        Guid userId,
        Guid departmentId,
        Guid? positionId,
        CancellationToken cancellationToken = default) =>
        SendAsync<IdentityAdminUserMappingDto>(HttpMethod.Put, $"api/organization/user-mappings/{mappingId:D}", new
        {
            userId,
            departmentId,
            unitId = (Guid?)null,
            positionId,
            isPrimary = true
        }, cancellationToken);

    public Task DeleteUserMappingAsync(Guid mappingId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/organization/user-mappings/{mappingId:D}", cancellationToken);

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using var response = await CreateClient().GetAsync(uri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new IdentityAdminApiException(HttpStatusCode.NoContent, "Gateway returned an empty response.");
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string uri, object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(payload) };
        using var response = await CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new IdentityAdminApiException(HttpStatusCode.NoContent, "Gateway returned an empty response.");
    }

    private async Task SendAsync(HttpMethod method, string uri, object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(payload) };
        using var response = await CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task DeleteAsync(string uri, CancellationToken cancellationToken)
    {
        using var response = await CreateClient().DeleteAsync(uri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new IdentityAdminApiException(response.StatusCode, body);
    }

    private HttpClient CreateClient() => httpClientFactory.CreateClient("HCS.Bff");

    internal static string BuildQuery(string endpoint, string? filter, int skipCount, int maxResultCount)
    {
        var query = new StringBuilder(endpoint).Append('?');
        if (!string.IsNullOrWhiteSpace(filter))
        {
            query.Append("filter=").Append(Uri.EscapeDataString(SearchText.Normalize(filter))).Append('&');
        }

        query.Append("skipCount=").Append(Math.Max(0, skipCount))
            .Append("&maxResultCount=").Append(Math.Clamp(maxResultCount, 1, 100));
        return query.ToString();
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class ListResult<T>
    {
        public List<T> Items { get; set; } = [];
    }
}
