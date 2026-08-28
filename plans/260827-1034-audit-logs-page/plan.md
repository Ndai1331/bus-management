---
title: "Audit logs tra cứu và chi tiết"
description: "Xây trang tra cứu nhật ký kiểm toán cho HCS với bộ lọc server-side, bảng dense và modal chi tiết qua BFF."
status: completed
progress: 100%
priority: P1
effort: 3-5d
branch: main
tags: [audit, backend, blazor, security, search, ui]
blockedBy: []
blocks: []
created: 2026-08-27
---

# Kế hoạch page Audit Logs — đã hoàn tất

## Phân biệt nguồn yêu cầu

- **Yêu cầu của user:** có trang để tra cứu audit logs theo họ tên/người dùng, IP, trình duyệt, thao tác, service, API và các thông tin liên quan.
- **Screenshot đính kèm:** chỉ là reference về bố cục bảng, badge HTTP status/method, nút **Chi tiết**, mật độ dữ liệu và thứ tự cột. Không dùng screenshot làm source of truth cho endpoint, schema hoặc quyền.

## Overview

Đã hoàn tất backend contract/query và page Blazor chuyên biệt qua BFF cho GET /api/audit-logs và GET /api/audit-logs/{id}. Gateway route hiện hữu được giữ nguyên.

UI đã thay thế panel JSON động bằng typed client, filter server-side, phân trang/sort ổn định, detail modal, localization vi/en và trạng thái loading/empty/error/retry.

Kế hoạch dùng projection làm read model tập trung. Blazor chỉ gọi BFF, không gọi trực tiếp service/database. Read model là eventual-consistent vì audit records đi qua outbox/event bus.

## Phạm vi MVP

- Bảng server-side paging/sorting với các cột: Chi tiết, HTTP status + method + API, người dùng, IP, thời gian, thời lượng, service, ứng dụng, correlation ID, dấu hiệu lỗi.
- Bộ lọc nhanh và bộ lọc nâng cao: keyword, user, thời gian, status, method, IP, action/API, service, application, correlation ID, có lỗi hay không.
- Modal chi tiết: request context, user/browser, action list, entity-change summary, exception đã sanitize và comments.
- Dùng permission HCS.AuditViewer nhất quán với backend; giữ menu hiện có cho admin.
- Loading, empty, error/retry, 401/403, stale/eventual-consistency và responsive behavior.

Không làm trong MVP: sửa/xóa log, realtime streaming, dashboard biểu đồ, export CSV/PDF, request/response body, token/cookie, hoặc full before/after entity-property diff.

## Phases

| Phase | Deliverable | Status |
|---|---|---|
| Phase 01 — Contract và read query | Contract lọc, projection mapping, query/index/security hardening | Completed |
| Phase 02 — Blazor page và detail | Typed BFF client, page, filter, table, modal, localization/CSS | Completed |
| Phase 03 — Test và smoke verification | Test matrix, build, review và verification handoff | Completed |
| Phase 04 — Runtime event consumer fix | Platform RabbitMQ consumer, inbox/projection runtime verification và duplicate handling | Completed |

Chi tiết: phase-01-audit-contract-and-query.md, phase-02-blazor-audit-page.md, phase-03-verification-and-documentation.md, phase-04-runtime-event-consumer-fix.md.

## Findings and dependencies

- Báo cáo backend: plans/reports/20260827-audit-logs-backend-review.md.
- Báo cáo UI: plans/reports/260827-blazor-audit-logs-ui-scan.md.
- Projection hiện nhận audit event từ Document, Organization, Work Management và Collaboration; Platform/AuthServer đang dùng ABP AbpAuditLogs nhưng chưa được project vào read model này.
- Client IP hiện lấy từ RemoteIpAddress; phải kiểm tra forwarded headers qua Caddy/YARP và chỉ trust proxy đã cấu hình.
- UserName cần smoke test để xác nhận là họ tên hiển thị thay vì subject/username. Nếu không đạt, bổ sung claim resolver ở phase 01.
- EntityChanges hiện chỉ là summary; before/after property values là backlog riêng vì có rủi ro PII/secret.

## Finalization status

- **Implementation:** Completed — backend query/contract, capture metadata, typed BFF client, Audit Logs page/detail, localization và responsive/accessibility states đã triển khai.
- **Validation:** PASS — code gates (license/secret audit, navigation/mobile checks, build, test, `git diff --check` và implementation review) cùng runtime evidence ngày 2026-08-28: queue `HCS.PlatformService` có `consumers=1`, `AbpEventInbox` có `2` handled/`0` pending, `HcsAuditRecordProjections` có `1` row, duplicate republish vẫn giữ projection `1`, Platform build `0/0`, targeted tests `9/9`, license PASS.
- **Acceptance:** Đạt toàn bộ MVP acceptance; bản vá runtime consumer/projection đã hoàn tất và duplicate handling đã được xác nhận.

## Known limitations and operating conditions

- **AuthServer/Platform:** projection hiện chỉ nhận custom audit event từ Organization, Document, Work Management và Collaboration. AuthServer và Platform vẫn ghi native `AbpAuditLogs`, chưa phát event vào `HcsAuditRecordProjections`; vì vậy log của hai hệ thống này không xuất hiện trong page. Không dùng fake data hoặc ghép database để che gap; đây là follow-up coverage riêng.
- **Multi-tenant:** event/projection/query hiện chưa có `TenantId` và chưa áp tenant predicate. MVP chỉ được coi là hợp lệ trong local/single-tenant hoặc môi trường đã chấp nhận không có tenant isolation. Trước khi bật multi-tenant phải bổ sung tenant context vào capture/event/projection và enforce scope ở server.
- **Consistency/network:** dữ liệu qua outbox/projection có eventual consistency; IP hiển thị dựa trên `RemoteIpAddress` và cần hạ tầng proxy/forwarded-header được cấu hình đúng.
- **Runtime wiring:** Đã hoàn tất module/consumer RabbitMQ cho Platform. Runtime hiện có queue `HCS.PlatformService` với `consumers=1`; `AbpEventInbox` xử lý `2` event với `0` pending và `HcsAuditRecordProjections` có `1` row. Duplicate republish giữ projection ở `1` row.

## Acceptance đã xác nhận

- [x] Filter kết hợp, server-side paging, page size tối đa 100 và allow-list sort/tie-break hoạt động.
- [x] Keyword/field filter, date end-exclusive, null/fallback, detail malformed JSON và exception sanitizer có hành vi xác định.
- [x] Server permission `HCS.AuditViewer`, BFF typed endpoint, 401/403/error/retry và không lộ secret/body/token được giữ đúng.
- [x] Page/detail, localization, responsive layout, keyboard/focus states và refresh eventual-consistency note đã hoàn tất.
- [x] Test/build/review/license/diff gates PASS; working tree ngoài plan/report được giữ nguyên.
- [x] Runtime trace xác nhận event đi đến `HcsAuditRecordProjections`; projection có `1` row và duplicate republish không tạo row trùng.

Nếu contract/index/schema thay đổi thì chạy migration Platform trước khi test query mới. Gateway route hiện có được giữ nguyên; không cần route mới. User/role phải được cấp HCS.AuditViewer.

## Success criteria

- Admin mở được /administration/audit-logs, tìm đúng record theo filter, đổi page/page size/sort và mở detail.
- API giới hạn 100 record/request, query có thứ tự ổn định, input được normalize/giới hạn và không lộ raw exception/body/token.
- Field trong screenshot được map rõ ràng; null hiển thị —, status có text + màu/icon, API/correlation dài không phá layout.
- Người không có HCS.AuditViewer nhận access denied/403; lỗi gateway/service có retry.
- Build/test/license audit đạt rule repository; smoke test qua https://hcs.localhost ở 375/768/1440px.

## Handoff

Đã chạy `/ck:cook --auto` trên plan này. Feature đã hoàn tất trong working tree; chưa stage, commit hoặc push để bảo toàn các thay đổi khác của user. Khi cần tạo commit riêng, dùng message gợi ý: `feat(auditing): add admin audit log viewer`.
