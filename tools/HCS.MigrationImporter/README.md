# HCS Migration Importer

One-way, resumable migration utility from the read-only legacy PostgreSQL database to the five HCS Community databases. The source transaction is `REPEATABLE READ` and explicitly `READ ONLY`. The utility never drops databases or buckets.

## Safety boundary

- Only tables in `MigrationManifest.Tables` can be read. SaaS/tenant, GDPR, Text Template, File Management, Forms and OpenIddict Pro names are rejected.
- Target writes use primary-key upserts plus `hcs_migration_checkpoints`; reruns skip rows whose canonical SHA-256 checksum is unchanged.
- Secrets are accepted only through environment variables. No connection strings or credentials belong in tracked files.
- Legacy users are matched to a verified Keycloak export by normalized email or username. Ambiguous, missing and unmatched mappings are reported.
- Legacy signing credential fields (`Password`, `Pin`, private keys, tokens and client secrets) are deliberately stripped. Operators must re-provision them through the encrypted write-only signing configuration flow.
- Reports include per-table counts/checksums, relationship/blob issues, JSON/CSV reconciliation and a non-executable rollback preview.

## Required inputs

Export verified Keycloak users to a protected JSON file outside the repository:

```json
[{ "id": "00000000-0000-0000-0000-000000000000", "email": "user@example.test", "userName": "user", "emailVerified": true }]
```

Set these environment variables in the shell or a secret manager:

```text
HCS_MIGRATION_SOURCE_CONNECTION
HCS_MIGRATION_IDENTITY_CONNECTION
HCS_MIGRATION_ORGANIZATION_CONNECTION
HCS_MIGRATION_DOCUMENT_CONNECTION
HCS_MIGRATION_WORK_CONNECTION
HCS_MIGRATION_COLLABORATION_CONNECTION
```

Optional MinIO validation uses `HCS_MINIO_ENDPOINT`, `HCS_MINIO_ACCESS_KEY`, `HCS_MINIO_SECRET_KEY`, and `HCS_MINIO_USE_SSL`. Without an endpoint, blob network checks are skipped and must be completed before cutover.

Dry-run is the default (the explicit flag is retained for scripts):

```bash
dotnet run --project HCS.MigrationImporter.csproj -- --dry-run --keycloak-users /secure/keycloak-users.json --output /tmp/hcs-migration-report
```

Restrict a rehearsal to allowlisted source tables with `--tables AppDepartments,AppDocuments`. Before a real import, validate source-to-target column compatibility against the deployed fresh migrations; aggregates whose new schema differs from legacy require a reviewed table-specific transform. Set `HCS_MIGRATION_CONFIRM` to the exact phrase printed by the CLI and pass `--execute` only after reviewing reconciliation reports. Rollback remains a DBA/operator action: review `rollback-preview.txt`, confirm backups, then follow the approved runbook against target resources only.
