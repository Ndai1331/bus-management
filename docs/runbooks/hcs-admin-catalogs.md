# HCS admin catalog CRUD smoke runbook

This runbook covers the approved Organization/catalog slice in the free-license runtime. It does not certify the deferred document, workflow, work-management, survey, signing, or reporting routes.

The same admin shell now includes `/administration` (or `/users`) for Identity user CRUD and a **Vai trò & quyền** modal for role permissions.

## Prerequisites

- Start the local Compose stack and open `https://hcs.localhost`.
- Sign in with an account mapped to the local `admin` role.
- Use a fresh BFF session after changing role permissions; sign out and sign in again.
- After rebuilding `web-gateway` or changing local Data Protection keys, hard-refresh or sign in again so the browser receives a new `.HCS.Bff.Antiforgery` cookie. Unsafe catalog requests are CSRF-protected by the BFF; the internal Organization API relies on the authenticated BFF boundary and does not require a second browser antiforgery token.

## Browser acceptance checklist

1. Confirm the menu only shows catalog links allowed by the current permission claims.
2. Open `/departments`, `/unit-lists`, `/positions`, and `/master-datas`.
3. Open each typed master-data route: `/document-types`, `/sectors`, `/urgency-levels`, `/confidentiality-levels`, `/processing-methods`, `/document-status`, `/signing-methods`, and `/event-types`. Confirm `/even-types` still resolves as an alias.
4. On each catalog, verify the empty, loading, retry/error, server paging, text filter, active-status filter, reset, create, edit, confirmation-delete, and success-message states.
5. For departments, confirm parent options display `Code — Name` and a self-parent choice is not available. For units, confirm the department dropdown displays `Code — Name` and no GUID input is shown.
6. On a typed master-data route, inspect the network request and confirm `type` is the fixed allow-listed value. On `/master-datas`, confirm a user can only choose one of the eight allow-listed values.
7. Submit a duplicate code and confirm the UI shows a friendly conflict message. Confirm invalid sign order is rejected outside `0–100`.
8. Remove one catalog permission, start a fresh session, and confirm the menu item is hidden, the direct route renders `NotAuthorized`, and the corresponding API returns `403`.
9. Repeat the critical list/form actions at desktop and mobile widths. The grid may scroll horizontally on narrow screens; action buttons must remain reachable and labelled.
10. Open `/administration`, verify user search and server paging, then create → assign role → edit → delete. Confirm duplicate username/email returns a friendly conflict message and a second click during save does not send a duplicate request.
11. Open **Vai trò & quyền**, select the permission group, toggle individual/all permissions, save, then sign out/in before checking the changed claims. Confirm a non-admin receives `403` from the Platform Identity/Permission APIs.
12. In the create/edit modal, verify the position and department values use `Code — Name`; do not enter GUIDs manually. The email-confirmed and force-password-change controls are intentionally read-only because the ABP Community Identity DTO does not accept those fields.

## Targeted verification commands

Run from `services/HCS_web_free_license`:

```bash
./scripts/audit-license-clean.sh
dotnet restore HCS.slnx --configfile NuGet.Config
dotnet build HCS.slnx --no-restore
dotnet test HCS.slnx --no-build
```

The UI client contract tests are in `gateways/web/HCS.WebGateway/HCS.WebGateway.Tests/OrganizationCatalogClientTests.cs` and `IdentityAdminClientTests.cs`. Record browser and Docker smoke results in the canonical plan before marking the phase complete.
