using Volo.Abp.BlobStoring;

namespace HCS.WorkManagementService.Storage;

[BlobContainerName("hcs-work-assets")]
public sealed class WorkAssetBlobContainer;

public static class WorkAssetBlobNamePolicy
{
    public static string Task(Guid taskId, Guid fileId) => $"tasks/{taskId:N}/{fileId:N}";
    public static string Survey(Guid sessionId, Guid fileId) => $"surveys/{sessionId:N}/{fileId:N}";
}
