---
title: Bus Management Phase 4B parking operations
description: Station-scoped parking spots and reservation lifecycle integrated with parking sessions.
status: in-progress
priority: P1
effort: medium
branch: codex/bus-management
tags: [bus-management, parking, reservation, station-scope, outbox]
created: 2026-08-30
---

# Phase 4B — vị trí bãi đỗ và reservation

Status: **in-progress — implementation complete; runtime/release gates open**
Progress: **implementation complete**

## Outcome

Extend the Phase 4A parking revenue slice with operational inventory:

```text
Parking spot → reservation window → check-in → parking session → close/receipt
```

The feature stays behind the Gateway/BFF, uses the existing parking permission family, and keeps every lookup/mutation station-scoped on the server.

## Implemented scope

- `ParkingSpot` stores station, unique code, display name, optional vehicle-type restriction and active state.
- `ParkingReservation` stores spot, normalized plate, vehicle type, UTC window, note, creator and lifecycle:
  `Reserved → CheckedIn → Completed`, with cancellation from `Reserved` or `CheckedIn`.
- Reservation creation serializes overlap checks with a PostgreSQL advisory lock per station. It rejects overlap on either the same spot or the same vehicle plate.
- Parking sessions may reference a spot/reservation. A reservation check-in and session mutation are committed together; closing completes the reservation and cancelling the session cancels it.
- Database FKs use `(Id, StationId)` alternate keys to prevent cross-station references. A partial unique index prevents two open sessions on one spot.
- Reservation mutations emit versioned outbox events and mutation audit records in the same DbContext save boundary.
- APIs:
  - `GET/POST/PUT /api/bus-management/revenue/parking/spots`
  - `GET/POST /api/bus-management/revenue/parking/reservations`
  - `POST /api/bus-management/revenue/parking/reservations/{id}/check-in`
  - `POST /api/bus-management/revenue/parking/reservations/{id}/cancel`
  - `POST /api/bus-management/revenue/parking/reservations/{id}/complete`

## Verification

- Focused Bus Management tests cover UTC assignment values, reservation lifecycle/time invariants, API permission boundaries, EF indexes/FKs, event payload and parking-spot occupancy guard; the current focused run is `40 passed, 0 failed`.
- Migration `20260830023530_AddBusParkingOperations` creates spot/reservation tables and session links and is applied in local PostgreSQL.
- Migration `20260830023907_AddParkingSpotOccupancyGuard` adds the partial unique open-session/spot index and is applied in local PostgreSQL.
- Live migration/schema validation and unauthenticated service/Gateway smoke passed. Authenticated role × permission × station acceptance and browser acceptance remain runtime gates; automated PostgreSQL concurrency/rollback coverage and authoritative departure-readiness validation remain release limitations.

## Deliberate non-goals

Barrier/camera integrations, occupancy-map visualization, payment gateway, QR, refunds, VAT and realtime notifications remain later phases. Reservation availability is calculated from server-side reservations/sessions; no client-side authority is introduced.
