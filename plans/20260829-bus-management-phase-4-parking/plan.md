---
title: Bus Management Phase 4A parking session vertical slice
description: Add station-scoped parking tariff and parking-session lifecycle with immutable revenue receipt integration.
status: in-progress
priority: P1
effort: medium
branch: codex/bus-management
tags: [bus-management, parking, revenue, station-scope, outbox]
created: 2026-08-29
---

# Phase 4A — phiên bãi đỗ và thu phí

## Outcome

Implement the next bounded-context slice after Phase 2–3:

```text
Parking tariff → vehicle enters → active session → close session
                                             ↓
                                  Parking revenue receipt
                                             ↓
                                  shift settlement/report
```

The slice remains behind the existing Gateway/BFF, uses the existing `Revenue` permission family, and does not introduce accounting, VAT, debt, payment-gateway or real-time requirements.

## Scope

### Domain and persistence

- Add `ParkingTariff` with station, vehicle type, billing-unit minutes, rate per unit, minimum charge, calculation description, effective dates and active flag.
- Add `ParkingSession` with station/business date/shift, vehicle plate/type, arrival and exit UTC timestamps, tariff ID plus snapshots of vehicle type, billing-unit minutes, rate, minimum charge and description, duration, calculated charge and lifecycle status. Receipt linkage is resolved through the receipt-side FK to avoid a circular FK during close.
- Session states: `Open → Closed` or `Open → Cancelled`.
- Resolve and store the complete tariff snapshot at arrival; later tariff edits must not change an active or closed session.
- A closed session is immutable. A cancelled session cannot generate revenue.
- Add nullable `ParkingSessionId` to `RevenueReceipt`, a unique one-to-one relationship/index and a PostgreSQL check requiring it for new `SourceType = Parking` receipts (non-Parking receipts must have it null). `IsLegacyParking` is backfilled for historical parking receipts without a session so startup migration remains non-blocking; the domain/API never creates new unlinked parking receipts.
- Add indexes for station/date/status, open vehicle lookup and tariff effective-date lookup. Foreign keys must use restrictive delete behavior for financial history.

### Application/API

- `GET/POST /api/bus-management/revenue/parking/tariffs`
- `GET/POST /api/bus-management/revenue/parking/sessions`
- `POST /api/bus-management/revenue/parking/sessions/{id}/close`
- `POST /api/bus-management/revenue/parking/sessions/{id}/cancel`
- Enforce active station assignment and `Revenue`/`Revenue.Create`/`Revenue.Update` policies.
- Reject a second open session for the same station and vehicle plate on the same business day.
- Resolve a tariff by explicit ID or deterministic exact vehicle-type match at arrival; reject missing or ambiguous tariff configuration.
- Close validates that `ArrivalUtc` maps to the supplied business date in the station timezone, calculates `ceil(duration minutes / billing unit) × rate`, applies minimum charge, rounds to whole VND, and creates one issued `Parking` receipt with source reference, plate, shift and tariff description snapshot.
- Close must begin/participate in one explicit transactional unit of work covering advisory lock, reload, validation, session transition, receipt creation, outbox and save/commit. It calls the existing open-day guard and PostgreSQL business-day advisory lock before any mutation. A concurrent close waits on the lock, observes the committed closed session and returns its receipt; a unique-link conflict is treated as an idempotent retry only after reloading the committed session, never by issuing a second receipt.
- Add revenue outbox event for the generated receipt and audit through the existing mutation path.
- List endpoints support server-side filter, date, status, plate, paging and station scope.

### Contract/UI/docs

- Add typed contracts and event/payload fields without exposing service-direct URLs.
- Add central permission constants, definitions, localization and role seed behavior. Operations and station-manager may operate sessions; leadership remains read-only; accounting can read/report but does not receive station scope expansion.
- Add focused domain, model, idempotency and permission-contract tests. Serialize tariff overlap checks with a station-wide PostgreSQL advisory lock. Add PostgreSQL integration coverage when local infrastructure is available for concurrent close, retry/rollback, check constraints and advisory lock; in-memory tests must not be described as proof of those database guarantees.
- Keep UI change minimal: add parking session/tariff API surface and a dashboard link only if the existing BFF client pattern supports it without introducing a second direct-service path.
- Update the Phase 4 documentation and plan status with validation evidence.

## Non-goals

- Detailed parking occupancy map, spot allocation, reservation, barriers/cameras, QR/payment integration, refunds, VAT, receivables, bank reconciliation or realtime SignalR.
- Changing generic receipt edit/void behavior or rewriting the existing settlement workflow.
- Cross-database joins with Work/Document; future integration remains event/read-model based.

## Verification

- Unit tests for tariff selection, duration rounding, minimum charge and session state invariants.
- Service tests for station scope on every ID lookup/mutation, inactive station, duplicate open session, closed-day guard, source snapshot, post-arrival tariff edits and close idempotency.
- EF model tests for indexes, one-to-one parking receipt link, numeric/UTC/date mapping, foreign keys and the two-way source check constraint.
- PostgreSQL integration tests for concurrent close, retry after rollback/partial failure and advisory-lock serialization when the local database is running.
- Build, focused bus tests and full solution tests.
- EF `has-pending-model-changes`, `git diff --check` and license/secret audit.
- Runtime PostgreSQL/Gateway/browser smoke remains a release gate when local infrastructure is available.

## Implementation order

1. Contracts and domain invariants.
2. EF model and migration.
3. Application service and controllers.
4. Permissions/localization and optional BFF/UI surface.
5. Tests, docs, review and push.

## Implementation status

- [x] Contracts, domain invariants, station-scoped tariff/session APIs and parking receipt integration.
- [x] EF migration `20260829133927_AddBusPhaseFourParking` with legacy parking backfill, composite station-safe receipt FK, unique open-session and parking-receipt constraints.
- [x] Outbox event, permission/localization catalog and role seed integration.
- [x] Focused tests: 40 passed; full solution test count is refreshed by the Phase 4B verification.
- [x] Build: 0 warning/0 error; EF pending-model: no changes; diff check and license/secret audit.
- [x] PostgreSQL migration apply, service health, RabbitMQ startup, Keycloak OIDC discovery and unauthenticated Gateway/BFF route smoke.
- [ ] PostgreSQL concurrent close/rollback integration and authenticated Gateway/Keycloak/browser acceptance.

The Phase 4A implementation is complete in the working tree. Phase 4B continues with parking spots and reservations; authenticated role/station acceptance, browser certificate trust, and PostgreSQL concurrent close/rollback integration are still open.
