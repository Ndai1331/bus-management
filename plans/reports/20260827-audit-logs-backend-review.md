# Rà soát backend audit logs — chuẩn bị page tra cứu

## Kết luận nhanh

Viewer hiện đọc **chỉ** `HcsAuditRecordProjections`, không đọc trực tiếp `AbpAuditLogs`. Projection có dữ liệu đủ cho một bảng HTTP cơ bản, nhưng chưa đủ cho audit viewer hợp nhất/toàn hệ thống. Rủi ro lớn nhất là feed vào projection chưa được chứng minh chạy runtime: Platform có native ABP audit nhưng không thấy producer `AuditRecordCapturedEto` hoặc module RabbitMQ consumer.

## Dữ liệu hiện có / còn thiếu

| Nhu cầu | Hiện trạng | Ghi chú |
|---|---|---|
| Họ tên | Có `UserName` | Lấy từ `HttpContext.User.Identity.Name`, không có snapshot display/full name. Collaboration đặt `NameClaimType = sub`, nên có thể hiện subject, không phải họ tên. |
| User ID | Có | Parse từ `NameIdentifier` hoặc `sub`; không có FK/join tới Identity để resolve tên hiện tại. |
| IP | Có | Lấy `RemoteIpAddress`; chưa có `UseForwardedHeaders`, nên qua BFF/proxy có thể là IP proxy/container. |
| Browser | Có ở detail | Raw User-Agent, không có ở list; chưa chuẩn hóa browser/device. |
| Thao tác | Có nhưng không đồng nhất | Middleware ghi endpoint/HTTP; Document có thêm 6 business actions (`Created/Updated/Assigned/Submitted/Sent/Revoked`). Workflow/signing/chat hub chủ yếu chỉ có HTTP hoặc không có log. |
| Service | Có | 4 custom producer: Organization, Document, WorkManagement, Collaboration. Platform/AuthServer/Blazor không phát ETO này. Tên service là hard-code. |
| API | Có path + method | `Url` là `Request.Path`, không gồm query string/host; action endpoint phụ thuộc `Endpoint.DisplayName`. |
| Status | Có | Request throw exception bị ép thành `500`; request bị Authorization short-circuit trước middleware không được capture. |
| Thời gian/duration | Có | UTC `DateTime` và `ElapsedMilliseconds`; contract chưa ghi rõ đơn vị/timezone. |
| Correlation | Có | Lấy `TraceIdentifier`; chưa có test chứng minh giữ cùng ID qua Gateway → service → event. |
| Chi tiết entity | Có một phần | Projection chỉ lưu metadata entity change JSON; không có `EntityPropertyChanges` old/new như bảng native ABP. |
| Exception | Có nhưng đã sanitize | Viewer nhận thông báo ổn định; raw exception vẫn tồn tại ở log server/native audit. |

Native schema trong migration đã có trường phong phú hơn (`ClientName`, `ClientId`, impersonator, tenant, browser, actions, entity/property changes), nhưng chưa được map vào `AuditViewerAppService` và migration projection không có backfill từ `AbpAuditLogs`.

## Rủi ro cần xử lý trước khi làm page

### Cao

1. **Projection có thể rỗng trong runtime.** Các producer service phụ thuộc `AbpEventBusRabbitMqModule`, còn `HCSPlatformServiceModule` không phụ thuộc module này; Platform chỉ cấu hình inbox/outbox DbContext. Cần xác minh Rabbit consumer và event đến `AuditRecordIntegrationEventHandler`. Test hiện tại gọi handler trực tiếp nên chưa bắt lỗi wiring runtime.
2. **Không phải audit toàn hệ thống.** Platform chỉ dùng `UseAuditing` native; AuthServer/Blazor cũng không thấy producer custom. `/hubs/chat`, background worker/consumer và các action không đi qua `/api` không được middleware HTTP ghi nhận.
3. **IP có khả năng sai ngữ nghĩa.** Kiến trúc dùng BFF/YARP nhưng capture đọc `RemoteIpAddress` và không thấy xử lý forwarded headers.
4. **Semantics/độ bền không nhất quán.** Organization/Document dùng transaction chung: audit outbox lỗi sẽ rollback/block business request. Work/Collaboration ghi audit ở scope riêng sau request, lỗi bị nuốt và tạo audit gap. Document write còn có thể tạo 2 dòng (business `AddAudit` + generic HTTP middleware).

### Vừa/thấp

- Query hiện chỉ filter UserId, exact UserName, time, status, correlation và `Action.Contains`; thiếu service, API/method, IP, browser, duration, exception, entity/action detail.
- `Action.Contains` thường thành `LIKE '%term%'`, không tận dụng B-tree index; chưa có composite index theo các tổ hợp filter + `ExecutionTime`.
- List load entity đầy đủ, gồm các cột JSON/text detail, rồi mới map; chưa `AsNoTracking`/projection cột tối thiểu. Có count query và data query riêng.
- Sort chỉ hiểu chính xác `ExecutionTime ASC`, mặc định mọi giá trị khác là DESC; không có tie-breaker `Id`, nên pagination có thể nhảy khi nhiều record cùng timestamp.
- `StartTime/EndTime` không normalize UTC/validate thứ tự; `EndTime` dùng inclusive `<=` dễ gây lỗi biên.
- `ActionsJson`/`EntityChangesJson` là `text`, không thể query/index field con. Không có retention/partition/archive dù mọi GET `/api` đều tạo audit.
- Permission backend là một quyền toàn cục `HCS.AuditViewer`; không có scope tenant/service hoặc quyền riêng list/detail/export. Projection không có `TenantId` dù native audit có tenant fields.
- UI hiện khóa route bằng role `admin`, backend khóa bằng `HCS.AuditViewer`; admin thiếu permission sẽ 403, auditor có permission nhưng không có role admin không mở được page. Controller không gắn `[Authorize]` trực tiếp, nên cần integration test endpoint, dù app-service interceptor hiện là lớp bảo vệ dự kiến.

## Files có khả năng sửa

- Contract/query: `src/HCS.Application.Contracts/Auditing/AuditLogDtos.cs`, `IAuditViewerAppService.cs`, `src/HCS.Application/Auditing/AuditViewerAppService.cs`, `src/HCS.HttpApi/Controllers/Auditing/AuditViewerController.cs`.
- Data/event: `src/HCS.Domain/Auditing/AuditRecordProjection.cs`, `building-blocks/HCS.IntegrationEvents/AuditEvents.cs`, `src/HCS.Application/Auditing/AuditRecordIntegrationEventHandler.cs`.
- EF: `src/HCS.EntityFrameworkCore/EntityFrameworkCore/Auditing/HcsAuditProjectionModelBuilderExtensions.cs`, `HCSDbContext.cs`, thêm migration mới; không sửa migration cũ.
- Capture/wiring: bốn `services/*/Integration/HttpAuditOutboxMiddleware.cs`, các `OutboxInbox.cs`, `services/platform/HCS.PlatformService/HCSPlatformServiceModule.cs` và `.csproj`; nên cân nhắc helper capture dùng chung.
- Business audit: `services/document/HCS.DocumentService/Documents/DocumentAppService.cs`, thêm workflow/signing nếu cần business-level action và quyết định cách loại duplicate HTTP row.
- UI: `src/HCS.Blazor.Client/Pages/AdministrationFeature.razor` hiện chỉ là generic `GatewayDataPanel`; cần client/model/page detail riêng. Menu đã có route.

## Test cases nên bổ sung

- Anonymous, user không có quyền, user chỉ có `HCS.AuditViewer`, admin không có quyền; kiểm tra cả hai route và detail endpoint trả đúng 401/403/404.
- Runtime RabbitMQ: phát ETO từ từng service, xác nhận projection insert; retry/duplicate/concurrent delivery không tạo duplicate; dead-letter/outbox backlog có quan sát được.
- Platform API và native `AbpAuditLogs`: xác nhận record có/không xuất hiện trong projection theo thiết kế; kiểm tra migration không làm mất log cũ.
- Capture 2xx/4xx/5xx, exception mapped status, authorization 401/403, timeout/cancel; xác nhận duration, correlation, user, IP, UA và URL.
- Request qua BFF/Caddy với forwarded headers; xác nhận IP client được chọn theo allow-list proxy tin cậy.
- Document business mutation: xác định expected một hay hai record; failure phải vừa giữ/rollback business vừa có failure audit theo policy.
- Query boundary: UTC/local timezone, `StartTime == EndTime`, end exclusive, start > end, null/long filters, special LIKE characters, all status/service/action filters.
- Paging/sort: max page size, page beyond end, same `ExecutionTime`, stable order across repeated requests; dùng SQL `EXPLAIN` trên dataset lớn.
- Detail: malformed JSON, large actions/entity changes, sanitized exception, no raw parameters/secrets; entity property old/new nếu page yêu cầu.
- Hub/background actions: chat SignalR, outbox consumer và scheduled worker phải có quyết định audit rõ ràng.

## Verification

- `HCS.EntityFrameworkCore.Tests`: 2 audit projection/query tests pass.
- `HCS.OrganizationService.Tests`: 1 transaction/audit-outbox rollback test pass.
- Không sửa source code; chỉ thêm báo cáo này.

**Status:** DONE_WITH_CONCERNS

**Summary:** Có nền tảng DTO/API/projection và capture cho 4 service, nhưng page chưa thể coi là audit toàn hệ thống cho tới khi chốt/fix event wiring Platform, coverage/auth-failure/IP semantics, tenant/permission scope và query/index strategy.
