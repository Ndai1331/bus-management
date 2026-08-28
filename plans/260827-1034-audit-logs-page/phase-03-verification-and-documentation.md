# Phase 03 — Verification và documentation

**Status:** Completed
**Progress:** 100%

## Mục tiêu

Chứng minh page tra cứu đúng dữ liệu, đúng quyền, không lộ thông tin nhạy cảm và không làm hỏng các thay đổi đang có trong working tree.

## Automated checks — PASS (code-level)

Kết quả finalization:

- [x] `./scripts/audit-license-clean.sh` — PASS.
- [x] `./scripts/audit-navigation-layout.sh` — PASS.
- [x] `./scripts/audit-mobile-layout.sh` — PASS.
- [x] `dotnet build HCS.slnx --no-restore` — PASS.
- [x] `dotnet test HCS.slnx --no-build` — PASS.
- [x] `git diff --check` — PASS.
- [x] Implementation/code review — PASS; không còn blocker trong phạm vi MVP.

Không reset, stash hoặc ghi đè các thay đổi không thuộc feature audit logs.

Runtime RabbitMQ consumer được kiểm tra riêng trong Phase 04; các gate code-level vẫn giữ vai trò regression cho contract/query/page.

## Test matrix backend

- Default page size, page size 20/50/100 và giá trị vượt 100.
- Keyword ở user, user id, IP, browser, URL, action, service, application và correlation.
- Filter exact status/method/service/application/exception; nhiều filter kết hợp.
- Start inclusive/end exclusive, UTC conversion, start lớn hơn end và date null.
- Sort tăng/giảm ở các field allow-list; tie-break bằng Id; page boundary không trùng/mất record.
- Empty result và page vượt tổng số.
- User có display name, chỉ có username, chỉ có user id và anonymous request.
- Detail có action/entity JSON hợp lệ, JSON rỗng và JSON malformed.
- Exception được sanitize; không trả token/cookie/body/header nhạy cảm.
- 401/403, correlation id không hợp lệ, chuỗi quá dài, ký tự đặc biệt và retry không gây lỗi query.
- Event deduplication và eventual consistency của outbox/projection.
- Gateway route /api/audit-logs và /api/audit-logs/{id} giữ đúng upstream.

Ưu tiên unit/integration test cho input normalization, query predicate, mapping, detail fallback và permission; bổ sung migration/model test nếu index/schema thay đổi.

## Runtime smoke test — PASS (local runtime, 2026-08-28)

HTTP route/auth đã được kiểm tra: page chưa đăng nhập redirect về BFF login và `/api/audit-logs` trả `401`. Baseline trước fix cho thấy native audit và producer outbox có dữ liệu thật, nhưng page read model chưa có dữ liệu:

1. `hcs_identity."AbpAuditLogs"` có 325 rows; `AbpAuditLogActions` có 356 rows.
2. Organization/Document/Work Management/Collaboration đã publish lần lượt 1,299/1,964/1,282/21,752 audit events; pending/dead-letter đều 0.
3. `hcs_identity."AbpEventInbox"` và `hcs_identity."HcsAuditRecordProjections"` đều 0 rows.
4. RabbitMQ có queue của bốn producer nhưng không có queue consumer tương ứng cho Platform.
5. Outbox payload đã có request query/CRUD (`GET`, `POST`, `PUT`, `DELETE`) cùng user, IP, API path, service và application, nhưng chưa được project vào page.

- [x] Sau fix, queue `HCS.PlatformService` có `consumers=1`; một smoke event hợp lệ đi qua producer outbox → RabbitMQ → `AbpEventInbox` (`2 handled`, `0 pending`) → `HcsAuditRecordProjections` (`1 row`). Republish cùng event ID giữ projection ở `1 row`.
- [x] Auth boundary vẫn đúng: anonymous page redirect về BFF login và anonymous `/api/audit-logs` trả `401`; projection đã sẵn sàng cho authenticated viewer.
- [ ] Interactive authenticated BFF/page query chưa chạy trong smoke vì không có session đăng nhập; cần account có `HCS.AuditViewer` nếu muốn xác nhận UI sau restart.
- [x] AuthServer/Platform coverage gap và không dùng fake data được xác nhận.
- [x] Multi-tenant condition được ghi nhận: chưa có `TenantId`/tenant predicate, chỉ vận hành single-tenant/local cho MVP.

Chi tiết evidence và root cause: `plans/reports/20260828-audit-logs-runtime-trace.md`.

## Documentation handoff

Nếu triển khai thật, tạo hoặc cập nhật runbook tại docs/runbooks/hcs-admin-audit-logs.md với:

- route, permission và các service đang được capture;
- định nghĩa từng cột/filter, timezone và eventual-consistency delay;
- fallback khi user/browser/IP thiếu;
- cách kiểm tra outbox/projection, 401/403 và gateway;
- retention/PII guidance và các field tuyệt đối không hiển thị;
- backlog bao phủ Platform/AuthServer, export, retention và entity before/after nếu cần.

Cập nhật roadmap/changelog chỉ khi repository đã có tài liệu tương ứng; trong task này chỉ sync plan và report liên quan, không sửa product docs/source code.

## Acceptance đã đạt

- [x] Automated checks, build, test, review và license audit có kết quả PASS ở code-level.
- [x] Runtime smoke xác nhận queue, inbox, projection và duplicate handling sau khi wiring consumer.
- [ ] Interactive authenticated page smoke cho quyền, filter/paging/sort/detail cần chạy với BFF session thật.
- [x] Có evidence từ producer outbox thật và smoke event đi qua dispatcher; không chèn trực tiếp fake row vào projection để che delivery gap.
- [x] Working tree bảo toàn thay đổi trước đó; report nêu rõ limitation AuthServer/Platform và multi-tenant condition.
