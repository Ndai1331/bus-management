# Phase 01 — Contract và read query

**Status:** Completed
**Progress:** 100%

## Mục tiêu

Chuẩn hóa contract và query server-side cho một read model audit tập trung. Phase này chịu trách nhiệm bảo đảm dữ liệu đủ để tra cứu, an toàn khi hiển thị và không tạo thêm đường truy cập trực tiếp tới database/service.

## Context đã xác minh

- Contract: src/HCS.Application.Contracts/Auditing/AuditLogDtos.cs
- App service: src/HCS.Application/Auditing/AuditViewerAppService.cs
- Projection: src/HCS.Domain/Auditing/AuditRecordProjection.cs
- EF mapping/index: src/HCS.EntityFrameworkCore/EntityFrameworkCore/Auditing/HcsAuditProjectionModelBuilderExtensions.cs
- HTTP API: src/HCS.HttpApi/Controllers/Auditing/AuditViewerController.cs
- Gateway route: gateways/web/HCS.WebGateway/appsettings.json
- Capture event: building-blocks/HCS.IntegrationEvents/AuditEvents.cs

## Đã hoàn tất

- [x] Mở rộng `GetAuditLogsInput` theo hướng tương thích ngược với keyword tổng hợp, field filters, HTTP method/IP/browser/service/application/exception, URL; page mới dùng `EndTimeExclusive`, còn `EndTime` giữ semantics inclusive cũ.
- [x] Chuẩn hóa page size mặc định 20, giới hạn tuyệt đối 100, skip bound và normalize/trim độ dài input.
- [x] Bổ sung `ApplicationName` vào row DTO; resolver display name ưu tiên claim `name`, fallback given/family name rồi username/identity.
- [x] Allow-list sort theo status/duration/user/time/service/application và luôn thêm `Id` làm tie-breaker.
- [x] Query `AsNoTracking`/projection tối thiểu, count trước page, filter kết hợp và empty page an toàn.
- [x] Detail deserialize malformed JSON an toàn, log server-side có kiểm soát, sanitize exception và loại parameters khỏi response.
- [x] Giữ `HCS.AuditViewer` tại application service là server authority duy nhất; route/gateway hiện hữu không tạo vòng audit.

## Coverage và limitation đã chốt

MVP đọc projection hiện tại, bao phủ event từ Organization, Document, Work Management và Collaboration. Platform/AuthServer chỉ ghi `AbpAuditLogs`, chưa phát event vào projection nên không xuất hiện ở endpoint. Đây là follow-up coverage, không query nhiều database hoặc ghép dữ liệu trong browser.

Projection/event/query chưa mang `TenantId` và chưa có tenant predicate. Do đó phase này chỉ được vận hành trong local/single-tenant hoặc khi tenant isolation không nằm trong scope; multi-tenant phải bổ sung tenant propagation và server scope trước khi enable.

Kiểm tra RemoteIpAddress sau Caddy/YARP/forwarded headers. Chỉ tin proxy đã cấu hình; không dùng header client tùy ý làm IP xác thực. Smoke test phải xác nhận UserName là họ tên mong muốn, không phải subject/username. Nếu không đạt, bổ sung resolver claim/display name ở capture boundary và test backward compatibility.

## File dự kiến

- Sửa: src/HCS.Application.Contracts/Auditing/AuditLogDtos.cs
- Sửa: src/HCS.Application/Auditing/AuditViewerAppService.cs
- Có điều kiện: src/HCS.Domain/Auditing/AuditRecordProjection.cs
- Có điều kiện: src/HCS.EntityFrameworkCore/EntityFrameworkCore/Auditing/HcsAuditProjectionModelBuilderExtensions.cs
- Có điều kiện: migration và model snapshot tương ứng
- Test: test/HCS.EntityFrameworkCore.Tests/EntityFrameworkCore/PlatformFeatureTests.cs hoặc test project phù hợp
- Chỉ kiểm tra, không sửa nếu không cần: controller và gateway route

## Acceptance đã đạt

- [x] Filter kết hợp không làm mất điều kiện; page/sort ổn định và không vượt 100 row.
- [x] Tìm được theo user, IP, API, action, service, application, status, method, correlation và exception state.
- [x] Date range, null field, malformed detail JSON và record không có user đều có hành vi xác định.
- [x] Unauthorized/forbidden bị chặn ở server; không lộ secret hoặc raw exception.
- [x] Test projection/event deduplication và query/index pass.

## Rủi ro

Eventual consistency khiến record mới chưa xuất hiện ngay; UI phải thể hiện thời điểm refresh. Contains trên nhiều field có thể chậm khi volume lớn; cần đo trước khi thêm index hoặc full-text. Dữ liệu cũ có thể thiếu browser/display name/entity detail; hiển thị fallback thay vì bịa dữ liệu.
