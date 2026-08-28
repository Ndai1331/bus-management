using HCS.MigrationImporter;
using Minio;

try
{
    var cli = CliOptions.Parse(args);
    var source = RequiredEnvironment("HCS_MIGRATION_SOURCE_CONNECTION");
    var targets = new Dictionary<TargetDatabase, string>
    {
        [TargetDatabase.Identity] = RequiredEnvironment("HCS_MIGRATION_IDENTITY_CONNECTION"),
        [TargetDatabase.Organization] = RequiredEnvironment("HCS_MIGRATION_ORGANIZATION_CONNECTION"),
        [TargetDatabase.Document] = RequiredEnvironment("HCS_MIGRATION_DOCUMENT_CONNECTION"),
        [TargetDatabase.Work] = RequiredEnvironment("HCS_MIGRATION_WORK_CONNECTION"),
        [TargetDatabase.Collaboration] = RequiredEnvironment("HCS_MIGRATION_COLLABORATION_CONNECTION")
    };

    IBlobExistenceChecker blobChecker = new SkipBlobExistenceChecker();
    var endpoint = Environment.GetEnvironmentVariable("HCS_MINIO_ENDPOINT");
    if (!string.IsNullOrWhiteSpace(endpoint))
    {
        var client = new MinioClient().WithEndpoint(endpoint)
            .WithCredentials(RequiredEnvironment("HCS_MINIO_ACCESS_KEY"), RequiredEnvironment("HCS_MINIO_SECRET_KEY"));
        if (string.Equals(Environment.GetEnvironmentVariable("HCS_MINIO_USE_SSL"), "true", StringComparison.OrdinalIgnoreCase))
            client = client.WithSSL();
        blobChecker = new MinioBlobExistenceChecker(client.Build());
    }

    var engine = new ImportEngine(new PostgresLegacySource(source), new PostgresTargetStore(targets),
        new JsonKeycloakUserDirectory(cli.KeycloakUsersPath), blobChecker);
    var report = await engine.RunAsync(new ImportOptions(cli.DryRun, cli.OutputDirectory, cli.Tables));
    Console.WriteLine($"Migration {(cli.DryRun ? "dry-run" : "run")} completed. Report: {Path.GetFullPath(cli.OutputDirectory)}");
    Console.WriteLine($"Tables: {report.Tables.Count}; user issues: {report.DuplicateUsers.Count + report.MissingUsers.Count + report.UnmatchedUsers.Count}; blob issues: {report.BlobIssues.Count}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static string RequiredEnvironment(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
    ? value : throw new InvalidOperationException($"Required environment variable is missing: {name}");

internal sealed record CliOptions(bool DryRun, string OutputDirectory, string KeycloakUsersPath, IReadOnlySet<string>? Tables)
{
    public static CliOptions Parse(string[] args)
    {
        if (args.Contains("--help"))
        {
            Console.WriteLine("Usage: HCS.MigrationImporter [--dry-run|--execute] --keycloak-users <verified-users.json> [--output <dir>] [--tables Table1,Table2]");
            Environment.Exit(0);
        }
        var keycloak = Value(args, "--keycloak-users") ?? throw new ArgumentException("--keycloak-users is required");
        var requested = Value(args, "--tables")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var execute = args.Contains("--execute");
        if (execute && Environment.GetEnvironmentVariable("HCS_MIGRATION_CONFIRM") != "I UNDERSTAND TARGET DATA WILL BE MODIFIED")
            throw new InvalidOperationException("--execute requires HCS_MIGRATION_CONFIRM='I UNDERSTAND TARGET DATA WILL BE MODIFIED'");
        return new(!execute, Value(args, "--output") ?? "migration-report", keycloak, requested);
    }

    private static string? Value(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
