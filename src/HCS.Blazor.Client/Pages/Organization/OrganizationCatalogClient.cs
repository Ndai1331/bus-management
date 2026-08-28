using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HCS.Blazor.Client.Services;
using Microsoft.Extensions.Http;

namespace HCS.Blazor.Client.Pages.Organization;

public sealed class OrganizationCatalogClient(IHttpClientFactory httpClientFactory)
{
    private const int MaxPageSize = 100;

    public Task<OrganizationPagedResponse<DepartmentCatalogDto>> GetDepartmentsAsync(
        OrganizationCatalogQuery query,
        CancellationToken cancellationToken = default) =>
        GetAsync<OrganizationPagedResponse<DepartmentCatalogDto>>(
            BuildListUri(OrganizationCatalogKind.Department, null, query), cancellationToken);

    public Task<OrganizationPagedResponse<UnitCatalogDto>> GetUnitsAsync(
        OrganizationCatalogQuery query,
        CancellationToken cancellationToken = default) =>
        GetAsync<OrganizationPagedResponse<UnitCatalogDto>>(
            BuildListUri(OrganizationCatalogKind.Unit, null, query), cancellationToken);

    public Task<OrganizationPagedResponse<PositionCatalogDto>> GetPositionsAsync(
        OrganizationCatalogQuery query,
        CancellationToken cancellationToken = default) =>
        GetAsync<OrganizationPagedResponse<PositionCatalogDto>>(
            BuildListUri(OrganizationCatalogKind.Position, null, query), cancellationToken);

    public Task<OrganizationPagedResponse<MasterDataCatalogDto>> GetMasterDataAsync(
        string? masterType,
        OrganizationCatalogQuery query,
        CancellationToken cancellationToken = default) =>
        GetAsync<OrganizationPagedResponse<MasterDataCatalogDto>>(
            BuildListUri(OrganizationCatalogKind.MasterData, masterType, query), cancellationToken);

    public Task<IReadOnlyList<DepartmentCatalogDto>> GetDepartmentLookupAsync(
        CancellationToken cancellationToken = default) =>
        GetLookupAsync<DepartmentCatalogDto>(OrganizationCatalogKind.Department, cancellationToken);

    public async Task<IReadOnlyList<UserDepartmentLookupDto>> GetUserDepartmentsAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(x => x != Guid.Empty).Distinct().Take(200).ToArray();
        if (ids.Length == 0) return [];
        var query = string.Join("&", ids.Select(id => $"userIds={id:D}"));
        return await GetAsync<IReadOnlyList<UserDepartmentLookupDto>>(
            $"/api/organization/user-departments?{query}", cancellationToken);
    }

    public Task<OrganizationPagedResponse<DepartmentCatalogDto>> SearchDepartmentsAsync(
        string? filter,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default) =>
        GetDepartmentsAsync(new OrganizationCatalogQuery(filter, true, skipCount, maxResultCount), cancellationToken);

    public Task<OrganizationPagedResponse<MasterDataCatalogDto>> SearchMasterDataAsync(
        string masterType,
        string? filter,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default) =>
        GetMasterDataAsync(masterType, new OrganizationCatalogQuery(filter, true, skipCount, maxResultCount), cancellationToken);

    public Task<DepartmentCatalogDto> CreateDepartmentAsync(
        DepartmentUpsertRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<DepartmentCatalogDto>(
            HttpMethod.Post,
            Endpoint(OrganizationCatalogKind.Department),
            request,
            cancellationToken);

    public Task<DepartmentCatalogDto> UpdateDepartmentAsync(
        Guid id,
        DepartmentUpsertRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<DepartmentCatalogDto>(
            HttpMethod.Put,
            ItemEndpoint(OrganizationCatalogKind.Department, id),
            request,
            cancellationToken);

    public Task<UnitCatalogDto> CreateUnitAsync(
        UnitUpsertRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<UnitCatalogDto>(HttpMethod.Post, Endpoint(OrganizationCatalogKind.Unit), request, cancellationToken);

    public Task<UnitCatalogDto> UpdateUnitAsync(
        Guid id,
        UnitUpsertRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<UnitCatalogDto>(HttpMethod.Put, ItemEndpoint(OrganizationCatalogKind.Unit, id), request, cancellationToken);

    public Task<PositionCatalogDto> CreatePositionAsync(
        PositionUpsertRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<PositionCatalogDto>(HttpMethod.Post, Endpoint(OrganizationCatalogKind.Position), request, cancellationToken);

    public Task<PositionCatalogDto> UpdatePositionAsync(
        Guid id,
        PositionUpsertRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<PositionCatalogDto>(HttpMethod.Put, ItemEndpoint(OrganizationCatalogKind.Position, id), request, cancellationToken);

    public Task<MasterDataCatalogDto> CreateMasterDataAsync(
        MasterDataUpsertRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<MasterDataCatalogDto>(HttpMethod.Post, Endpoint(OrganizationCatalogKind.MasterData), request, cancellationToken);

    public Task<MasterDataCatalogDto> UpdateMasterDataAsync(
        Guid id,
        MasterDataUpsertRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<MasterDataCatalogDto>(HttpMethod.Put, ItemEndpoint(OrganizationCatalogKind.MasterData, id), request, cancellationToken);

    public async Task DeleteAsync(
        OrganizationCatalogKind kind,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        using var response = await CreateClient().DeleteAsync(ItemEndpoint(kind, id), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    internal static string BuildListUri(
        OrganizationCatalogKind kind,
        string? masterType,
        OrganizationCatalogQuery query)
    {
        var builder = new StringBuilder(Endpoint(kind));
        var parameters = new List<string>();

        if (kind == OrganizationCatalogKind.MasterData && !string.IsNullOrWhiteSpace(masterType))
        {
            parameters.Add($"type={Uri.EscapeDataString(masterType.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            parameters.Add($"filter={Uri.EscapeDataString(SearchText.Normalize(query.Filter))}");
        }

        if (query.IsActive.HasValue)
        {
            parameters.Add($"isActive={query.IsActive.Value.ToString().ToLowerInvariant()}");
        }

        parameters.Add($"skipCount={Math.Max(0, query.SkipCount)}");
        parameters.Add($"maxResultCount={Math.Clamp(query.MaxResultCount, 1, MaxPageSize)}");
        builder.Append('?').Append(string.Join('&', parameters));
        return builder.ToString();
    }

    internal static string Endpoint(OrganizationCatalogKind kind) => kind switch
    {
        OrganizationCatalogKind.Department => "/api/organization/departments",
        OrganizationCatalogKind.Unit => "/api/organization/units",
        OrganizationCatalogKind.Position => "/api/organization/positions",
        OrganizationCatalogKind.MasterData => "/api/organization/master-data",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    internal static string ItemEndpoint(OrganizationCatalogKind kind, Guid id) =>
        $"{Endpoint(kind)}/{id:D}";

    private async Task<IReadOnlyList<T>> GetLookupAsync<T>(
        OrganizationCatalogKind kind,
        CancellationToken cancellationToken)
    {
        var items = new List<T>();
        var skipCount = 0;
        long totalCount = long.MaxValue;
        while (items.Count < totalCount)
        {
            var result = await GetAsync<OrganizationPagedResponse<T>>(
                BuildListUri(kind, null, new OrganizationCatalogQuery(null, null, skipCount, MaxPageSize)),
                cancellationToken);
            items.AddRange(result.Items);
            totalCount = result.TotalCount;
            if (result.Items.Count == 0 || result.Items.Count < MaxPageSize)
            {
                break;
            }

            skipCount += result.Items.Count;
        }

        return items;
    }

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using var response = await CreateClient().GetAsync(uri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new OrganizationCatalogApiException(HttpStatusCode.NoContent, "Gateway returned an empty response.");
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string uri,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri)
        {
            Content = JsonContent.Create(payload)
        };
        using var response = await CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new OrganizationCatalogApiException(HttpStatusCode.NoContent, "Gateway returned an empty response.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new OrganizationCatalogApiException(response.StatusCode, body);
    }

    private HttpClient CreateClient() => httpClientFactory.CreateClient("HCS.Bff");
}

internal sealed class OrganizationCatalogApiException(HttpStatusCode statusCode, string? responseBody)
    : Exception($"Organization catalog request failed with HTTP {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = responseBody;
}

public sealed record UserDepartmentLookupDto(Guid UserId, Guid? DepartmentId, string? DepartmentName = null);
