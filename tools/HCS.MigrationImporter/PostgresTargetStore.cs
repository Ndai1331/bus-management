using System.Text.Json.Nodes;
using Npgsql;

namespace HCS.MigrationImporter;

public sealed class PostgresTargetStore(IReadOnlyDictionary<TargetDatabase, string> connectionStrings) : ITargetStore
{
    public async Task<Checkpoint?> GetCheckpointAsync(TargetDatabase database, string table, string rowKey, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(database, cancellationToken);
        await EnsureCheckpointTableAsync(connection, null, cancellationToken);
        await using var command = new NpgsqlCommand("SELECT checksum, completed_at FROM hcs_migration_checkpoints WHERE source_table = @table AND row_key = @key", connection);
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("key", rowKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new Checkpoint(table, rowKey, reader.GetString(0), reader.GetFieldValue<DateTimeOffset>(1))
            : null;
    }

    public async Task UpsertAsync(TableMigrationSpec table, SourceRow row, string rowKey, string checksum, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(table.TargetDatabase, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureCheckpointTableAsync(connection, transaction, cancellationToken);
        var columns = row.Values.Select(x => x.Key).ToArray();
        var quotedColumns = string.Join(", ", columns.Select(PostgresLegacySource.Quote));
        var parameters = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
        var updates = columns.Except(table.KeyColumns, StringComparer.OrdinalIgnoreCase).ToArray();
        var conflict = updates.Length == 0 ? "DO NOTHING" : "DO UPDATE SET " + string.Join(", ", updates.Select(x => $"{PostgresLegacySource.Quote(x)} = EXCLUDED.{PostgresLegacySource.Quote(x)}"));
        var sql = $"INSERT INTO {PostgresLegacySource.Quote(table.TargetTable)} ({quotedColumns}) VALUES ({parameters}) ON CONFLICT ({string.Join(", ", table.KeyColumns.Select(PostgresLegacySource.Quote))}) {conflict}";
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            for (var i = 0; i < columns.Length; i++)
                command.Parameters.AddWithValue($"p{i}", ToDbValue(row.Values[columns[i]]));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var checkpoint = new NpgsqlCommand("""
            INSERT INTO hcs_migration_checkpoints (source_table, row_key, checksum, completed_at)
            VALUES (@table, @key, @checksum, now())
            ON CONFLICT (source_table, row_key) DO UPDATE SET checksum = EXCLUDED.checksum, completed_at = EXCLUDED.completed_at
            """, connection, transaction))
        {
            checkpoint.Parameters.AddWithValue("table", table.SourceTable);
            checkpoint.Parameters.AddWithValue("key", rowKey);
            checkpoint.Parameters.AddWithValue("checksum", checksum);
            await checkpoint.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(TargetDatabase database, string table, string column, string value, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(database, cancellationToken);
        var sql = $"SELECT EXISTS(SELECT 1 FROM {PostgresLegacySource.Quote(table)} WHERE {PostgresLegacySource.Quote(column)}::text = @value)";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("value", value);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    private async Task<NpgsqlConnection> OpenAsync(TargetDatabase database, CancellationToken cancellationToken)
    {
        if (!connectionStrings.TryGetValue(database, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing target connection string for {database}");
        var connection = new NpgsqlConnection(value);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task EnsureCheckpointTableAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS hcs_migration_checkpoints (
              source_table varchar(128) NOT NULL,
              row_key varchar(1000) NOT NULL,
              checksum char(64) NOT NULL,
              completed_at timestamptz NOT NULL,
              PRIMARY KEY (source_table, row_key)
            )
            """, connection, transaction);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static object ToDbValue(JsonNode? node)
    {
        if (node is null) return DBNull.Value;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var b)) return b;
            if (value.TryGetValue<int>(out var i)) return i;
            if (value.TryGetValue<long>(out var l)) return l;
            if (value.TryGetValue<decimal>(out var d)) return d;
            if (value.TryGetValue<DateTime>(out var dt)) return dt;
            if (value.TryGetValue<DateTimeOffset>(out var dto)) return dto;
            if (value.TryGetValue<Guid>(out var guid)) return guid;
            if (value.TryGetValue<string>(out var s)) return s;
        }
        return node.ToJsonString();
    }
}
