# HCS Organization Service

ABP Community 10.6 / .NET 10 single-tenant bounded context for departments, units,
positions, master-data catalogs, and user-to-organization mappings.

The service owns PostgreSQL database `hcs_organization` and schema `hcs_organization`.
No table contains `TenantId` and no other service may query this database directly.

## Local run

```bash
export ConnectionStrings__Organization='Host=localhost;Port=5432;Database=hcs_organization;Username=postgres;Password=...'
dotnet run --project HCS.OrganizationService.Host/HCS.OrganizationService.Host.csproj
```

Credentials belong in environment variables or .NET User Secrets. The tracked JSON
files intentionally contain no database password.

## API contract

- `/api/organization/departments`
- `/api/organization/units`
- `/api/organization/positions`
- `/api/organization/master-data`
- `/api/organization/user-mappings`

Each resource supports list, create, update, and delete. The host listens on
`https://localhost:44412`, matching the WebGateway cluster configuration.

## Fresh migrations

Set `ConnectionStrings__Organization`, then run:

```bash
dotnet ef migrations add InitialOrganization \
  --project HCS.OrganizationService.Host/HCS.OrganizationService.Host.csproj \
  --context OrganizationDbContext
```

Only migrations generated from this Community model are accepted. Do not import
snapshots from the licensed solution.
