using HCS.WorkManagementService.Storage;

namespace HCS.WorkManagementService.Tests;

public sealed class StoragePolicyTests
{
    [Fact]
    public void Work_asset_names_are_single_tenant_and_bounded_by_aggregate()
    {
        var aggregateId = Guid.NewGuid(); var fileId = Guid.NewGuid();
        var name = WorkAssetBlobNamePolicy.Survey(aggregateId, fileId);
        Assert.Equal($"surveys/{aggregateId:N}/{fileId:N}", name);
        Assert.DoesNotContain("tenant", name, StringComparison.OrdinalIgnoreCase);
    }
}
