using System.Text.Json.Nodes;
using Xunit;

namespace HCS.MigrationImporter.Tests;

public sealed class ImportEngineTests
{
    [Fact]
    public async Task Second_run_skips_rows_with_matching_checkpoint()
    {
        var userId = Guid.NewGuid();
        var source = Source(
            Row("AbpUsers", ("Id", "legacy-user"), ("Email", "doctor@example.test"), ("UserName", "doctor")),
            Row("AppDocuments", ("Id", "doc-1"), ("CreatorId", "legacy-user"), ("Name", "A")));
        var target = new FakeTarget();
        var engine = Engine(source, target, new KeycloakUser(userId, "DOCTOR@example.test", "doctor"));

        using var output = new OutputFolder();
        var options = new ImportOptions(false, output.Path, Set("AppDocuments"));
        var first = await engine.RunAsync(options);
        var second = await engine.RunAsync(options);

        Assert.Equal(1, first.Tables.Single().UpsertedRows);
        Assert.Equal(0, second.Tables.Single().UpsertedRows);
        Assert.Equal(1, second.Tables.Single().SkippedRows);
        Assert.Single(target.Writes);
        Assert.Equal(userId, target.Writes[0].Values["CreatorId"]!.GetValue<Guid>());
    }

    [Fact]
    public async Task Dry_run_never_writes_target_and_source_is_opened_read_only()
    {
        var source = Source(Row("AbpUsers", ("Id", "u1"), ("Email", "u@example.test")), Row("AppUnits", ("Id", "unit-1")));
        var target = new FakeTarget();
        using var output = new OutputFolder();

        await Engine(source, target, new KeycloakUser(Guid.NewGuid(), "u@example.test", null))
            .RunAsync(new ImportOptions(true, output.Path, Set("AppUnits")));

        Assert.Empty(target.Writes);
        Assert.Equal(1, source.ReadOnlySnapshotsOpened);
        Assert.Equal(1, source.SnapshotsDisposed);
    }

    [Fact]
    public async Task Reports_duplicate_unmatched_and_missing_user_references()
    {
        var source = Source(
            Row("AbpUsers", ("Id", "duplicate"), ("Email", "same@example.test")),
            Row("AbpUsers", ("Id", "unmatched"), ("Email", "none@example.test")),
            Row("AppDocuments", ("Id", "doc-1"), ("CreatorId", "unknown")));
        var users = new[]
        {
            new KeycloakUser(Guid.NewGuid(), "same@example.test", "one"),
            new KeycloakUser(Guid.NewGuid(), "same@example.test", "two")
        };
        using var output = new OutputFolder();

        var report = await Engine(source, new FakeTarget(), users)
            .RunAsync(new ImportOptions(true, output.Path, Set("AppDocuments")));

        Assert.Single(report.DuplicateUsers);
        Assert.Single(report.UnmatchedUsers);
        Assert.Single(report.MissingUsers);
        Assert.True(File.Exists(System.IO.Path.Combine(output.Path, "reconciliation.json")));
        Assert.True(File.Exists(System.IO.Path.Combine(output.Path, "reconciliation.csv")));
        Assert.Contains("NEVER EXECUTED", await File.ReadAllTextAsync(System.IO.Path.Combine(output.Path, "rollback-preview.txt")));
    }

    [Fact]
    public void Checksum_is_stable_regardless_of_property_order_and_changes_with_data()
    {
        var first = new JsonObject { ["B"] = 2, ["A"] = "one" };
        var reordered = new JsonObject { ["A"] = "one", ["B"] = 2 };
        var changed = new JsonObject { ["A"] = "two", ["B"] = 2 };

        Assert.Equal(ImportEngine.ComputeChecksum(first), ImportEngine.ComputeChecksum(reordered));
        Assert.NotEqual(ImportEngine.ComputeChecksum(first), ImportEngine.ComputeChecksum(changed));
    }

    [Theory]
    [InlineData("SaasTenants")]
    [InlineData("AbpGdprRequests")]
    [InlineData("TextTemplateContents")]
    [InlineData("FileManagementFiles")]
    [InlineData("FormsQuestions")]
    [InlineData("OpenIddictProApplications")]
    [InlineData("TotallyUnknownCustomTable")]
    public void Excluded_or_non_allowlisted_tables_are_rejected(string table)
        => Assert.Throws<InvalidOperationException>(() => MigrationManifest.EnsureAllowed(table));

    private static ImportEngine Engine(FakeSource source, FakeTarget target, params KeycloakUser[] users)
        => new(source, target, new FakeUsers(users), new FakeBlobs());

    private static FakeSource Source(params SourceRow[] rows) => new(rows);
    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.OrdinalIgnoreCase);
    private static SourceRow Row(string table, params (string Key, object? Value)[] values)
    {
        var json = new JsonObject();
        foreach (var (key, value) in values) json[key] = JsonValue.Create(value);
        return new SourceRow(table, json);
    }

    private sealed class FakeSource(IEnumerable<SourceRow> rows) : ILegacySource
    {
        private readonly SourceRow[] _rows = rows.ToArray();
        public int ReadOnlySnapshotsOpened { get; private set; }
        public int SnapshotsDisposed { get; private set; }
        public Task<ISourceSnapshot> OpenReadOnlySnapshotAsync(CancellationToken cancellationToken)
        {
            ReadOnlySnapshotsOpened++;
            return Task.FromResult<ISourceSnapshot>(new Snapshot(_rows, () => SnapshotsDisposed++));
        }

        private sealed class Snapshot(SourceRow[] rows, Action disposed) : ISourceSnapshot
        {
            public async IAsyncEnumerable<SourceRow> ReadAsync(TableMigrationSpec table,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
            {
                foreach (var row in rows.Where(x => x.Table == table.SourceTable))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return new SourceRow(row.Table, (JsonObject)row.Values.DeepClone());
                    await Task.Yield();
                }
            }
            public Task<long> CountAsync(string table, CancellationToken cancellationToken)
                => Task.FromResult((long)rows.Count(x => x.Table == table));
            public ValueTask DisposeAsync() { disposed(); return ValueTask.CompletedTask; }
        }
    }

    private sealed class FakeTarget : ITargetStore
    {
        private readonly Dictionary<string, Checkpoint> _checkpoints = [];
        public List<SourceRow> Writes { get; } = [];
        public Task<Checkpoint?> GetCheckpointAsync(TargetDatabase database, string table, string rowKey, CancellationToken cancellationToken)
            => Task.FromResult(_checkpoints.GetValueOrDefault($"{database}:{table}:{rowKey}"));
        public Task UpsertAsync(TableMigrationSpec table, SourceRow row, string rowKey, string checksum, CancellationToken cancellationToken)
        {
            Writes.Add(new(row.Table, (JsonObject)row.Values.DeepClone()));
            _checkpoints[$"{table.TargetDatabase}:{table.SourceTable}:{rowKey}"] = new(table.SourceTable, rowKey, checksum, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }
        public Task<bool> ExistsAsync(TargetDatabase database, string table, string column, string value, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class FakeUsers(IReadOnlyList<KeycloakUser> users) : IKeycloakUserDirectory
    {
        public Task<IReadOnlyList<KeycloakUser>> GetUsersAsync(CancellationToken cancellationToken) => Task.FromResult(users);
    }

    private sealed class FakeBlobs : IBlobExistenceChecker
    {
        public Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class OutputFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hcs-importer-tests", Guid.NewGuid().ToString("N"));
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
    }
}
