using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HCS.Blazor.Client.Pages.Organization;
using Microsoft.Extensions.Http;
using Xunit;

namespace HCS.WebGateway.Tests;

public sealed class OrganizationCatalogClientTests
{
    [Fact]
    public void Builds_paged_catalog_url_with_filter_status_and_bounds()
    {
        var query = new OrganizationCatalogQuery("  A&B  ", false, -20, 500);

        var actual = OrganizationCatalogClient.BuildListUri(
            OrganizationCatalogKind.Department,
            null,
            query);

        Assert.Equal(
            "/api/organization/departments?filter=a%26b&isActive=false&skipCount=0&maxResultCount=100",
            actual);
    }

    [Fact]
    public void Adds_the_fixed_master_data_type_to_the_existing_query_contract()
    {
        var actual = OrganizationCatalogClient.BuildListUri(
            OrganizationCatalogKind.MasterData,
            "DocumentType",
            new OrganizationCatalogQuery(null, null, 0, 20));

        Assert.Equal(
            "/api/organization/master-data?type=DocumentType&skipCount=0&maxResultCount=20",
            actual);
    }

    [Theory]
    [InlineData(OrganizationCatalogKind.Department, "/api/organization/departments")]
    [InlineData(OrganizationCatalogKind.Unit, "/api/organization/units")]
    [InlineData(OrganizationCatalogKind.Position, "/api/organization/positions")]
    [InlineData(OrganizationCatalogKind.MasterData, "/api/organization/master-data")]
    public void Maps_catalog_kind_to_the_existing_api_endpoint(OrganizationCatalogKind kind, string expected)
    {
        Assert.Equal(expected, OrganizationCatalogClient.Endpoint(kind));
    }

    [Theory]
    [InlineData("document-types", "DocumentType")]
    [InlineData("sectors", "Sector")]
    [InlineData("urgency-levels", "UrgencyLevel")]
    [InlineData("confidentiality-levels", "ConfidentialityLevel")]
    [InlineData("processing-methods", "ProcessingMethod")]
    [InlineData("document-status", "DocumentStatus")]
    [InlineData("signing-methods", "SigningMethod")]
    [InlineData("even-types", "EventType")]
    [InlineData("event-types", "EventType")]
    public void Keeps_typed_master_data_routes_on_the_allow_list(string route, string expectedType)
    {
        Assert.True(OrganizationCatalogRouteMap.TryResolve(route, out var definition));
        Assert.Equal(expectedType, definition.Type);
    }

    [Fact]
    public async Task Gets_departments_using_typed_response_and_existing_query_contract()
    {
        var id = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                totalCount = 1,
                items = new[]
                {
                    new { id, code = "HR", name = "Human Resources", parentId = (Guid?)null, sortOrder = 2, isActive = true }
                }
            })
        });
        var client = CreateClient(handler);

        var result = await client.GetDepartmentsAsync(new OrganizationCatalogQuery("HR", true, 20, 20));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(id, Assert.Single(result.Items).Id);
        Assert.Equal("HR", result.Items[0].Code);
        Assert.Equal("/api/organization/departments?filter=hr&isActive=true&skipCount=20&maxResultCount=20",
            handler.Request!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task Sends_position_create_as_typed_json_payload()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = Guid.NewGuid(), code = "DIR", name = "Director", signOrder = 10, sortOrder = 1, isActive = true
            })
        });
        var client = CreateClient(handler);

        await client.CreatePositionAsync(new PositionUpsertRequest("DIR", "Director", 10, 1, true));

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/organization/positions", handler.Request.RequestUri!.PathAndQuery);
        using var json = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("DIR", json.RootElement.GetProperty("code").GetString());
        Assert.Equal(10, json.RootElement.GetProperty("signOrder").GetInt32());
        Assert.True(json.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Maps_conflict_response_to_typed_api_exception()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("duplicate code")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<OrganizationCatalogApiException>(() =>
            client.DeleteAsync(OrganizationCatalogKind.Department, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("duplicate code", exception.ResponseBody);
    }

    private static OrganizationCatalogClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bff.test")
        };
        return new OrganizationCatalogClient(new StubHttpClientFactory(httpClient));
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }

        public string? RequestBody { get; private set; }
    }
}
