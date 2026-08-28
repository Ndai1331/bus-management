# Phase 02 — Blazor page và detail

**Status:** Completed
**Progress:** 100%

## Mục tiêu

Thay màn hình JSON động hiện tại bằng page Audit Logs chuyên biệt, giữ mật độ dữ liệu gần screenshot nhưng có thể tra cứu thật qua BFF, phân trang server-side và xem detail.

## Context đã xác minh

- Route hiện tại: src/HCS.Blazor.Client/Pages/AdministrationFeature.razor
- Component generic cần tránh dùng cho page này: src/HCS.Blazor.Client/Shared/GatewayDataPanel.razor
- Pattern DataGrid: src/HCS.Blazor.Client/Pages/Administration.razor và các page catalog/document
- Date picker: src/HCS.Blazor.Client/Shared/HcsDatePicker.razor
- HTTP client module: src/HCS.Blazor.Client/HCSBlazorClientModule.cs
- Layout/menu: src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor
- Tokens/CSS: src/HCS.Blazor.Client/wwwroot/hcs-tokens.css, main.css, hcs-components.css
- Localization: src/HCS.Domain.Shared/Localization/HCS/vi.json và en.json

## Kiến trúc đã triển khai

- [x] Typed client dùng named client `HCS.Bff` và DTO dùng chung.
- [x] Page `/administration/audit-logs` thay panel JSON động; filter, paging, sorting, detail modal/drawer và retry đã hoạt động.
- [x] Localization vi/en, status text/color, null fallback, sanitized detail rendering và accessibility labels đã bổ sung.
- [x] Responsive table scroll, modal body scroll, loading/empty/error states, request cancellation và stale-result guard đã triển khai.
- [x] Route language cũ được giữ; gateway route và server permission không bị thay đổi.

Files implementation: `src/HCS.Blazor.Client/Auditing/AuditLogClient.cs`, `src/HCS.Blazor.Client/Pages/AuditLogs.razor`, `src/HCS.Blazor.Client/Pages/AuditLogs.razor.cs`, `src/HCS.Blazor.Client/Pages/AuditLogs.razor.css`, module registration, AdministrationFeature route cleanup và localization vi/en.

Không mở rộng GatewayDataPanel thành component audit. Component generic đó không có contract typed, filter, paging hoặc detail lifecycle phù hợp.

## Route và quyền

Page dùng route /administration/audit-logs và policy/permission HCS.AuditViewer ở server. Menu hiện tại đang ẩn theo role admin; giữ hành vi này trong MVP để không mở menu rộng hơn quyền đang có, nhưng phải test trường hợp user admin không có HCS.AuditViewer nhận 403 rõ ràng. Nếu UX yêu cầu hiển thị menu theo permission thay vì role, ghi thành quyết định riêng trước khi sửa layout.

## Bố cục và hành vi

1. Header: tiêu đề, mô tả ngắn, tổng số bản ghi của query hiện tại và nút Refresh. Hiển thị thời điểm refresh/ghi chú eventual consistency khi cần.

2. Filter card:

   - ô keyword nhanh;
   - advanced filters: user/họ tên, khoảng thời gian, HTTP status, method, IP, action, API/path, service, application, correlation ID, có exception;
   - Search, Reset và Enter để submit; không gọi API ở mỗi lần gõ;
   - đổi filter reset về page 1; giữ filter khi đổi page/sort; date gửi UTC;
   - trạng thái filter rõ ràng và không phụ thuộc placeholder.

3. Bảng desktop dense theo reference:

   - Chi tiết;
   - HTTP status + text, HTTP method và API/path;
   - họ tên/user, fallback username hoặc —;
   - IP;
   - thời gian local theo timezone đã thống nhất, format dd/MM/yyyy HH:mm:ss;
   - thời lượng với tabular numbers và đơn vị ms;
   - source service;
   - application;
   - correlation ID;
   - exception indicator.

   Status phải có text ngoài màu/icon để không phụ thuộc màu. API, correlation ID và browser dài phải wrap/truncate có title; null dùng —. Dùng table scroll ngang có nhắc mobile, không thu nhỏ chữ tới mức khó đọc.

4. Pagination/sort: page size 20/50/100, tổng số row, first/previous/next/last khi phù hợp. Có loading skeleton hoặc trạng thái loading, empty state có hướng dẫn Reset, error state có Retry, và không để request cũ ghi đè kết quả mới.

5. Detail modal/drawer:

   - summary: audit id, status, method, URL, action, service, application, execution time, duration, correlation ID;
   - actor/context: họ tên, user id, IP, browser;
   - exception đã sanitize, comments;
   - action list: service, method, parameters đã lọc secret, execution time/duration;
   - entity-change summary: type, entity id, change type, change time;
   - empty/missing fields có fallback; đóng bằng nút, Escape và overlay theo pattern hiện có.

   Không hiển thị token, cookie, authorization header, request/response body hoặc secret trong modal. Parameters phải được coi là untrusted text và encode an toàn.

## Accessibility và responsive

- Dùng design tokens hiện có, không hard-code màu theo screenshot.
- Keyboard focus, semantic table/header, aria-label cho filter/action/status, focus trap và focus restore cho modal.
- Nút và control có touch target tối thiểu theo convention hiện có; focus visible.
- Kiểm tra 375, 768 và 1440px; ở mobile ưu tiên horizontal scroll có affordance hoặc chuyển metadata thành block rõ ràng.
- Tôn trọng prefers-reduced-motion và không dùng animation để truyền đạt trạng thái duy nhất.

## Acceptance đã đạt

- [x] Page gọi đúng BFF typed endpoint, không còn dữ liệu fake/dynamic JSON.
- [x] Filter, sort, paging và detail hoạt động; lỗi 401/403/5xx có thông báo và retry.
- [x] Các field yêu cầu tra cứu có trên row hoặc detail; browser nằm trong detail.
- [x] UI giữ filter trong phiên tra cứu, không gửi request theo từng phím, không rò secret.
- [x] Localization vi/en không còn chuỗi hiển thị audit hard-code; keyboard và responsive smoke pass.
