using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace HCS.DocumentService.Workflows;

public sealed class HttpWorkflowAssigneeResolver(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContext,
    IConfiguration configuration) : IWorkflowAssigneeResolver
{
    public async Task<IReadOnlyList<WorkflowAssigneeCandidateDto>> ResolveByRoleAsync(
        Guid roleId, Guid submitterUserId, CancellationToken cancellationToken = default)
    {
        var baseUrl = configuration["Services:Platform:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl) || roleId == Guid.Empty || submitterUserId == Guid.Empty)
            return [];

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"api/identity/workflow-assignees?roleId={roleId:D}");
        var token = httpContext.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.TryAddWithoutValidation("Authorization", token);

        var client = httpClientFactory.CreateClient("HCS.Platform");
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return [];

            var payload = await response.Content.ReadFromJsonAsync<List<WorkflowAssigneeCandidateDto>>(cancellationToken: cancellationToken);
            return payload ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    public async Task<WorkflowAssigneeCandidateDto?> ResolveByUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var baseUrl = configuration["Services:Platform:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl) || userId == Guid.Empty)
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"api/identity/workflow-assignees/{userId:D}");
        var token = httpContext.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.TryAddWithoutValidation("Authorization", token);

        var client = httpClientFactory.CreateClient("HCS.Platform");
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<WorkflowAssigneeCandidateDto>(cancellationToken: cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
