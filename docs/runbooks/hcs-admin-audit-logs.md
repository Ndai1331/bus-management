# HCS admin audit logs runbook

Runbook này mô tả màn hình tra cứu audit logs trong local free-license runtime. Đây là read model tập trung cho một phần HTTP activity; không phải native ABP audit viewer hợp nhất toàn hệ thống.

## Route và quyền

- Màn hình Blazor: `https://hcs.localhost/administration/audit-logs`.
- BFF list endpoint: `GET /api/audit-logs`.
- BFF detail endpoint: `GET /api/audit-logs/{id}`.
- `AuditViewerController` khai báo thêm alias `api/hcs/audit-logs`; route mà Web Gateway cấu hình và typed client sử dụng là `/api/audit-logs`.
- Permission server-side: `HCS.AuditViewer` (`HCSPermissions.AuditViewer.Default`). `AuditViewerAppService` là boundary bảo vệ cho cả list và detail; không coi việc ẩn menu hay route UI là cơ chế bảo mật.

Menu hiện vẫn nằm trong admin shell. Vì vậy admin không có `HCS.AuditViewer` có thể thấy/đi tới route nhưng phải nhận `403`; principal có permission nhưng không thuộc shell hiện tại chưa được xem là UX được hỗ trợ trong MVP. Sau khi đổi role/permission, sign out rồi sign in lại để BFF nhận claims mới.

## Luồng dữ liệu và độ trễ

```text
Organization / Document / Work Management / Collaboration
  -> AuditRecordCapturedEto (hcs.audit.record.v1)
  -> service outbox + RabbitMQ/event bus
  -> Platform consumer queue (HCS.PlatformService)
  -> AbpEventInbox + AuditRecordIntegrationEventHandler
  -> HcsAuditRecordProjections
  -> Platform AuditViewerAppService
  -> Web Gateway/BFF -> Blazor page
```

Projection được cập nhật bất đồng bộ. Request nghiệp vụ thành công không có nghĩa là dòng đã xuất hiện ngay trong màn hình; outbox, RabbitMQ, inbox hoặc retry backlog có thể tạo độ trễ. Dùng **Refresh**, ghi nhận thời điểm refresh, và chờ một khoảng ngắn trước khi kết luận event bị mất. Không query nhiều database hoặc ghép native audit logs trong browser để bù dữ liệu.

Migration tạo `HcsAuditRecordProjections`, `AbpEventInbox` và các index cho `ExecutionTime`, `UserId`, `CorrelationId` và `(SourceService, ActionName)`. Projection hiện không backfill từ `AbpAuditLogs` và không có retention/archive/partition policy trong slice này.

### Kiểm tra queue, outbox, inbox và projection

Các thành phần có ý nghĩa vận hành khác nhau; không coi một trạng thái đơn lẻ là bằng chứng cho toàn bộ flow:

| Thành phần | Dấu hiệu bình thường | Ý nghĩa khi chẩn đoán |
|---|---|---|
| Producer outbox | `OutboxMessages` của producer có `PublishedAt`, không có pending/dead-letter bất thường | Event đã được ghi bền vững cùng request và worker đã publish. `PublishedAt` chỉ chứng minh producer publish thành công, không chứng minh Platform đã consume. |
| RabbitMQ | Có queue `HCS.PlatformService`, có consumer; `messages` giảm sau request mới | Queue phải được bind vào exchange `hcs` cho event `hcs.audit.record.v1`. Không có consumer thì event mới không thể vào inbox/projection. |
| Platform inbox | `AbpEventInbox` có row của event mới, `HandledTime` được điền; retry không tăng liên tục | Event đã tới consumer và được ABP theo dõi durable. `RetryCount`/`NextRetryTime` hoặc row chưa handled cho thấy consumer đang lỗi/chờ retry. |
| Projection | `HcsAuditRecordProjections` có đúng một row theo `Id` của event mới | Handler đã ghi read model. Handler idempotent; duplicate event ID không tạo thêm projection row. |

Trong local Docker, bắt đầu từ topology và queue:

```bash
docker compose ps
docker compose exec -T rabbitmq rabbitmqctl list_queues name messages consumers
```

Queue cần thấy là `HCS.PlatformService` và consumer count phải lớn hơn `0`. Nếu queue có message nhưng consumer bằng `0`, kiểm tra log Platform và cấu hình `RabbitMQ:EventBus:ClientName=HCS.PlatformService`, `RabbitMQ:EventBus:ExchangeName=hcs` trước khi kiểm tra projection.

Kiểm tra inbox/projection trong database `hcs_identity`:

```bash
docker compose exec -T postgres psql -U hcs -d hcs_identity -c \
  'SELECT "EventName", "Status", "HandledTime", "RetryCount", "NextRetryTime" FROM "AbpEventInbox" ORDER BY "CreationTime" DESC LIMIT 20;'
docker compose exec -T postgres psql -U hcs -d hcs_identity -c \
  'SELECT COUNT(*) AS projection_rows, MAX("ExecutionTime") AS newest_execution FROM "HcsAuditRecordProjections";'
```

Producer outbox nằm ở các database/bảng sau; thay tên bảng vào truy vấn tương tự và lọc `EventName = 'hcs.audit.record.v1'`:

| Producer | Database | Bảng |
|---|---|---|
| Organization | `hcs_organization` | `hcs_organization."OutboxMessages"` |
| Document | `hcs_document` | `document."OutboxMessages"` |
| Work Management | `hcs_work` | `hcs_work."OutboxMessages"` |
| Collaboration | `hcs_collaboration` | `public."CollaborationOutbox"` |

Tập trung kiểm tra ba trạng thái: `PublishedAt IS NULL AND DeadLetteredAt IS NULL` là pending; `DeadLetteredAt IS NOT NULL` cần xem `LastError`; `PublishedAt IS NOT NULL` là đã publish nhưng vẫn phải kiểm tra queue → inbox → projection. Sau mỗi lần kiểm tra nên tạo **một request mới** từ Organization, Document, Work Management hoặc Collaboration, chờ worker/consumer xử lý, rồi đối chiếu event ID qua các lớp và gọi lại `GET /api/audit-logs`.

### Event đã publish trước khi có consumer không tự backfill

Event đã publish và outbox đã đánh dấu `PublishedAt` trước khi queue/consumer `HCS.PlatformService` tồn tại không tự được publish lại. RabbitMQ không giữ lại event đã publish vào exchange cho một queue được tạo/bind về sau; vì vậy việc thêm consumer chỉ sửa flow cho event mới (hoặc message cũ vẫn còn thực sự nằm trong queue), không làm hồi sinh các event đã trôi qua.

Muốn khôi phục lịch sử phải có quy trình replay/backfill riêng, có kiểm soát từ payload outbox còn lưu hoặc nguồn native phù hợp (`AbpAuditLogs`). Không tự sửa `HcsAuditRecordProjections`, không chèn fake rows và không coi việc restart consumer là backfill. Đây cũng là lý do số liệu trước khi fix có thể vẫn không xuất hiện trong page dù outbox báo đã publish.

## Coverage hiện tại

| Service | Nguồn vào projection | Phạm vi thực tế |
|---|---|---|
| Organization | HTTP audit outbox middleware | HTTP requests đi qua middleware; tên source/application hiện được producer gán cố định là `HCS.OrganizationService`. |
| Document | HTTP audit outbox middleware và một số business audit event | Có thể có thêm dòng business-level bên cạnh dòng HTTP cho cùng một thao tác; không mặc định coi đó là duplicate lỗi. |
| Work Management | HTTP audit outbox middleware | HTTP requests đi qua middleware. |
| Collaboration | HTTP audit outbox middleware | HTTP requests đi qua middleware; hub/background activity không mặc định được bao phủ. |

Platform và AuthServer hiện dùng native ABP auditing (`AbpAuditLogs`) cho hoạt động do chính chúng phát sinh; Platform là consumer của custom event từ các producer khác, không phải producer tự động đưa native audit vào projection này. Vì vậy login, Identity/Permission API và các request trực tiếp của Platform/AuthServer có thể có trong `AbpAuditLogs` nhưng không xuất hiện tại `/api/audit-logs`. Blazor host cũng không phải producer của read model này. Đây là coverage gap đã biết, không phải lỗi filter; nếu cần audit toàn hệ thống phải có follow-up phát cùng schema và chính sách backfill riêng.

Các giới hạn capture cần nhớ:

- middleware không bảo đảm ghi được request bị authorization short-circuit trước khi chạy tới middleware;
- exception của request được hiển thị bằng status `500` trong event capture;
- `ClientIpAddress` lấy từ `RemoteIpAddress`. Qua Caddy/YARP/container, giá trị có thể là IP proxy/container nếu forwarded headers chưa được cấu hình và tin cậy đúng cách;
- correlation field bắt nguồn từ `HttpContext.TraceIdentifier`; không tự động coi là một ID xuyên suốt Gateway → service nếu runtime chưa chứng minh điều đó.

## Bộ lọc, phân trang và sắp xếp

List query là server-side. Các điều kiện kết hợp với nhau theo `AND`; keyword là một nhóm `OR` bên trong nhóm điều kiện đó.

| Query field | Hành vi |
|---|---|
| `filter` | Keyword tổng hợp trên user name, User ID nếu keyword parse được thành GUID, IP, browser, action, URL/path, source service, application và correlation ID; phần text dùng `contains`. |
| `userId` | Exact GUID match. |
| `userName` | Exact match sau khi trim. Giá trị capture ưu tiên claim `name`, sau đó `family_name + given_name`, rồi `preferred_username`/identity name. |
| `startTime` | `ExecutionTime >= startTime`, inclusive. API chuẩn hóa UTC. |
| `endTimeExclusive` | `ExecutionTime < endTimeExclusive`, exclusive; UI dùng field này. |
| `endTime` | Backward-compatible inclusive `<=`, chỉ dùng khi không có `endTimeExclusive`. |
| `httpStatusCode` | Exact status code. |
| `httpMethod` | Exact match, chuẩn hóa uppercase; UI allow-list GET/POST/PUT/PATCH/DELETE. |
| `clientIpAddress` | Exact IP match. |
| `browserInfo` | `contains` trên User-Agent/browser info; chỉ có ở detail, không load vào row list. |
| `sourceService` / `applicationName` | Exact match. |
| `correlationId` | Exact match. |
| `action` | `contains` trên action/endpoint name. |
| `url` | `contains` trên `Request.Path`; không gồm host hoặc query string. |
| `hasException` | `true` khi projection có exception text không rỗng; `false` khi null/rỗng. |

Quy ước vận hành:

- API nhận `DateTime` theo UTC; màn hình nhập giờ local của trình duyệt rồi chuyển sang UTC. Giờ hiển thị dùng local timezone của client và format `dd/MM/yyyy HH:mm:ss`.
- Page size mặc định là `20`, UI cho `20/50/100`, giới hạn server tuyệt đối là `100`. `skipCount` âm được đưa về `0` và bị giới hạn tối đa `100000`.
- Sort được allow-list ở server: `ExecutionTime`, `HttpStatusCode`, `ExecutionDuration`, `UserName`, `SourceService`, `ApplicationName`. Mặc định và giá trị không hợp lệ là `ExecutionTime DESC`; mọi sort có `Id DESC` làm tie-breaker để phân trang ổn định.
- Filter text được trim và cắt tối đa 256 ký tự; URL tối đa 2048 ký tự. Không gửi filter theo từng phím; dùng Search hoặc Enter.

## Fields hiển thị và detail

### Row list

List projection chỉ lấy các field cần cho bảng:

- `Id`, `UserId`, `UserName`;
- `ExecutionTime`, `ExecutionDuration` (ms);
- `HttpMethod`, `Url`, `HttpStatusCode`;
- `ClientIpAddress`, `SourceService`, `ApplicationName`, `CorrelationId`;
- `HasException` dạng cờ.

URL, correlation ID và giá trị dài có thể wrap/truncate trên bảng; null hiển thị `—`. User hiển thị fallback theo thứ tự `UserName` → `UserId` → `—`. Browser info, exception text, comments, nested actions và entity changes không được load trong list.

### Detail

Detail tải riêng theo `id` và gồm summary request (status, method, URL, action, service, application, time, duration, correlation), actor/context (user, user ID, IP, browser), exception đã sanitize, comments, nested actions và metadata entity changes. `ActionsJson`/`EntityChangesJson` malformed được coi là danh sách rỗng và ghi warning có kiểm soát ở server; không trả raw `JsonException` cho người dùng.

## Redaction và PII

Audit data có thể chứa PII hoặc dữ liệu vận hành nhạy cảm: user name/user ID, IP, User-Agent/browser, URL/path, action name, correlation ID, comments và entity identifiers. Chỉ cấp `HCS.AuditViewer` cho nhóm cần tra cứu; không copy raw log vào ticket/chat công khai và không dùng audit viewer như nơi lưu trữ bí mật.

Các bảo vệ hiện có:

- exception capture được chuyển thành thông báo ổn định: `Request failed. Inspect server logs using the correlation id.`; raw exception vẫn chỉ ở server logs/native audit;
- projection sanitize exception một lần nữa khi nhận event;
- action `Parameters` bị loại khỏi detail DTO (`null`), nên UI không hiển thị parameters;
- DTO/viewer không trả request body, response body, access token, cookie hoặc authorization header;
- Razor render text qua encoding thông thường; coi URL, browser, comments và entity metadata là untrusted text.

`Comments` và các field metadata không phải là secret store. Nếu một producer đưa PII/secret vào đó thì nó vẫn cần được xử lý ở capture boundary; không đưa secret vào comments, endpoint path, action name hoặc test data.

## Smoke check sau deploy/rebuild

1. Xác nhận stack local đang chạy và mở `https://hcs.localhost` bằng account đã được cấp `admin` + `HCS.AuditViewer`.
2. Mở `/administration/audit-logs`; kiểm tra tổng số record, thời điểm refresh và ghi chú projection/outbox.
3. Test Search với user/IP/API/action/correlation; mở Advanced filters và test status, method, service, application, exception state, khoảng thời gian.
4. Kiểm tra end time là exclusive: record đúng tại mốc kết thúc không nằm trong kết quả; start time vẫn inclusive.
5. Kiểm tra sort status/user/time/duration, page size 20/50/100, chuyển trang và reset. Xác nhận request list không chứa JSON detail.
6. Mở một detail và xác nhận browser, exception sanitized, nested actions/entity changes; parameters không xuất hiện.
7. Sau một request mới ở Organization, Document, Work Management hoặc Collaboration, kiểm tra queue `HCS.PlatformService`, inbox/projection rồi Refresh và chờ projection đồng bộ. Không dùng một request Platform/AuthServer để kiểm tra coverage của read model; event cũ đã publish trước khi consumer tồn tại cũng không phải dữ liệu kiểm tra hợp lệ cho flow mới.
8. Thử account không có permission: page/API phải bị từ chối (`403` cho session đã xác thực; `401` khi chưa xác thực). Sau khi đổi permission, sign out/in lại.

## Troubleshooting

| Triệu chứng | Kiểm tra và xử lý |
|---|---|
| `401` hoặc bị quay lại login | Kiểm tra BFF session/OIDC; sign in lại. Nếu vừa đổi auth/config, hard-refresh và lấy lại antiforgery/session cookie. |
| `403` với admin | Cấp `HCS.AuditViewer`, sign out/in để refresh claims. Không mở quyền bằng cách bỏ attribute khỏi UI; server app service là boundary. |
| Dòng mới chưa xuất hiện | Chạy `docker compose ps`; xác nhận queue `HCS.PlatformService` có consumer, rồi kiểm tra producer outbox → `AbpEventInbox` → `HcsAuditRecordProjections` và Platform logs. Sau đó Refresh; xem backlog/retry trước khi kết luận mất event. |
| Queue Platform không có hoặc consumer bằng 0 | Kiểm tra Platform đã bật `Volo.Abp.EventBus.RabbitMQ`, module `AbpEventBusRabbitMqModule`, `ClientName=HCS.PlatformService`, exchange `hcs`, rồi rebuild/restart Platform. Chỉ kiểm tra lại bằng một event mới. |
| Có dữ liệu nhưng thiếu Platform/AuthServer | Đây là gap đã biết: kiểm tra native `AbpAuditLogs`; projection hiện không đọc trực tiếp và không backfill bảng native. |
| Outbox báo published nhưng inbox/projection vẫn không tăng | `PublishedAt` không chứng minh consumer đã xử lý. Kiểm tra queue binding/consumer, `AbpEventInbox.HandledTime`, `RetryCount` và `NextRetryTime`; event đã publish trước khi queue tồn tại không tự backfill. |
| IP hiển thị IP proxy/container | Kiểm tra Caddy/YARP và forwarded-header configuration. Chỉ tin proxy allow-list; không coi header client tùy ý là IP xác thực. |
| Correlation không nối được qua các service | Capture hiện dùng `TraceIdentifier`; kiểm tra từng hop và log server, không giả định ID đã được propagate end-to-end. |
| Detail không có actions/entity changes | Record có thể không có detail, JSON cũ/malformed hoặc event producer không gửi nested data. Kiểm tra warning của `AuditViewerAppService`; UI cố ý trả danh sách rỗng. |
| Document có hai dòng gần giống nhau | Document có cả HTTP middleware và một số business audit event. Đối chiếu `Id`, action, time và correlation trước khi coi là lỗi deduplication. |
| Kết quả sai do khoảng thời gian | Gửi UTC; dùng `endTimeExclusive` cho mốc kết thúc. Nhớ `userName`, status, method, IP, service, application và correlation là exact; `action`, URL, browser là contains. |
| Record cũ không có browser/name/detail | Dữ liệu lịch sử có thể thiếu field; UI dùng fallback và không bịa dữ liệu. Native `AbpAuditLogs` cũng chưa được backfill vào projection. |
| Query chậm | Giảm khoảng thời gian và filter trước; tránh keyword contains quá rộng. Chỉ thêm index sau khi đo volume/query plan; JSON detail không được thiết kế để query trong list. |

## Tài liệu/code tham chiếu

- Contract và query: [`AuditLogDtos.cs`](../../src/HCS.Application.Contracts/Auditing/AuditLogDtos.cs), [`AuditViewerAppService.cs`](../../src/HCS.Application/Auditing/AuditViewerAppService.cs).
- Projection và migration: [`AuditRecordProjection.cs`](../../src/HCS.Domain/Auditing/AuditRecordProjection.cs), [`HcsAuditProjectionModelBuilderExtensions.cs`](../../src/HCS.EntityFrameworkCore/EntityFrameworkCore/Auditing/HcsAuditProjectionModelBuilderExtensions.cs).
- Event/redaction: [`AuditEvents.cs`](../../building-blocks/HCS.IntegrationEvents/AuditEvents.cs).
- HTTP controller: [`AuditViewerController.cs`](../../src/HCS.HttpApi/Controllers/Auditing/AuditViewerController.cs).
- BFF route: [`appsettings.json`](../../gateways/web/HCS.WebGateway/appsettings.json).
- Blazor page/client: [`AuditLogs.razor`](../../src/HCS.Blazor.Client/Pages/AuditLogs.razor), [`AuditLogClient.cs`](../../src/HCS.Blazor.Client/Auditing/AuditLogClient.cs).
