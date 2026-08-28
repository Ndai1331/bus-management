using HCS.Blazor.Client.Documents;
using HCS.Blazor.Client.Work;
using Xunit;

namespace HCS.WebGateway.Tests;

public sealed class BusinessClientContractTests
{
    [Fact]
    public void Work_list_uri_clamps_paging_and_encodes_filters()
    {
        var query = new WorkListQuery("  A&B  ", "Active", -5, 500);

        var actual = WorkManagementClient.BuildListUri("/api/projects", query);

        Assert.Equal("/api/projects?skip=0&take=100&filter=a%26b&status=Active", actual);
    }

    [Fact]
    public void Document_list_uri_includes_mine_flag_and_status()
    {
        var query = new DocumentListQuery(null, "InReview", true, 20, 20);

        var actual = DocumentClient.BuildListUri(query);

        Assert.Equal("/api/documents?skip=20&take=20&mine=true&status=InReview", actual);
    }

    [Fact]
    public void Document_list_uri_includes_source_type()
    {
        var query = new DocumentListQuery(null, null, false, 0, 50, 2);

        var actual = DocumentClient.BuildListUri(query);

        Assert.Equal("/api/documents?skip=0&take=50&mine=false&sourceType=2", actual);
    }

    [Fact]
    public void Document_list_uri_omits_optional_filters_when_empty()
    {
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var typeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var query = new DocumentListQuery("  title  ", "Draft", false, 0, 10, 0, typeId, null, null, null, from, null);

        var actual = DocumentClient.BuildListUri(query);

        Assert.Contains("filter=title", actual);
        Assert.Contains("status=Draft", actual);
        Assert.Contains("sourceType=0", actual);
        Assert.Contains($"documentTypeId={typeId:D}", actual);
        Assert.Contains($"from={Uri.EscapeDataString(from.ToString("O"))}", actual);
        Assert.DoesNotContain("sectorId=", actual);
        Assert.DoesNotContain("&to=", actual);
    }
}
