# Phase 04 — Runtime event consumer cho Platform

**Status:** Completed
**Progress:** 100%

## Root cause đã xác nhận

Các producer đã publish `hcs.audit.record.v1` vào RabbitMQ, nhưng Platform chưa tham chiếu/khởi tạo `AbpEventBusRabbitMqModule`; vì vậy không có consumer queue, `AbpEventInbox` và `HcsAuditRecordProjections` đều rỗng. Root cause này đã được xử lý.

## Bản vá đã hoàn tất

- [x] Thêm `Volo.Abp.EventBus.RabbitMQ` `10.6.0` vào Platform.
- [x] Thêm `AbpEventBusRabbitMqModule` vào `HCSPlatformServiceModule`.
- [x] Cấu hình rõ `RabbitMQ:EventBus:ClientName=HCS.PlatformService` và `ExchangeName=hcs` cho local runtime.
- [x] Bổ sung regression test/config assertion để tránh Platform mất RabbitMQ module lần nữa.
- [x] Rebuild/restart Platform và xác nhận queue, inbox, projection, duplicate handling.

## Runtime evidence — PASS (local runtime, 2026-08-28)

| Check | Result |
|---|---:|
| RabbitMQ queue `HCS.PlatformService` | `consumers=1` |
| `AbpEventInbox` | `2` handled, `0` pending |
| `HcsAuditRecordProjections` | `1` row |
| Duplicate republish | Projection giữ nguyên `1` row |
| Platform build | `0/0` |
| Targeted tests | `9/9` |
| License audit | PASS |

## Acceptance — PASS

- [x] Platform có queue consumer trên RabbitMQ (`HCS.PlatformService`, `consumers=1`).
- [x] Runtime inbox xử lý `2` event, không còn pending; projection có đúng `1` row.
- [x] Duplicate event republish không tạo projection duplicate; projection vẫn là `1` row.
- [x] Platform build đạt `0/0` và targeted tests đạt `9/9`.
- [x] License audit PASS.
