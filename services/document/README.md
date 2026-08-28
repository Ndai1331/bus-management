# HCS Document Service

ABP Community 10.6 / .NET 10 bounded context for documents, workflows and signing.

## License boundary

- NuGet dependencies are Community-compatible and restored from `nuget.org`.
- No commercial ABP module, generated commercial template, legacy migration, or credential is included.
- The VISNAM/Vin-HSM adapter uses the existing signing SDK references required by the local lab. Redistribution/deployment rights for those SDKs remain a release gate (see `SIGNING-RELEASE-BLOCKER.md`).

## Configuration

All secrets must come from environment variables or .NET User Secrets:

```text
ConnectionStrings__DocumentService=Host=localhost;Database=hcs_document;Username=...;Password=...
AuthServer__Authority=https://localhost:44401
Minio__EndPoint=localhost:9000
Minio__AccessKey=...
Minio__SecretKey=...
RabbitMQ__Connections__Default__HostName=localhost
Signing__RemoteCa__ApiKey=...

# Optional provider defaults (never put TokenRef/secret values here)
Signing__Providers__VISNAM__DefaultEndpoint=https://sign-hn10.vin-hsm.com
Signing__Providers__TAG__DefaultEndpoint=https://<your-tag-endpoint>
```

Buckets are private. Files are returned only through authorized API streams; no public bucket URL is persisted.
