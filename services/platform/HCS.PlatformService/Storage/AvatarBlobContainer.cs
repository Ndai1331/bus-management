using Volo.Abp.BlobStoring;

namespace HCS.PlatformService.Storage;

[BlobContainerName("hcs-avatars")]
public sealed class AvatarBlobContainer;

public static class AvatarBlobNamePolicy
{
    public static string ForUser(Guid userId) => $"avatars/{userId:N}";
}
