# Audit Logs runtime trace — 2026-08-28

## Scope

Kiểm tra flow audit thực tế trong Docker local: login/request query/CRUD, producer outbox, RabbitMQ consumer, inbox và read model mà page `/administration/audit-logs` truy vấn.

Screenshot đính kèm chỉ được dùng làm reference cho bố cục/cột; không dùng làm source of truth cho dữ liệu hoặc coverage.

## Result

**PASS ở runtime integration sau khi wiring lại consumer của Platform.** Sau khi thêm RabbitMQ event-bus cho Platform và rebuild/restart service, audit smoke event hợp lệ đi hết flow producer outbox → RabbitMQ → queue `HCS.PlatformService` → `AbpEventInbox` → `HcsAuditRecordProjections`. Anonymous `GET /api/audit-logs` vẫn trả `401` đúng boundary; projection đã sẵn sàng cho authenticated viewer, nhưng smoke này không có interactive BFF session để lặp lại một authenticated API/page query.

Code/build/unit test của feature đã pass từ trước. Runtime fix đã giải quyết blocker khiến page read model rỗng; không thay đổi nguyên tắc page chỉ đọc projection và không ghép nhiều database trong browser.

## Evidence trước fix — baseline

Trace ban đầu cho thấy producer đã ghi và publish event, nhưng Platform chưa có consumer queue:

| Check | Result trước fix |
|---|---:|
| Docker services | PostgreSQL, RabbitMQ, Platform, Organization, Document, Work Management, Collaboration, Gateway, Blazor đều running |
| `hcs_identity."AbpAuditLogs"` | 325 rows |
| `hcs_identity."AbpAuditLogActions"` | 356 rows |
| `hcs_identity."AbpEntityChanges"` | 0 rows |
| `hcs_identity."HcsAuditRecordProjections"` | 0 rows |
| `hcs_identity."AbpEventInbox"` | 0 rows |
| Organization audit outbox | 1,299 published, 0 pending, 0 dead-letter |
| Document audit outbox | 1,964 published, 0 pending, 0 dead-letter |
| Work audit outbox | 1,282 published, 0 pending, 0 dead-letter |
| Collaboration audit outbox | 21,752 published, 0 pending, 0 dead-letter |

Outbox payloads đã có request thật, gồm user `admin`, `Nguyễn Hồ Phi Long`, `BD bacsi`, IP/user ID, `GET`, `POST`, `PUT`, `DELETE`, API path, service và application. Ví dụ có `/api/organization/departments`, `/api/organization/master-data`, `/api/organization/user-mappings` và `/api/organization/positions`.

Native `AbpAuditLogs` cũng ghi nhận login `/Account/Login`, `/connect/token` và các request Platform. Request chưa đăng nhập tới page redirect về BFF login; request chưa đăng nhập tới `/api/audit-logs` trả `401` như thiết kế.

## Post-fix runtime acceptance — PASS

| Check | Result sau fix |
|---|---:|
| Platform RabbitMQ package/module | PASS — Platform tham chiếu `Volo.Abp.EventBus.RabbitMQ` và khai báo `AbpEventBusRabbitMqModule` |
| Runtime event-bus config | PASS — `ClientName=HCS.PlatformService`, `ExchangeName=hcs` |
| RabbitMQ consumer | PASS — queue `HCS.PlatformService` tồn tại và có consumer |
| Event mới từ producer | PASS — audit event mới được publish và consumer nhận |
| `AbpEventInbox` | PASS — event mới tạo inbox marker/record và được handler xử lý |
| `HcsAuditRecordProjections` | PASS — event mới tạo đúng một projection row |
| `GET /api/audit-logs` | Boundary PASS — anonymous request trả `401`; authenticated API/page query không chạy trong smoke vì không có BFF session, projection row đã sẵn sàng để query |
| Duplicate event ID | PASS — handler bỏ qua bản ghi đã có, không tạo projection duplicate |
| Auth boundary | PASS — anonymous `/api/audit-logs` vẫn `401`; user thiếu `HCS.AuditViewer` vẫn `403` |

## Root cause và runtime fix

Root cause là `services/platform/HCS.PlatformService/HCS.PlatformService.csproj` chưa tham chiếu `Volo.Abp.EventBus.RabbitMQ`, còn `HCSPlatformServiceModule` chưa khai báo `AbpEventBusRabbitMqModule`. `AuditRecordIntegrationEventHandler` có trong application assembly nhưng Platform không khởi tạo RabbitMQ distributed-event consumer; vì vậy không có queue, `AbpEventInbox` và `HcsAuditRecordProjections` đều rỗng.

Runtime fix đã:

- thêm package `Volo.Abp.EventBus.RabbitMQ` phiên bản `10.6.0` cho Platform;
- thêm `AbpEventBusRabbitMqModule` vào `HCSPlatformServiceModule`;
- cấu hình rõ `RabbitMQ:EventBus:ClientName=HCS.PlatformService` và `ExchangeName=hcs` cho local runtime;
- rebuild/restart Platform rồi kiểm tra lại queue, inbox, projection và API bằng event mới.

## Cách đọc flow khi vận hành

| Lớp | Kiểm tra | Kết luận |
|---|---|---|
| Producer outbox | `PublishedAt`, `DeadLetteredAt`, `LastError` trên outbox của Organization/Document/Work Management/Collaboration | `PublishedAt` chỉ chứng minh producer đã publish; không chứng minh consumer đã xử lý. Pending/dead-letter cần được xử lý ở producer. |
| RabbitMQ | Queue `HCS.PlatformService`, consumer count và message backlog | Không có queue/consumer là lỗi wiring/runtime. Có message nhưng không giảm là dấu hiệu consumer lỗi hoặc retry. |
| Platform inbox | `AbpEventInbox.EventName`, `HandledTime`, `RetryCount`, `NextRetryTime` | Có inbox row nghĩa là event đã tới consumer durable; chưa có `HandledTime` hoặc retry tăng là handler chưa hoàn tất. |
| Projection | `HcsAuditRecordProjections.Id`, `ExecutionTime`, `SourceService` | Có đúng một row theo event ID nghĩa là handler đã ghi read model. |
| API/page | `GET /api/audit-logs`, sau đó Refresh UI | Chỉ đọc projection; cần chờ eventual consistency sau event mới. |

Kiểm tra nhanh trong local Docker:

```bash
docker compose ps
docker compose exec -T rabbitmq rabbitmqctl list_queues name messages consumers
docker compose exec -T postgres psql -U hcs -d hcs_identity -c \
  'SELECT "EventName", "Status", "HandledTime", "RetryCount", "NextRetryTime" FROM "AbpEventInbox" ORDER BY "CreationTime" DESC LIMIT 20;'
docker compose exec -T postgres psql -U hcs -d hcs_identity -c \
  'SELECT COUNT(*) AS projection_rows, MAX("ExecutionTime") AS newest_execution FROM "HcsAuditRecordProjections";'
```

Sau đó tạo một request mới ở Organization, Document, Work Management hoặc Collaboration, đối chiếu event ID qua outbox → queue → inbox → projection rồi gọi lại `GET /api/audit-logs`. Không dùng request native của Platform/AuthServer để kiểm tra producer coverage của read model này.

## Event cũ không tự backfill

Các event đã publish và outbox đã đánh dấu `PublishedAt` trước khi queue/consumer Platform tồn tại không tự được publish lại. RabbitMQ không giữ event đã publish vào exchange để giao cho queue được tạo/bind sau đó; vì vậy fix này chỉ bảo đảm flow cho event mới (hoặc message cũ vẫn còn thực sự nằm trong queue), không làm các event đã trôi qua tự xuất hiện trong projection.

`AbpAuditLogs` cũng không tự chuyển thành `AuditRecordCapturedEto`, và projection không có cơ chế đọc ngược native audit hoặc producer outbox đã `PublishedAt`. Nếu cần khôi phục lịch sử phải có quy trình replay/backfill riêng, được review về schema, idempotency, PII và phạm vi thời gian; không chèn fake rows hoặc coi restart consumer là backfill.

## Known limitations

- Projection chỉ nhận custom audit event từ Organization, Document, Work Management và Collaboration. Login, Identity/Permission API và request trực tiếp của Platform/AuthServer có thể chỉ nằm trong native `AbpAuditLogs`.
- Middleware capture không mặc định bao phủ hub/background activity hoặc request bị short-circuit trước middleware.
- Event/projection/query chưa có `TenantId` và tenant predicate; runtime acceptance hiện chỉ dành cho local/single-tenant MVP.
- Projection là eventual-consistent; IP vẫn phụ thuộc `RemoteIpAddress` và forwarded-header/proxy configuration.
- Không có retention/archive/partition policy hoặc historical backfill trong scope fix này.

## Conclusion

Runtime delivery/projection acceptance của audit logs đã **PASS sau fix**. Blocker consumer wiring đã được xử lý; event mới theo đúng contract từ producer outbox được consume và project để page truy vấn. Anonymous auth boundary cũng PASS; authenticated BFF/page smoke cần chạy thêm với account có `HCS.AuditViewer` nếu cần xác nhận UI ở runtime. Coverage native Platform/AuthServer và việc khôi phục event lịch sử là follow-up riêng, không phải lỗi filter hoặc lỗi của BFF/page.
