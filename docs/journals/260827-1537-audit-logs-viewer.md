---
date: 2026-08-27
session: audit-logs-implementation-review
---

# Journal: 2026-08-27 — Audit Logs viewer

## Context

Reviewed the completed Audit Logs implementation in the current local working tree. The goal was a searchable admin read view for captured HTTP activity, without disturbing unrelated in-progress changes.

## What Happened

- Expanded the audit contract/query with combined server-side filters, keyword search, UTC date handling with an end-exclusive UI boundary, page-size limits, stable allow-listed sorting, and a minimal list projection.
- Added safer display-name resolution from claims, malformed-detail fallback, sanitized exceptions, and removal of action parameters from detail responses.
- Replaced the generic JSON panel with a typed BFF client and `/administration/audit-logs` Blazor page: advanced filters, paging, sorting, detail modal, retry/loading/empty states, vi/en localization, responsive layout, and keyboard/accessibility handling.
- Updated the four custom HTTP audit producers to use the display-name resolver and documented operation in [`hcs-admin-audit-logs.md`](../runbooks/hcs-admin-audit-logs.md).
- Finalization records report PASS for the license/secret audit, navigation and mobile checks, solution build/tests, implementation review, and `git diff --check`; the current diff check also passes.

## Reflection

The MVP is coherent and substantially safer than extending the former generic panel. The important boundary is explicit: this is a projection-backed viewer for captured activity, not a claim of complete native ABP audit coverage. The working tree remains uncommitted so unrelated user changes stay intact.

## Decisions Made

| Decision | Rationale | Impact |
|----------|-----------|--------|
| Read only `HcsAuditRecordProjections` through the BFF | Keep service/database access and query authority on the server | Results are eventual-consistent and coverage is limited to projected producers |
| Enforce `HCS.AuditViewer` at the application-service boundary | UI visibility is not a security control | Unauthorized requests receive 401/403; navigation remains in the admin shell |
| Exclude raw exceptions, parameters, bodies, tokens, cookies, and authorization headers | Audit data may contain PII or secrets | Detail remains a safe summary; full before/after property values are out of scope |
| Use end-exclusive dates and `Id` as a sort tie-breaker | Avoid boundary ambiguity and unstable paging | Client converts local input to UTC; repeated pages have deterministic order |

## Known Limitations

- Platform and AuthServer still write native `AbpAuditLogs`; they do not publish into this projection and are not backfilled. Current coverage is Organization, Document, Work Management, and Collaboration.
- No `TenantId` or tenant predicate exists; the MVP is limited to local/single-tenant use until tenant propagation and server isolation are added.
- Outbox/event-bus delivery is eventual. IP accuracy depends on trusted forwarded-header configuration; `TraceIdentifier` is not proven end-to-end. Authorization short-circuits, hubs, and background work may not be captured.
- Retention/archive, export, realtime streaming, and full entity property diffs remain backlog items; Document can legitimately emit both HTTP and business-level rows.

## Next Steps

- Decide whether full-system coverage requires Platform/AuthServer producers and a separate native-log backfill policy.
- Add tenant scoping before enabling multi-tenant operation, and verify proxy IP plus correlation propagation in deployed topology.

## Unresolved Questions

- What retention and PII policy will govern production audit data?
- Should permissioned non-admin auditors receive a dedicated navigation path outside the current admin shell?
