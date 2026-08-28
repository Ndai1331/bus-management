using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HCS.Blazor.Client.Pages;
using Microsoft.Extensions.Http;
using Xunit;

namespace HCS.WebGateway.Tests;

public sealed class IdentityAdminClientTests
{
    [Fact]
    public void Builds_identity_query_with_safe_filter_and_page_bounds()
    {
        var actual = IdentityAdminClient.BuildQuery("api/identity/users", "  admin@example.com  ", -20, 500);

        Assert.Equal("api/identity/users?filter=admin%40example.com&skipCount=0&maxResultCount=100", actual);
    }

    [Fact]
    public async Task Sends_create_user_with_the_community_identity_contract()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id = Guid.NewGuid(), userName = "new-user", email = "new@example.com" })
        });
        var client = CreateClient(handler);
        var form = new IdentityAdminUserForm
        {
            UserName = " new-user ",
            Password = "Password123!",
            Email = "new@example.com",
            IsActive = true,
            LockoutEnabled = true
        };
        form.RoleNames.Add("operator");

        await client.CreateUserAsync(form);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/identity/users", handler.Request.RequestUri!.PathAndQuery);
        using var json = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("new-user", json.RootElement.GetProperty("userName").GetString());
        Assert.Equal("Password123!", json.RootElement.GetProperty("password").GetString());
        Assert.Contains("operator", json.RootElement.GetProperty("roleNames").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task Sends_update_user_with_role_names()
    {
        var userId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id = userId, userName = "existing", email = "existing@example.com" })
        });
        var client = CreateClient(handler);
        var form = new IdentityAdminUserForm
        {
            UserName = "existing",
            Email = "existing@example.com"
        };
        form.RoleNames.Add("admin");
        form.RoleNames.Add("bacsi");

        await client.UpdateUserAsync(userId, form, "stamp");

        using var json = JsonDocument.Parse(handler.RequestBody!);
        var roleNames = json.RootElement.GetProperty("roleNames").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("admin", roleNames);
        Assert.Contains("bacsi", roleNames);
        Assert.Equal("stamp", json.RootElement.GetProperty("concurrencyStamp").GetString());
    }

    [Fact]
    public async Task Sends_update_user_roles_payload()
    {
        var userId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.UpdateUserRolesAsync(userId, ["admin", "lanhdao"]);

        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal($"/api/identity/users/{userId:D}/roles", handler.Request.RequestUri!.PathAndQuery);
        using var json = JsonDocument.Parse(handler.RequestBody!);
        var roleNames = json.RootElement.GetProperty("roleNames").EnumerateArray()
            .Select(item => item.GetString())
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();
        Assert.Equal(new[] { "admin", "lanhdao" }, roleNames);
    }

    [Fact]
    public async Task Deletes_user_organization_mapping()
    {
        var mappingId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.DeleteUserMappingAsync(mappingId);

        Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
        Assert.Equal($"/api/organization/user-mappings/{mappingId:D}", handler.Request.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task Sends_create_role_with_the_community_identity_contract()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id = Guid.NewGuid(), name = "operator", isDefault = true, isPublic = false })
        });
        var client = CreateClient(handler);

        await client.CreateRoleAsync(" operator ", true, false);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/identity/roles", handler.Request.RequestUri!.PathAndQuery);
        using var json = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("operator", json.RootElement.GetProperty("name").GetString());
        Assert.True(json.RootElement.GetProperty("isDefault").GetBoolean());
        Assert.False(json.RootElement.GetProperty("isPublic").GetBoolean());
    }

    [Fact]
    public async Task Uses_role_provider_and_escapes_role_key_for_permissions()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { entityDisplayName = "Role", groups = Array.Empty<object>() })
        });
        var client = CreateClient(handler);

        await client.GetRolePermissionsAsync("role admin");

        Assert.Equal("/api/permission-management/permissions?providerName=R&providerKey=role%20admin", handler.Request!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task Updates_role_permissions_through_the_admin_endpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.UpdateRolePermissionsAsync("role admin",
        [
            new IdentityAdminPermission { Name = "Documents.Workflow.View", IsGranted = true }
        ]);

        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal("/api/admin/roles/role%20admin/permissions", handler.Request.RequestUri!.PathAndQuery);
        using var json = JsonDocument.Parse(handler.RequestBody!);
        var permission = json.RootElement.GetProperty("permissions")[0];
        Assert.Equal("Documents.Workflow.View", permission.GetProperty("name").GetString());
        Assert.True(permission.GetProperty("isGranted").GetBoolean());
    }

    [Fact]
    public async Task Deletes_role_through_the_identity_endpoint()
    {
        var roleId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.DeleteRoleAsync(roleId);

        Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
        Assert.Equal($"/api/identity/roles/{roleId:D}", handler.Request.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task Reads_user_roles_from_the_identity_list_result()
    {
        var userId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                items = new[]
                {
                    new { id = Guid.NewGuid(), name = "bacsi", isDefault = false, isStatic = false, isPublic = true }
                }
            })
        });
        var client = CreateClient(handler);

        var roles = await client.GetUserRolesAsync(userId);

        Assert.Equal($"/api/identity/users/{userId:D}/roles", handler.Request!.RequestUri!.PathAndQuery);
        Assert.Equal(["bacsi"], roles.Select(role => role.Name));
    }

    [Fact]
    public async Task Maps_identity_conflict_to_typed_exception()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("duplicate user")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<IdentityAdminApiException>(() =>
            client.DeleteUserAsync(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("duplicate user", exception.ResponseBody);
    }

    [Fact]
    public async Task Maps_role_delete_conflict_to_typed_exception()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("role in use")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<IdentityAdminApiException>(() =>
            client.DeleteRoleAsync(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("role in use", exception.ResponseBody);
    }

    private static IdentityAdminClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://bff.test") };
        return new IdentityAdminClient(new StubHttpClientFactory(httpClient));
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }
}
