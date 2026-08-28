using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HCS.MigrationImporter;

public sealed class ImportEngine(
    ILegacySource source,
    ITargetStore target,
    IKeycloakUserDirectory keycloak,
    IBlobExistenceChecker blobs)
{
    public async Task<ReconciliationReport> RunAsync(ImportOptions options, CancellationToken cancellationToken = default)
    {
        var report = new ReconciliationReport { DryRun = options.DryRun };
        var tables = MigrationManifest.Select(options.RequestedTables);
        foreach (var table in tables) MigrationManifest.EnsureAllowed(table.SourceTable);

        await using var snapshot = await source.OpenReadOnlySnapshotAsync(cancellationToken);
        var userMap = await BuildUserMapAsync(snapshot, report, cancellationToken);

        foreach (var table in tables)
        {
            var sourceCount = await snapshot.CountAsync(table.SourceTable, cancellationToken);
            long upserted = 0, skipped = 0;
            using var tableHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            await foreach (var original in snapshot.ReadAsync(table, cancellationToken))
            {
                var row = Clone(original);
                var rowKey = BuildRowKey(table, row.Values);
                RemapUsers(table, rowKey, row.Values, userMap, report);
                var checksum = ComputeChecksum(row.Values);
                tableHash.AppendData(Encoding.UTF8.GetBytes(rowKey));
                tableHash.AppendData(Convert.FromHexString(checksum));

                if (await IsAlreadyImportedAsync(table, rowKey, checksum, cancellationToken))
                {
                    skipped++;
                    continue;
                }

                await ValidateRelationshipsAsync(table, rowKey, row.Values, report, cancellationToken);
                await ValidateBlobsAsync(table, rowKey, row.Values, report, cancellationToken);
                if (!options.DryRun)
                    await target.UpsertAsync(table, row, rowKey, checksum, cancellationToken);
                upserted++;
            }

            report.Tables.Add(new TableResult(table.SourceTable, sourceCount, upserted, skipped,
                Convert.ToHexString(tableHash.GetHashAndReset()).ToLowerInvariant()));
        }

        report.CompletedAt = DateTimeOffset.UtcNow;
        await ReportWriter.WriteAsync(report, options.OutputDirectory, tables, cancellationToken);
        return report;
    }

    private async Task<Dictionary<string, Guid?>> BuildUserMapAsync(
        ISourceSnapshot snapshot, ReconciliationReport report, CancellationToken cancellationToken)
    {
        var users = await keycloak.GetUsersAsync(cancellationToken);
        var index = users.Where(x => x.EmailVerified)
            .SelectMany(x => new[] { Normalize(x.Email), Normalize(x.UserName) }.Where(v => v is not null).Select(v => (Key: v!, x.Id)))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Select(v => v.Id).Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, Guid?>(StringComparer.OrdinalIgnoreCase);
        var userSpec = MigrationManifest.UserMappingSource;
        await foreach (var row in snapshot.ReadAsync(userSpec, cancellationToken))
        {
            var legacyId = Text(row.Values, "Id");
            if (legacyId is null) continue;
            var keys = new[] { Normalize(Text(row.Values, "Email")), Normalize(Text(row.Values, "UserName")) }
                .Where(x => x is not null).Distinct(StringComparer.OrdinalIgnoreCase).Cast<string>().ToArray();
            var matches = keys.Where(index.ContainsKey).SelectMany(x => index[x]).Distinct().ToArray();
            if (matches.Length == 1)
            {
                result[legacyId] = matches[0];
                continue;
            }

            result[legacyId] = null;
            var issue = new UserIssue("AbpUsers", legacyId, "Id", legacyId,
                matches.Length > 1 ? "Multiple verified Keycloak users match normalized email/username" : "No verified Keycloak user matches normalized email/username");
            (matches.Length > 1 ? report.DuplicateUsers : report.UnmatchedUsers).Add(issue);
        }
        return result;
    }

    private static void RemapUsers(TableMigrationSpec table, string rowKey, JsonObject values,
        IReadOnlyDictionary<string, Guid?> userMap, ReconciliationReport report)
    {
        values.Remove("TenantId");
        if (table.SourceTable == "AppSignatureSettings")
        {
            foreach (var sensitive in new[] { "Password", "Pin", "PrivateKey", "AccessToken", "RefreshToken", "Secret", "ClientSecret" })
                values.Remove(sensitive);
        }
        var userColumns = (table.UserIdColumns ?? []).Concat(["CreatorId", "LastModifierId", "DeleterId"])
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var column in userColumns)
        {
            var legacy = Text(values, column);
            if (legacy is null) continue;
            if (userMap.TryGetValue(legacy, out var mapped) && mapped.HasValue)
                values[column] = mapped.Value;
            else
                report.MissingUsers.Add(new UserIssue(table.SourceTable, rowKey, column, legacy, "Legacy user reference could not be mapped"));
        }

    }

    private async Task<bool> IsAlreadyImportedAsync(TableMigrationSpec table, string key, string checksum, CancellationToken ct)
        => (await target.GetCheckpointAsync(table.TargetDatabase, table.SourceTable, key, ct))?.Checksum == checksum;

    private async Task ValidateRelationshipsAsync(TableMigrationSpec table, string key, JsonObject values,
        ReconciliationReport report, CancellationToken ct)
    {
        foreach (var relationship in table.Relationships ?? [])
        {
            var value = Text(values, relationship.Column);
            if (value is null) continue;
            if (!await target.ExistsAsync(table.TargetDatabase, relationship.ReferencedTable, relationship.ReferencedColumn, value, ct))
                report.RelationshipIssues.Add(new(table.SourceTable, key, relationship.Column, relationship.ReferencedTable, value));
        }
    }

    private async Task ValidateBlobsAsync(TableMigrationSpec table, string key, JsonObject values,
        ReconciliationReport report, CancellationToken ct)
    {
        foreach (var column in table.BlobReferenceColumns ?? [])
        {
            var objectName = Text(values, column);
            if (objectName is null) continue;
            var bucket = BucketFor(table.TargetDatabase);
            if (!await blobs.ExistsAsync(bucket, objectName, ct))
                report.BlobIssues.Add(new(table.SourceTable, key, bucket, objectName, "Object not found in target MinIO"));
        }
    }

    public static string ComputeChecksum(JsonObject values)
    {
        var canonical = new JsonObject(values.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => KeyValuePair.Create(x.Key, x.Value?.DeepClone())));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToJsonString()))).ToLowerInvariant();
    }

    public static string BuildRowKey(TableMigrationSpec table, JsonObject values) => string.Join("|",
        table.KeyColumns.Select(x => Text(values, x) ?? throw new InvalidDataException($"{table.SourceTable}.{x} is null")));

    private static SourceRow Clone(SourceRow row) => new(row.Table, (JsonObject)row.Values.DeepClone());
    private static string? Text(JsonObject values, string column)
    {
        if (values[column] is not JsonValue value) return null;
        if (value.TryGetValue<string>(out var text)) return string.IsNullOrWhiteSpace(text) ? null : text;
        if (value.TryGetValue<Guid>(out var guid)) return guid.ToString();
        var rendered = value.ToString();
        return string.IsNullOrWhiteSpace(rendered) ? null : rendered;
    }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static string BucketFor(TargetDatabase database) => database switch
    {
        TargetDatabase.Document => "hcs-documents",
        TargetDatabase.Work => "hcs-work-assets",
        TargetDatabase.Collaboration => "hcs-collaboration",
        _ => "hcs-migration"
    };
}
