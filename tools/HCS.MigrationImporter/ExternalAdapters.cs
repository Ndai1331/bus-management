using System.Text.Json;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace HCS.MigrationImporter;

public sealed class JsonKeycloakUserDirectory(string path) : IKeycloakUserDirectory
{
    public async Task<IReadOnlyList<KeycloakUser>> GetUsersAsync(CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<KeycloakUser>>(stream, cancellationToken: cancellationToken) ?? [];
    }
}

public sealed class MinioBlobExistenceChecker(IMinioClient client) : IBlobExistenceChecker
{
    public async Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken cancellationToken)
    {
        try
        {
            await client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(objectName), cancellationToken);
            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
        catch (BucketNotFoundException)
        {
            return false;
        }
    }
}

public sealed class SkipBlobExistenceChecker : IBlobExistenceChecker
{
    public Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken cancellationToken) => Task.FromResult(true);
}
