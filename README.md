# HCS Community Microservices

## Community baseline

This directory is the license-clean ABP Community 10.6 / .NET 10 baseline for the HCS microservice migration. It must restore from `nuget.org` without an ABP commercial feed or license.

### Requirements

- .NET 10 SDK
- Node.js 20 or newer
- PostgreSQL, Redis, RabbitMQ and MinIO
- Keycloak at `http://localhost:5110` for the BD SSO lab

### License and secret checks

Run the dependency and secret audit before every build:

```bash
./scripts/audit-license-clean.sh
dotnet restore HCS.slnx --configfile NuGet.Config
dotnet build HCS.slnx --no-restore
dotnet test HCS.slnx --no-build
```

Database, Keycloak, MinIO, encryption and certificate secrets must be supplied with environment variables or .NET User Secrets. Never add them to `appsettings*.json`.

The Web Gateway is the only browser OIDC client. Before starting the app hosts, provide:

```text
Authentication__Authority=https://localhost:44401
Authentication__ClientId=HCS_App
Authentication__ClientSecret=<secret>
DataProtection__Redis=localhost:6379
OpenIddict__Applications__HCS_App__ClientSecret=<same-secret>
```

`DataProtection__Redis` must be identical on `HCS.WebGateway` and `HCS.Blazor`. Service-to-service client IDs/secrets are listed in `.env.example`; seed only creates a confidential client when both values are provided.

### OpenIddict development certificate

```bash
dotnet dev-certs https --trust
```

Development uses the local ASP.NET Core development certificate. Production must mount an OpenIddict certificate outside the source tree and provide `AuthServer__CertificatePath` and `AuthServer__CertificatePassword` through runtime environment/secrets.

### Local infrastructure

Copy `.env.example` to an untracked `.env`, replace every `change-me`, then start PostgreSQL, Redis, RabbitMQ, and MinIO:

```bash
docker compose --env-file .env -f etc/docker-compose/local-infra.yml up -d
```

The existing BD lab Keycloak remains the preferred server at `http://localhost:5110`. If it is not running, add `--profile keycloak` to the command above to start the optional local Keycloak container on the same port (includes `KC_HOSTNAME=http://localhost:5110` for browser-safe OIDC URLs).

For Docker Compose runtime (`./scripts/docker-up.sh`), start Keycloak on the host first — see the SSO section in [`../../docs/runbooks/hcs-docker-compose-handoff.md`](../../docs/runbooks/hcs-docker-compose-handoff.md#keycloak--login-sso-bắt-buộc-trước-khi-test).

### Migration topology

The renamed layered projects are the Community foundation. Migration phases split them into:

- Applications: `HCS.AuthServer`, `HCS.WebGateway`, `HCS.Blazor`.
- Services: Platform, Organization, Document, WorkManagement and Collaboration.
- Infrastructure: PostgreSQL, Redis, RabbitMQ, MinIO and Keycloak.

The licensed source at `../HCS_web_with_license` is read-only migration input and must never become a project or package dependency.

Local HTTPS ports are AuthServer `44401`, Gateway/BFF `44402`, Blazor `44403`, Platform `44411`, Organization `44412`, Document `44413`, Work Management `44414`, Collaboration `44415`, and Bus Management `44416`.

Bus Management is a separate bounded context with database/schema `hcs_bus_management` and API prefix `/api/bus-management`. The browser uses the Gateway/BFF route; it must not call `https://localhost:44416` directly. The vertical slice and the Phase 2–4B implementation notes are in [`docs/bus-management-v1.md`](./docs/bus-management-v1.md).

Phase 2–4B currently includes server-side station scope, source-specific revenue validation, immutable issued/closed records, settlement/day-close advisory locking, closed-day adjustments with maker-checker approval, compliance expiry counts, server-side XLSX/PDF/HTML exports, parking tariff/session lifecycle with idempotent parking receipts, and parking spot/reservation operations. Phase 4A hardening adds a station-safe composite receipt FK `(ParkingSessionId, StationId)`, legacy parking backfill, UTC/business-date validation (including rejection of future exits), explicit transactions for parking mutations and daily close, and tariff/charge snapshot fields in DTOs and outbox events. Phase 4B adds station-safe parking spots, reservation overlap locking, reservation/session linking, spot occupancy uniqueness, and reservation outbox events. Export requires `HCS.BusManagement.Reports.Export`; the Blazor dashboard currently exposes revenue export links, while the other export types remain API routes. Migrations `20260829133927_AddBusPhaseFourParking`, `20260830023530_AddBusParkingOperations` and `20260830023907_AddParkingSpotOccupancyGuard` are present; focused Bus Management tests are `40 passed` and the full solution is `396 passed`. Runtime smoke covers applied PostgreSQL migration/schema, Bus health/Swagger, RabbitMQ startup, Keycloak OIDC discovery and the unauthenticated Gateway/BFF boundary. Authenticated role × permission × station scope and browser acceptance remain open; automated PostgreSQL concurrency/rollback coverage and authoritative departure-readiness validation remain release limitations. Browser acceptance is currently blocked because the local browser does not trust the internal Caddy CA. Details are recorded in [`docs/bus-management-v1.md`](./docs/bus-management-v1.md#migration-và-validation-evidence). The current branch is published at [Ndai1331/bus-management](https://github.com/Ndai1331/bus-management).

## Docker Compose: runtime mặc định

Docker Compose chạy toàn bộ chín HCS host, DbMigrator, PostgreSQL, Redis, RabbitMQ, MinIO và Caddy HTTPS proxy. Tạo `.env` từ `.env.k8s.example`, điền toàn bộ secret (bao gồm `HCS_ADMIN_PASSWORD`), sau đó:

```bash
./scripts/docker-up.sh
docker compose ps
```

Mở `https://hcs.localhost`. Nếu browser chưa tin local CA của Caddy, tin cậy certificate local một lần hoặc dùng môi trường có TLS certificate hợp lệ. Dừng nhưng giữ Docker volumes bằng `./scripts/docker-down.sh`.

Handoff triển khai và rollback: [`../../docs/runbooks/hcs-docker-compose-handoff.md`](../../docs/runbooks/hcs-docker-compose-handoff.md).

Production 2 server Ubuntu 24 (data + apps, Docker Hub, Nginx + Let's Encrypt): [`docs/runbooks/deploy-server.md`](./docs/runbooks/deploy-server.md) — **hướng dẫn deploy step-by-step**.

Chi tiết UFW/backup (reference): [`docs/runbooks/hcs-ubuntu24-two-server.md`](./docs/runbooks/hcs-ubuntu24-two-server.md).

## Browser sign-in and navigation

The browser entry point is `https://hcs.localhost`. The workspace root (`/`) requires an authenticated user. A direct visit to `/login` is allowed anonymously, but immediately starts the same BFF sign-in flow.

1. The Blazor client sends the browser to the configured `Bff:PublicOrigin` at `/bff/login`.
2. The gateway starts the OIDC challenge and, after a successful sign-in, returns the browser to the original HTTPS deep link.
3. The gateway accepts a return URL only when its origin is in `App:CorsOrigins`; an invalid or external return URL falls back to the configured UI origin.

The session id is held in a secure, HTTP-only BFF cookie; access tokens stay on the Gateway in Redis. Do not expose tokens to browser code or add credentials to application settings. For a manual smoke check, open `/`, `/login`, and a deep link such as `/chat` in a private window and confirm that sign-in returns to the requested in-app route.

The HCS header includes the notification action, a persistent `vi`/`en` culture selector, a permission-aware chat shortcut and an authenticated user menu. **Đăng xuất** calls the BFF `POST /bff/logout` endpoint through the existing antiforgery-aware client; browser code never handles an access token. The selector persists the ASP.NET Core request-culture cookie and reloads the current route.

The shell uses a two-row horizontal top menu above `1100px`; at and below `1100px` (including exactly `1100px`) it retains the permission-aware mobile drawer. The dominant palette is defined by the exact `--color-*` tokens in [`src/HCS.Blazor.Client/wwwroot/hcs-tokens.css`](./src/HCS.Blazor.Client/wwwroot/hcs-tokens.css); existing `--hcs-*` names remain compatibility aliases for page and component styles.

The main-menu entry **Trao đổi** (`/chat`) and the header chat shortcut require `Collaboration.Chat`. The page uses the free Collaboration contracts through the BFF and provides user/group creation, conversation list, paged messages, text/attachment sending, pin/rename/leave actions, member information, unread/read state, SignalR updates and REST retry fallback. User lookup is exposed as the least-privilege `GET /api/chat/contacts` Platform projection (`Id`, username, display name and active state), not the admin Identity list. Collaboration, MinIO, RabbitMQ/outbox and SignalR must be healthy for full attachment/realtime behavior; text/list UI still shows explicit loading, empty, forbidden and retry states when those dependencies are unavailable.

An authenticated user without the permission sees an access-denied view rather than another login redirect. After granting or revoking Collaboration permissions, sign out/in again so the BFF session receives fresh permission claims.

## Organization catalog CRUD (Blazorise)

The first free-license admin slice now has a shared Blazorise catalog shell and typed `HCS.Bff` client for:

- `/departments`, `/unit-lists`, and `/positions`;
- `/master-datas` plus `/document-types`, `/sectors`, `/urgency-levels`, `/confidentiality-levels`, `/processing-methods`, `/document-status`, `/signing-methods`, and `/event-types`.

The legacy `/even-types` route remains as a compatibility alias. Each page checks its matching `HCS.Organization.*` permission, uses server-side paging/filtering (`20` rows by default, `100` maximum), and keeps department/parent selection in typed dropdowns instead of accepting raw GUID input. The shared form uses allow-listed master-data type selects and Active/Inactive selects; a typed route locks its type to that route. The API contract remains unchanged.

For a local smoke check, sign in at `https://hcs.localhost`, open the routes above as an `admin`, and verify create → filter/page → edit → delete. Unsafe catalog calls are CSRF-checked once at the BFF; the internal Organization controllers deliberately do not require a second service-local antiforgery cookie/token. After rebuilding the gateway, hard-refresh or sign in again if the old antiforgery cookie is rejected. Re-login after changing role grants so the BFF session receives fresh permission claims. The detailed checklist is in [`docs/runbooks/hcs-admin-catalogs.md`](./docs/runbooks/hcs-admin-catalogs.md).

## User and role administration (Blazorise)

`/administration` (also `/users`) now provides the free-license user list with server-side paging, search/filter, create/edit modal tabs, role assignment, organization lookup, delete confirmation and typed Platform Identity API calls. The **Vai trò & quyền** action opens the role permission view backed by the standard Permission Management `R` provider. These routes are restricted to the local `admin` role; direct API calls still rely on Platform authorization and return `401/403` when the session lacks authority. The Identity Community contract does not expose `EmailConfirmed` and `ShouldChangePasswordOnNextLogin` in create/update DTOs, so those two screenshot fields are displayed read-only until a supported backend contract is approved.

Blazorise is pinned consistently to `2.3.0` in the client and host projects. It remains a production release blocker until the deploying organization records a valid license or the UI is replaced with an approved OSS stack. See [`docs/dependency-license-decisions.md`](./docs/dependency-license-decisions.md).

## Kubernetes: chỉ khôi phục khi cần

The deployable source is [`deploy/helm/hcs-community`](./deploy/helm/hcs-community). It contains the nine HCS hosts, the migration Job, PostgreSQL, Redis, RabbitMQ, MinIO, services and public/auth ingresses. The generic .NET image build is in [`deploy/docker/Dockerfile`](./deploy/docker/Dockerfile).

### First run

Prerequisites: Docker, `kubectl`, Helm 3, a Kubernetes cluster with an ingress controller, public DNS for two HTTPS hosts, and a TLS Secret in the target namespace. For local experimentation, install Kind and use `--kind`; it still needs an ingress controller, locally resolvable `*.localhost` hosts and a browser-trusted local TLS certificate.

1. Prepare secrets without adding them to Git:

   ```bash
   cd services/HCS_web_free_license
   cp .env.k8s.example .env
   chmod 600 .env
   ```

2. Set every placeholder in `.env`. `HCS_PUBLIC_HOST` is the Blazor/Gateway host and `HCS_AUTH_PUBLIC_HOST` is the AuthServer host. Both must be HTTPS and their shared parent domain must match `HCS_COOKIE_DOMAIN`. Create the TLS secret named by `HCS_TLS_SECRET` before deploy. Point `HCS_OPENIDDICT_PFX` and `HCS_DATAPROTECTION_PFX` at local PFX files; the script mounts them as Kubernetes secrets and never copies them into an image.

3. Configure the Keycloak realm/client once: external OIDC client `hcs-free-auth`, redirect URI `https://<HCS_AUTH_PUBLIC_HOST>/signin-oidc`, and the existing `bd-app-hcs` group gate/role mappings.

4. Run exactly one command:

   ```bash
   ./scripts/k8s-up.sh
   # or, for a local Kind cluster already prepared with ingress + TLS:
   ./scripts/k8s-up.sh --kind
   ```

The command builds ten images (nine hosts plus DbMigrator), creates or updates only the runtime/certificate secrets, deploys the Helm release, then waits for the migration Job and workloads. If a prior terminal was interrupted while Helm was waiting, the script automatically rolls back the pending Helm revision before upgrading; it preserves the namespace and all PVC data. It never reads or mutates `../HCS_web_with_license`.

### Later runs and operations

After code/configuration changes, run the same command again. Helm performs an upgrade and reruns the idempotent migration Job:

```bash
./scripts/k8s-up.sh
kubectl -n hcs get pods,svc,ingress
kubectl -n hcs logs deploy/web-gateway --tail=200
kubectl -n hcs logs job/db-migrator --tail=200
```

To change only deployment values (for example image registry tags), use the chart directly:

```bash
helm upgrade --install hcs ./deploy/helm/hcs-community -n hcs -f my-values.yaml
```

Do not use `kubectl delete namespace hcs` unless intentionally discarding all PostgreSQL/MinIO persistent data. Roll back an application release with `helm -n hcs history hcs` then `helm -n hcs rollback hcs <revision>`.

If an earlier version of the script left Helm at `pending-upgrade`, update the source then run the same `./scripts/k8s-up.sh --kind` command. For a one-off manual recovery, use `helm -n hcs rollback hcs <last-known-good-revision>`; do not delete the namespace merely to clear the Helm lock.
