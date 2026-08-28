# HCS Work Management Service

ABP Community 10.6 / .NET 10 single-tenant bounded context for projects, tasks,
calendar, surveys, dashboard metrics, and cross-domain reporting read models.

- Owns PostgreSQL database `hcs_work` and schema `hcs_work`.
- Stores only external identifiers for users, organization units, and documents.
- Publishes project/task/calendar/survey changes through a transactional outbox.
- Consumes integration events through an idempotent inbox; no cross-database queries.
- Stores private work attachments in the typed MinIO container `hcs-work-assets`.

## Local configuration

Set secrets only through environment variables or .NET User Secrets:

```bash
export ConnectionStrings__WorkManagement='Host=localhost;Port=5432;Database=hcs_work;Username=postgres;Password=...'
export Minio__AccessKey='...'
export Minio__SecretKey='...'
dotnet run --project HCS.WorkManagementService/HCS.WorkManagementService.csproj
```

The service listens on `https://localhost:44414`, matching the gateway cluster.

## Routes

- `/api/projects`
- `/api/project-tasks`
- `/api/calendar`
- `/api/surveys`
- `/api/reports`
- `/api/dashboard`

## Migration rule

The migration under `HCS.WorkManagementService/Migrations` is generated fresh from
this Community model. Never copy migrations or snapshots from the licensed source.
