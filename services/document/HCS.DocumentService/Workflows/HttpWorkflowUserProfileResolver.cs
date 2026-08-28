using System.Net.Http.Json;
using System.Security.Claims;

namespace HCS.DocumentService.Workflows;

public sealed record WorkflowUserProfile(string FullName, string? PositionName, string? DepartmentName);

public interface IWorkflowUserProfileResolver
{
    Task<WorkflowUserProfile> ResolveAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves submitter display metadata through authorized service APIs instead of
/// coupling DocumentService to Identity or Organization database assemblies.
/// </summary>
public sealed class HttpWorkflowUserProfileResolver(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContext,
    IConfiguration configuration) : IWorkflowUserProfileResolver
{
    public async Task<WorkflowUserProfile> ResolveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var principal = httpContext.HttpContext?.User;
        var fullName = ResolveClaimedName(principal, userId);
        var (positionName, departmentName) = await ResolveOrganizationAsync(userId, cancellationToken);
        return new WorkflowUserProfile(fullName, positionName, departmentName);
    }

    private async Task<(string? PositionName, string? DepartmentName)> ResolveOrganizationAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["Services:Organization:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl) || userId == Guid.Empty)
            return (null, null);

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"api/organization/user-departments?userIds={userId:D}");
        var token = httpContext.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.TryAddWithoutValidation("Authorization", token);

        try
        {
            var client = httpClientFactory.CreateClient("HCS.Organization");
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return (null, null);

            var payload = await response.Content.ReadFromJsonAsync<List<UserDepartmentLookupDto>>(
                cancellationToken: cancellationToken);
            var item = payload?.FirstOrDefault(x => x.UserId == userId);
            return (item?.PositionName, item?.DepartmentName);
        }
        catch (HttpRequestException)
        {
            return (null, null);
        }
        catch (System.Text.Json.JsonException)
        {
            return (null, null);
        }
    }

    private static string ResolveClaimedName(ClaimsPrincipal? principal, Guid userId)
    {
        var direct = principal?.FindFirst("name")?.Value
            ?? principal?.FindFirst(ClaimTypes.Name)?.Value;
        if (!string.IsNullOrWhiteSpace(direct)) return direct.Trim();

        var given = principal?.FindFirst("given_name")?.Value
            ?? principal?.FindFirst(ClaimTypes.GivenName)?.Value;
        var family = principal?.FindFirst("family_name")?.Value
            ?? principal?.FindFirst(ClaimTypes.Surname)?.Value;
        var composed = string.Join(' ', new[] { family, given }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (!string.IsNullOrWhiteSpace(composed)) return composed;

        return principal?.FindFirst("preferred_username")?.Value
            ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? userId.ToString("N");
    }

    private sealed record UserDepartmentLookupDto(Guid UserId, Guid? DepartmentId, string? DepartmentName,
        Guid? PositionId, string? PositionName);
}
