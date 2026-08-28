using System.Text;
using System.Text.Json;

namespace HCS.MigrationImporter;

public static class ReportWriter
{
    public static async Task WriteAsync(ReconciliationReport report, string outputDirectory,
        IReadOnlyList<TableMigrationSpec> tables, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "reconciliation.json"),
            JsonSerializer.Serialize(report, jsonOptions), cancellationToken);

        var csv = new StringBuilder("category,table,rowKey,column,value,reason\n");
        foreach (var issue in report.DuplicateUsers.Select(x => ("duplicate-user", x))
                     .Concat(report.MissingUsers.Select(x => ("missing-user", x)))
                     .Concat(report.UnmatchedUsers.Select(x => ("unmatched-user", x))))
            csv.AppendLine(Csv(issue.Item1, issue.x.Table, issue.x.RowKey, issue.x.Column, issue.x.LegacyValue, issue.x.Reason));
        foreach (var issue in report.RelationshipIssues)
            csv.AppendLine(Csv("relationship", issue.Table, issue.RowKey, issue.Column, issue.Value, $"Missing {issue.ReferencedTable}"));
        foreach (var issue in report.BlobIssues)
            csv.AppendLine(Csv("blob", issue.Table, issue.RowKey, issue.Bucket, issue.ObjectName, issue.Reason));
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "reconciliation.csv"), csv.ToString(), cancellationToken);

        var rollback = new StringBuilder()
            .AppendLine("# ROLLBACK PREVIEW — NEVER EXECUTED BY THE IMPORTER")
            .AppendLine("# Confirm backups and type exactly: I UNDERSTAND TARGET DATA WILL BE DELETED")
            .AppendLine("# Then run your approved DBA runbook for ONLY these target databases:")
            .AppendLine("# hcs_identity, hcs_organization, hcs_document, hcs_work, hcs_collaboration")
            .AppendLine("# Review and delete ONLY importer-created objects in buckets:")
            .AppendLine("# hcs-documents, hcs-signing, hcs-work-assets, hcs-collaboration")
            .AppendLine("# Re-run importer from the last verified source snapshot after cleanup.")
            .AppendLine("# Migrated tables:");
        foreach (var table in tables) rollback.AppendLine($"# - {table.TargetDatabase}: {table.TargetTable}");
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "rollback-preview.txt"), rollback.ToString(), cancellationToken);
    }

    private static string Csv(params string[] fields) => string.Join(',', fields.Select(x => $"\"{x.Replace("\"", "\"\"")}\""));
}
