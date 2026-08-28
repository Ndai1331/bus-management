---
date: 2026-08-28
session: audit-logs-runtime-consumer-fix
---

# Journal: 2026-08-28 — Audit Logs runtime consumer fix

## Context

Runtime trace của trang Audit Logs trong Docker local cho thấy các service producer đã tạo và publish audit event thật, nhưng read model mà page truy vấn vẫn rỗng. Mục tiêu của thay đổi là wiring consumer RabbitMQ cho Platform và bảo đảm schema projection/inbox tồn tại trước khi consumer bắt đầu.

## What Happened

- **Root cause:** `HCS.PlatformService` chưa tham chiếu package `Volo.Abp.EventBus.RabbitMQ` và `HCSPlatformServiceModule` chưa khai báo `AbpEventBusRabbitMqModule`. `AuditRecordIntegrationEventHandler` có trong application assembly nhưng Platform không tạo consumer queue để nhận `hcs.audit.record.v1`; vì vậy `AbpEventInbox` và `HcsAuditRecordProjections` đều có `0` rows. Native `AbpAuditLogs` và producer outbox không tự động trở thành dữ liệu của projection page.
- **Package/module:** thêm `Volo.Abp.EventBus.RabbitMQ` phiên bản `10.6.0`, cập nhật packages lock và thêm `AbpEventBusRabbitMqModule` vào `[DependsOn]` của Platform.
- **Runtime config:** khai báo `RabbitMQ:EventBus:ClientName=HCS.PlatformService` và `ExchangeName=hcs` trong `services/platform/HCS.PlatformService/appsettings.json`, đồng thời truyền cùng hai giá trị qua service `platform` trong `docker-compose.yml` để local container dùng đúng identity/exchange với các producer.
- **Startup order:** `Program.Main` chạy `HCSDbContext.Database.MigrateAsync()` trước `InitializeApplicationAsync()`, sau đó mới `RunAsync()`. Như vậy bảng inbox/projection được tạo trước khi ABP module initialization khởi động event-bus consumer.
- **Regression guard:** thêm test phản chiếu `[DependsOn]` để yêu cầu `AbpEventBusRabbitMqModule`, đồng thời thêm project reference tới Platform trong test project.

## Runtime Verification

Trace trước fix ngày 2026-08-28 đã xác nhận:

- PostgreSQL, RabbitMQ, Platform, Organization, Document, Work Management, Collaboration, Gateway và Blazor đều running.
- `AbpAuditLogs` có `325` rows và `AbpAuditLogActions` có `356` rows; `AbpEventInbox` và `HcsAuditRecordProjections` đều `0` rows.
- Outbox đã publish event thật từ Organization/Document/Work Management/Collaboration lần lượt `1,299`/`1,964`/`1,282`/`21,752`, không có pending hoặc dead-letter.
- RabbitMQ có queue của bốn producer nhưng không có queue consumer tương ứng cho Platform; đây là dấu hiệu trực tiếp của module wiring bị thiếu.
- HTTP boundary vẫn đúng: request chưa đăng nhập tới page chuyển qua BFF login và `/api/audit-logs` trả `401`.

Post-fix runtime verification — **PASS**:

- Queue `HCS.PlatformService` hoạt động với `consumers=1`.
- `AbpEventInbox` có `2 handled`, `0 pending`; `HcsAuditRecordProjections` có `1` row.
- Republish cùng duplicate event không tăng projection; projection vẫn giữ `1` row.
- Platform build đạt `0 errors / 0 warnings`; targeted tests đạt `9/9`; full solution đạt `350 tests PASS`.
- License audit đạt `PASS`.

Như vậy event consumer, inbox handling, projection insert và deduplication đã được xác nhận trong runtime; page có read model sau khi event mới được consume.

## Reflection

Đây là lỗi integration/runtime chứ không phải lỗi filter, permission hay mapping của page. Việc đối chiếu đồng thời database, outbox và RabbitMQ đã tách được ba lớp dữ liệu: native audit, event delivery và read model. Sau khi bổ sung package/module/config và sửa startup order, runtime đã tạo consumer, xử lý inbox, ghi projection và giữ đúng một row khi republish duplicate. Dữ liệu lịch sử vẫn được giữ nguyên, không bù bằng cách join database hoặc chèn fake rows.

## Decisions Made

| Decision | Rationale | Impact |
|----------|-----------|--------|
| Dùng ABP RabbitMQ event bus cho Platform | Handler đã tồn tại và các producer cùng publish event contract `hcs.audit.record.v1` | Platform có thể consume projection event theo đúng topology hiện hữu |
| Migration DB trước `InitializeApplicationAsync()` | Consumer không nên chạy trước khi inbox/projection schema sẵn sàng | Startup Platform chờ migration thành công trước khi mở consumer/runtime |
| Chốt rõ `ClientName` và `ExchangeName` | Tránh Platform dùng identity/exchange mặc định khác với local producers | Queue/exchange runtime có tên ổn định để kiểm tra và vận hành |
| Không backfill hoặc đọc ghép `AbpAuditLogs` trong fix này | Tránh che khuất delivery gap và thay đổi scope read model | Lịch sử trước khi consumer tồn tại cần policy backfill riêng |

## Known Limitations

- Những event đã được producer đánh dấu published trước khi queue Platform tồn tại không được tự động backfill bởi thay đổi này; dữ liệu lịch sử có thể vẫn thiếu cho tới khi có policy replay/backfill riêng.
- Platform và AuthServer vẫn ghi native `AbpAuditLogs` nhưng chưa phát custom `AuditRecordCapturedEto` vào projection. Việc Platform có consumer không đồng nghĩa audit native của chính Platform/AuthServer xuất hiện trên page.
- Delivery và projection là eventual-consistent; sau request nghiệp vụ cần chờ outbox/RabbitMQ/consumer xử lý rồi refresh page.
- Projection hiện chưa có `TenantId` hoặc tenant predicate; chỉ phù hợp local/single-tenant cho tới khi bổ sung tenant propagation và server-side isolation.
- Document có thể phát cả HTTP audit và business-level audit cho cùng thao tác; không mặc định coi hai dòng gần nhau là duplicate.

## Next Steps

- Chốt chính sách historical replay/backfill cho các event đã published trước khi queue Platform tồn tại.
- Quyết định coverage tiếp theo cho Platform/AuthServer: phát cùng custom audit contract hay bổ sung nguồn native audit riêng.

## Unresolved Questions

- Có cần replay/backfill các event đã published trước khi Platform consumer được wiring không?
- Platform/AuthServer sẽ phát cùng custom audit contract hay page sẽ có thêm nguồn native audit riêng?
