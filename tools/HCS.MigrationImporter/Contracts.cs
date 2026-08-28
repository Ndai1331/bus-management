namespace HCS.MigrationImporter;

public interface ILegacySource
{
    Task<ISourceSnapshot> OpenReadOnlySnapshotAsync(CancellationToken cancellationToken);
}

public interface ISourceSnapshot : IAsyncDisposable
{
    IAsyncEnumerable<SourceRow> ReadAsync(TableMigrationSpec table, CancellationToken cancellationToken);
    Task<long> CountAsync(string table, CancellationToken cancellationToken);
}

public interface ITargetStore
{
    Task<Checkpoint?> GetCheckpointAsync(TargetDatabase database, string table, string rowKey, CancellationToken cancellationToken);
    Task UpsertAsync(TableMigrationSpec table, SourceRow row, string rowKey, string checksum, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(TargetDatabase database, string table, string column, string value, CancellationToken cancellationToken);
}

public interface IKeycloakUserDirectory
{
    Task<IReadOnlyList<KeycloakUser>> GetUsersAsync(CancellationToken cancellationToken);
}

public interface IBlobExistenceChecker
{
    Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken cancellationToken);
}
