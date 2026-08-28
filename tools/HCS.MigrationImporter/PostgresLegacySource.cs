using System.Data;
using System.Text.Json.Nodes;
using Npgsql;

namespace HCS.MigrationImporter;

public sealed class PostgresLegacySource(string connectionString) : ILegacySource
{
    public async Task<ISourceSnapshot> OpenReadOnlySnapshotAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await using var command = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new Snapshot(connection, transaction);
    }

    private sealed class Snapshot(NpgsqlConnection connection, NpgsqlTransaction transaction) : ISourceSnapshot
    {
        public async IAsyncEnumerable<SourceRow> ReadAsync(TableMigrationSpec table,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            MigrationManifest.EnsureAllowed(table.SourceTable);
            await using var command = new NpgsqlCommand($"SELECT * FROM {Quote(table.SourceTable)} ORDER BY {string.Join(", ", table.KeyColumns.Select(Quote))}", connection, transaction);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var values = new JsonObject();
                for (var i = 0; i < reader.FieldCount; i++)
                    values[reader.GetName(i)] = ToJsonValue(reader.IsDBNull(i) ? null : reader.GetValue(i));
                yield return new SourceRow(table.SourceTable, values);
            }
        }

        public async Task<long> CountAsync(string table, CancellationToken cancellationToken)
        {
            MigrationManifest.EnsureAllowed(table);
            await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM {Quote(table)}", connection, transaction);
            return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.RollbackAsync();
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }

        private static JsonNode? ToJsonValue(object? value) => value switch
        {
            null => null,
            Guid x => JsonValue.Create(x),
            DateTime x => JsonValue.Create(x.ToUniversalTime()),
            DateTimeOffset x => JsonValue.Create(x),
            byte[] x => JsonValue.Create(Convert.ToBase64String(x)),
            bool x => JsonValue.Create(x),
            short x => JsonValue.Create(x),
            int x => JsonValue.Create(x),
            long x => JsonValue.Create(x),
            decimal x => JsonValue.Create(x),
            double x => JsonValue.Create(x),
            float x => JsonValue.Create(x),
            _ => JsonValue.Create(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture))
        };
    }

    internal static string Quote(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Any(x => !(char.IsLetterOrDigit(x) || x == '_')))
            throw new InvalidOperationException($"Unsafe PostgreSQL identifier: {identifier}");
        return $"\"{identifier}\"";
    }
}
