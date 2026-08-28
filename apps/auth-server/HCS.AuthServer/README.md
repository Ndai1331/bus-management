# HCS AuthServer

License-clean ABP Community 10.6 OpenIddict/Account host for .NET 10. It runs separately from the HCS UI at `https://localhost:44401` and uses Keycloak as an optional external OpenID Connect provider.

## Local configuration

Keep credentials outside tracked JSON. Set the database connection and Keycloak secret with environment variables:

```bash
export ConnectionStrings__Default='Host=localhost;Port=5432;Database=hcs;Username=postgres;Password=...'
export Authentication__Keycloak__Authority='http://localhost:5110/realms/bd'
# Docker Compose only — backchannel discovery from the auth-server container:
# export Authentication__Keycloak__MetadataAddress='http://host.docker.internal:5110/realms/bd/.well-known/openid-configuration'
export Authentication__Keycloak__ClientId='hcs-free-auth'
export Authentication__Keycloak__ClientSecret='...'
export Authentication__Keycloak__Enabled='true'
dotnet run --project HCS.AuthServer.csproj
```

Configure the Keycloak client as confidential, enable standard authorization code flow, and register `https://localhost:44401/signin-oidc` as a valid redirect URI. The client must emit its group membership in a `groups` claim.

## Access policy

- `bd-app-hcs` is required. The OIDC callback fails when it is absent.
- `bd-admin`, `bd-lanhdao`, `bd-bacsi`, and `bd-nhanvien` map to their corresponding lowercase HCS roles.
- An entitled user with no mapped role group receives `nhanvien`.
- The authorization request always uses `prompt=login`.

Development settings allow HTTP metadata for the local Keycloak endpoint only. Use HTTPS metadata outside the local lab.
