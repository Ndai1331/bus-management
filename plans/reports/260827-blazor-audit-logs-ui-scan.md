---
title: "Blazor audit logs UI pattern review"
description: "Rà soát pattern UI, HTTP, localization và authorization cho kế hoạch xây audit logs page."
status: completed
created: 2026-08-27
tags: [blazor, audit-logs, ui, accessibility]
---

# Blazor audit logs UI pattern review

## Kết luận

`AdministrationFeature.razor` hiện chỉ đưa `/administration/audit-logs` vào `GatewayDataPanel`, một panel debug tổng quát: tải tối đa 100 record, hiển thị tối đa 8 cột, không có DTO/filter/paging/detail. Không nên mở rộng panel này thành audit UI cuối cùng.

## Structure nên tái sử dụng

- Dùng `OrganizationCatalog.razor` làm page skeleton: `hcs-catalog-page`, title/action bar, filter card, server-side `BlazoriseDataGrid`, loading/empty state và paging.
- Mượn filter/reset/error/loading patterns từ `Administration.razor` hoặc `Notifications.razor`; filter nên gọi API server-side, không lọc bù sau khi đã phân trang.
- Dùng `WorkflowInfoModal.razor` làm detail modal: `ExtraLarge`, summary/status, các section hoặc tab cho request/actions/entity changes, body cuộn độc lập.
- Tạo typed `AuditLogsClient` dùng `IHttpClientFactory` với client `HCS.Bff`, `ReadFromJsonAsync`, URI encoding và typed API exception như `OrganizationCatalogClient`/`IdentityAdminClient`. Tái sử dụng `GetAuditLogsInput`, `AuditLogDto`, `AuditLogDetailDto` từ application contracts nếu tương thích.
- Reuse các class/token hiện có trong `hcs-tokens.css`, `hcs-components.css`, `main.css`; chỉ thêm CSS audit-specific khi không thể biểu diễn bằng pattern catalog/table/modal hiện hữu.

## UX responsive

- Desktop: bảng ưu tiên các cột thời gian, user, action, method/URL, status, duration, correlation ID, exception; giữ action/detail ở cột cuối. Cho phép scroll ngang có chủ đích và dùng scroll hint, không ép mọi cột co thành chữ khó đọc.
- Tablet/mobile: filter chuyển thành một cột, controls full-width và touch target theo token; có thể ẩn bớt cột phụ hoặc dùng detail modal để xem URL/IP/browser/actions/entity changes. Modal giữ max-height và body scroll.
- Mốc cần kiểm tra: desktop rộng, 1100px (menu drawer), 768px và màn hình khoảng 375px. Screenshot không có trong workspace/turn này, nên đánh giá visual dựa trên HCS patterns hiện hành.

## Files likely modify/create

- Tạo `src/HCS.Blazor.Client/Pages/Auditing/AuditLogs.razor` và typed client/model UI tương ứng.
- Tạo `src/HCS.Blazor.Client/Components/Auditing/AuditLogDetailModal.razor` nếu detail cần tách khỏi page.
- Sửa `src/HCS.Blazor.Client/Pages/AdministrationFeature.razor` để không dùng generic `GatewayDataPanel` cho audit route; có thể giữ các route language hiện tại.
- Sửa `src/HCS.Blazor.Client/HCSBlazorClientModule.cs` để đăng ký typed client.
- Bổ sung key `Audit:*` cho `src/HCS.Blazor.Client/Localization/en.json` và `vi.json`; thay toàn bộ text hardcoded trong feature/panel/page mới bằng `L`.
- Sửa `src/HCS.Blazor.Client/Layouts/HCSMainLayout.razor` và assertion script navigation nếu audit menu chuyển sang policy-aware.
- Chỉ sửa gateway/backend khi contract thực tế thay đổi; hiện đã có `GET /api/audit-logs`, detail endpoint và YARP route tương ứng.

## Localization, authorization, accessibility concerns

- Route hiện có `[Authorize(Roles="admin")]`, trong khi `AuditViewerAppService` bảo vệ bằng `HCS.AuditViewer`. Nên thống nhất page/menu với permission policy `HCSPermissions.AuditViewer.Default` (hoặc ghi rõ quyết định admin-only); backend policy vẫn là boundary cuối.
- Backend filter có semantics cụ thể: `UserName`, status code và correlation ID là exact match; `Action` là contains; sort chỉ nhận dạng rõ `ExecutionTime ASC`; cần phản ánh đúng trong label/placeholder và client query.
- `ExecutionTime`/DB dùng `DateTime` không kèm timezone. Date-time filter cần quy ước rõ local time hay UTC, chuyển đổi nhất quán và hiển thị timezone cho người dùng.
- URL, IP, browser info, parameters và exception có thể nhạy cảm: encode khi render, không dùng `MarkupString`, tránh log lại dữ liệu; chỉ hiển thị chi tiết theo permission.
- Nút icon phải có `aria-label`/text ẩn; filter toggle có `aria-expanded`; DataGrid state dùng `role="status"`/`aria-live`; modal/tab cần focus management, `aria-labelledby`, trạng thái tab và keyboard navigation. Giữ hỗ trợ `prefers-reduced-motion`.
- Tránh N+1 lookup user/role/organization như `Administration.razor`; audit DTO đã có `UserName`, nên ưu tiên một request list.

## Verification steps

- Build/test đúng project client và các contract/gateway tests; kiểm tra typed client với URI encoding, paging, empty/error/401/403, detail JSON và date boundaries.
- Chạy các static layout checks hiện có: `scripts/audit-navigation-layout.sh` và `scripts/audit-mobile-layout.sh`.
- Manual smoke: `/administration/audit-logs`, filter từng trường, reset, sort/paging, mở/đóng detail, refresh, lỗi gateway, và hard refresh sau khi đổi localization/auth.
- Kiểm tra bằng keyboard/screen reader, focus khi mở/đóng modal, contrast/status icons, responsive tại 1440/1100/768/375px; thử admin, user có `HCS.AuditViewer` và user không có permission.

## Unresolved questions

- Audit logs là admin-only hay bất kỳ principal nào được cấp `HCS.AuditViewer`?
- Date-time filter và hiển thị sẽ theo timezone nào?
- Detail dùng modal hay cần deep-link route để chia sẻ một audit record?

**Status:** DONE_WITH_CONCERNS

**Summary:** Có thể xây page theo catalog + server-side DataGrid + detail modal hiện hữu; generic `GatewayDataPanel` chỉ nên giữ cho dữ liệu gateway đơn giản. Cần chốt authorization/timezone và bổ sung localization/a11y trước khi implement.
