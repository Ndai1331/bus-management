---
title: Bus Management Phase 0–3 vertical slice
description: Synchronized implementation, verification evidence, and release limitations for the bus-management bounded context.
status: in-progress
priority: P1
effort: large
branch: codex/bus-management
tags: [bus-management, station-scope, finance, reconciliation, exports]
created: 2026-08-28
---

# Bus Management Phase 0–3

Status: **in-progress — implementation complete, release gates open**  
Progress: **90% (18/20 tracked items complete)**

## Phase status

| Phase | Status | Evidence / remaining gate |
|---|---|---|
| Phase 0 — service/runtime foundation | Completed | Service, schema, local port `44416`, permissions and Gateway/BFF route are present. Runtime smoke remains unverified. |
| Phase 1 — v1 operational slice | Completed | Server station scope, catalog, departure lifecycle, revenue/expense/settlement/daily close, outbox and audit hooks are implemented. |
| Phase 2A — expanded revenue | Completed (implementation) | Source snapshots, allow-list/reference validation, tariff snapshot checks and premises ownership constraint. |
| Phase 2B — adjustment and period lock | Completed (implementation) | Adjustment API/invariants, closed-day guard, maker-checker, base/adjustment/net totals, advisory locking and events/audit hooks. |
| Phase 2C — compliance and contracts | Completed (implementation) | Station ownership, vehicle-document renewal/update, legacy fallback and 30-day expiry warnings. |
| Phase 3A — server-side exports | Completed (implementation) | XLSX/PDF/HTML exports, server-side filtering/scope, permission gate and output limits. |
| Phase 3B — leadership dashboard | Completed (implementation) | Station breakdown, net totals, expiry counters and Gateway export links. |
| Release verification | In progress | Integration/API, live migration, infrastructure and browser smoke evidence are still missing. |

## Synced checklist

### Phase 0–1 baseline

- [x] Bounded-context service, PostgreSQL schema, local HTTPS configuration and BFF/YARP route.
- [x] Server-side station scope and station-assignment validity for reads and mutations.
- [x] v1 departure state machine and finance lifecycle.
- [x] Immutable issued/closed source records, outbox worker, audit hooks and v1 EF migration.

### Phase 2

- [x] Revenue source snapshots and source-specific server validation.
- [x] Adjustment create/list/approve flow with exactly-one-target and maker-checker rules.
- [x] Closed-day-only adjustments, source mutation guards, settlement refresh and advisory business-day lock.
- [x] Base/adjustment/net report and dashboard totals with station scope.
- [x] Compliance renewal/update, station ownership, legacy fallback and 30-day contract/document/lease warnings.

### Phase 3

- [x] Server-side revenue, departure, reconciliation and compliance exports in XLSX/PDF/HTML.
- [x] Export limits, filter/date/station scope and `HCS.BusManagement.Reports.Export` authorization.
- [x] Leadership dashboard station rows, net totals, expiry warnings and Gateway links.

### Verification

- [x] Bus-management tests: 19 passed, 0 failed.
- [x] Auth tests: 18 passed, 0 failed.
- [x] Gateway tests: 118 passed, 0 failed.
- [x] Full solution build: 0 warnings, 0 errors.
- [x] EF model-contract and outbox-contract tests pass.
- [x] Phase 2/3 migration files are present in the bus service.
- [ ] Application/API integration and live PostgreSQL migration/schema-drift verification.
- [ ] Keycloak/RabbitMQ/Gateway/browser runtime smoke verification.

The implementation work for Phases 0–3 is complete in the local working tree. The two unchecked items are release verification gates that require the local infrastructure to be running; they are intentionally not marked as passed from static build/test evidence.

## Mục tiêu

Tiếp tục vertical slice bến xe sau v1 bằng các khả năng nghiệp vụ còn thiếu nhưng có thể triển khai độc lập trong bounded context:

1. Phase 2 — nguồn thu mở rộng, adjustment sau chốt, gia hạn hồ sơ và cảnh báo hợp đồng/mặt bằng.
2. Phase 3 — báo cáo export server-side và dashboard tổng hợp theo từng bến, vẫn qua BFF/YARP và giữ station scope ở server.

Không tích hợp join database với Work/Document ở đợt này; chỉ giữ outbox event làm điểm nối. Không triển khai kế toán tổng hợp, VAT, công nợ, ngân hàng hoặc realtime vận hành.

## Baseline đã xác nhận

- Service hiện có tại `services/bus-management/HCS.BusManagementService`.
- PostgreSQL schema `hcs_bus_management`, migration v1 đã tồn tại.
- `RevenueReceipt` remains immutable after `Issued`; `AdjustmentEntry` now has application create/list/approve flow.
- Receipt stores source context and validates the source/reference contract server-side.
- Dashboard and compliance reports include station breakdown plus vehicle-document, carrier-contract and lease-expiry warnings.
- Server-side report exports now provide XLSX, PDF and printable HTML through the Gateway/BFF.

## Kết quả review kiến trúc trước implement

- `AdjustmentEntry` phải enforce đúng một target bằng domain rule và PostgreSQL check constraint; approval phải lưu approver/time và cấm creator approve.
- Mọi mutation receipt/expense/settlement phải dùng close guard; adjustment là mutation duy nhất được tạo cho ngày đã chốt.
- Hồ sơ xe mới lưu `StationId`; bản ghi legacy không có station chỉ được fallback qua departure khi xác định được duy nhất, không gán mơ hồ.
- Các báo cáo dùng ba cột rõ ràng: `BaseAmount`, `AdjustmentAmount`, `NetAmount`. `DailyClose` giữ snapshot bất biến tại thời điểm close, không back-write khi adjustment phát sinh sau đó.

## Thiết kế và phạm vi thực hiện

### Phase 2A — doanh thu mở rộng

- Mở rộng receipt với `SourceReference`, `VehiclePlateNumber`, `PremisesUnitId` để lưu snapshot ngữ cảnh phát sinh.
- Validate allow-list source và điều kiện tối thiểu ở server:
  - `FixedRoute`: phải tham chiếu chuyến hợp lệ, cùng bến; dùng tariff snapshot nếu có.
  - `VisitingVehicle`: phải có biển số/context.
  - `PublicBus`: phải có nhà xe.
  - `Parking`: phải có context lượt/biển số.
  - `Premises`: phải tham chiếu mặt bằng cùng bến.
  - `Other`: bắt buộc mô tả dòng thu.
- Giữ cùng receipt aggregate và outbox; chưa tách riêng parking/session aggregate khi chưa có yêu cầu vận hành chi tiết.

### Phase 2B — adjustment và khóa kỳ

- Hoàn thiện `AdjustmentEntry`: đúng một target receipt hoặc expense, số tiền khác 0, lý do, maker-checker khi approve.
- Thêm API create/list/approve dưới `/api/bus-management/reconciliation/adjustments`.
- Chỉ tạo adjustment cho ngày đã `DailyClose.Closed`; không sửa/xóa receipt/expense gốc.
- Dashboard và reports tính riêng base amount, adjustment amount và net amount.
- Phát outbox + audit cùng `SaveChanges` cho create/approve adjustment.
- Tách quyền tạo và duyệt adjustment; khóa advisory theo business day và khóa source mutation sau khi settlement submit.

### Phase 2C — compliance và hợp đồng

- Thêm endpoint renew/update hồ sơ pháp lý phương tiện; không tạo bản ghi trùng loại hồ sơ.
- Bắt buộc ownership station cho hồ sơ mới; legacy null ownership không được suy diễn khi có nhiều bến.
- Dashboard/report bổ sung cảnh báo trong 30 ngày cho hồ sơ xe, hợp đồng nhà xe và hợp đồng thuê mặt bằng; mọi số liệu đều station-scoped.
- Catalog nhà xe/tuyến/xe/tài xế có station ownership nullable để tương thích legacy; non-global user chỉ được tạo/truy vấn catalog thuộc assignment.

### Phase 3A — export server-side

- Thêm `GET /api/bus-management/exports/{reportType}?format=xlsx|pdf|html`.
- Export gọi query report server-side không bị giới hạn page UI, áp cùng filter/date/station scope.
- XLSX dùng Open XML; PDF là file PDF hợp lệ tạo server-side; HTML có bảng và CSS in.
- PDF multi-page; export giới hạn 366 ngày/100.000 dòng và URL UI encode đầy đủ query values.
- Chỉ cấp route cho `HCS.BusManagement.Reports.Export`; không expose token hay gọi service trực tiếp từ browser.

### Phase 3B — leadership dashboard

- Dashboard trả `StationRows` theo từng bến trong scope hiện tại.
- UI hiển thị breakdown theo bến, net revenue/expense, cảnh báo expiry và các link export qua Gateway.
- Giữ loading/error/empty state hiện có và không đưa logic tổng hợp tài chính về client.

## Kiểm thử và nghiệm thu

- Domain: adjustment target/maker-checker, source allow-list, renewal, settlement refresh and immutable source/close invariants are covered by the bus test project.
- EF model: model-contract tests cover ownership columns, source/adjustment constraints, composite premises FK and the intended receipt-line relationship.
- Outbox: contract tests cover departure/revenue/expense/reconciliation plus settlement and adjustment event names/payloads.
- Build: `dotnet build HCS_web_free_license.sln --no-restore --nologo` passed with 0 warnings and 0 errors in 1m16.84s.
- Focused tests: bus `19/19`, auth `18/18`, Gateway `118/118`; all passed with `--no-build --no-restore` after the successful build.
- Full-solution tests: `375 passed, 0 failed` with `--no-build --no-restore`; the test-runner also reports the expected no-test result for the non-test `HCS.TestBase` assembly.
- Application/API integration, live PostgreSQL migration/schema-drift, runtime infrastructure and browser smoke remain pending.

## Evidence and release limitations

- Business mutation paths add their outbox and mutation-audit records to the bus `DbContext` before `SaveChanges`; settlement and adjustment events are implemented.
- The generic HTTP audit middleware still creates a separate scope/context after the request UoW and suppresses persistence errors. HTTP audit durability/atomicity is therefore not release-verified.
- Departure readiness still accepts client-provided transport-order/control flags and only partially derives legal state from server records. Full authoritative legal validation and revalidation at departure remain a release gate.
- Keycloak group-to-local-role synchronization for bus roles is not implemented/verified; local bus-role seeding exists, but provisioning reconciliation remains open.
- `BusManagementDbContext` inherits directly from `DbContext`; optimistic concurrency rotation and live database migration behavior require explicit verification before release.
- No runtime smoke evidence was collected for PostgreSQL, Keycloak, RabbitMQ, Bus Management `:44416`, Gateway `:44402`, authenticated token forwarding, or browser export downloads.
- Final static checks: `dotnet ef migrations has-pending-model-changes` reports no pending model changes; `git diff --check` and `./scripts/audit-license-clean.sh` pass.
- Keep plan status `in-progress` until the two unchecked verification items and the listed release limitations are closed.

## Thứ tự thực thi

1. Contract/domain/EF migration.
2. Application service, outbox/audit, controllers.
3. Export service và dashboard DTO/UI.
4. Test, code review, docs, commit local trên `codex/bus-management`.
