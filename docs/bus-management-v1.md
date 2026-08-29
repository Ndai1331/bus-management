# HCS Bus Management — vertical slice v1 và Phase 2–4A

Đây là bản tách từ working tree `HCS_web_free_license`, hiện được quản lý tại repository [Ndai1331/bus-management](https://github.com/Ndai1331/bus-management); bến xe là bounded context độc lập và không join database với Work/Document.

Trạng thái: Phase 2–4A đã có vertical slice và đã harden trong working tree; nghiệm thu runtime end-to-end vẫn còn phụ thuộc PostgreSQL, Gateway/BFF, Keycloak và browser.

## Runtime contract

- Service: `HCS.BusManagementService`
- URL local: `https://localhost:44416`
- API prefix: `/api/bus-management`
- Database/schema: `hcs_bus_management`
- Browser chỉ gọi BFF/YARP; service nhận bearer token từ Gateway. PostgreSQL, Gateway/BFF, Keycloak và browser smoke test vẫn là runtime gate, chưa được coi là đạt chỉ từ build/test tĩnh.
- Export cũng đi qua BFF bằng session cookie; không tạo URL service trực tiếp hoặc đưa bearer token ra browser.

## Luồng v1

`station assignment → master data → departure/readiness → tariff/receipt → expense approval → shift settlement → daily close → dashboard/report`

Mọi truy vấn có station scope ở server. `admin` và `lanhdao` là global; các role còn lại chỉ thấy station assignment còn hiệu lực. `stationId` trong query/body không thể mở rộng scope. Seed hiện cấp quyền Bus theo vai trò: `admin` toàn quyền, `lanhdao` quyền leadership không gồm mutation/adjust/export, `station-manager` có quyền Bus quản lý/phê duyệt nhưng không tạo adjustment, `operations-staff` cho departures/revenue, `accountant-business` cho finance/report và tạo adjustment, còn `control-security` cho departures/master data.

Catalog nhà xe, tuyến, xe và tài xế mới có `StationId`; user không phải global phải truyền một bến đang được gán. Dữ liệu legacy không có station không được trả về cho scope bến. Riêng hồ sơ pháp lý xe legacy chỉ fallback khi lịch sử departure xác định đúng một station; nếu mơ hồ thì không suy diễn ownership.

## Chạy cục bộ

```bash
dotnet restore HCS.slnx --configfile NuGet.Config
dotnet build HCS.slnx --no-restore
dotnet test services/bus-management/HCS.BusManagementService.Tests/HCS.BusManagementService.Tests.csproj --no-restore
dotnet run --project services/bus-management/HCS.BusManagementService/HCS.BusManagementService.csproj
```

Service tự chạy migration cho `BusManagement` khi khởi động. Khi chạy Compose, database `hcs_bus_management` được tạo trong PostgreSQL init script và connection string phải lấy từ secret môi trường. Không dùng connection string hoặc secret trong source control.

## Endpoint chính

- `/api/bus-management/stations`
- `/api/bus-management/master-data/*`
- `/api/bus-management/operators/contracts`
- `/api/bus-management/compliance/vehicle-documents`
- `/api/bus-management/departures/*`
- `/api/bus-management/revenue/receipts`
- `/api/bus-management/revenue/parking/tariffs|sessions`
- `/api/bus-management/revenue/parking/sessions/{id}/close|cancel`
- `/api/bus-management/expenses`
- `/api/bus-management/premises/leases`
- `/api/bus-management/reconciliation/shifts/*`
- `/api/bus-management/reconciliation/daily/close`
- `/api/bus-management/reconciliation/adjustments`
- `/api/bus-management/dashboard`
- `/api/bus-management/reports/revenue|departures|reconciliation|compliance`
- `/api/bus-management/exports/{revenue|departures|reconciliation|compliance}`

Các mutation tài chính phát sinh outbox và audit event trong cùng DbContext/SaveChanges với mutation. Phiếu thu đã `Issued`, settlement đã `Closed`, kỳ đã `Closed` và dữ liệu nguồn sau chốt không có đường sửa/xóa trực tiếp.
Settlement tổng hợp lại số liệu hiện hành trước khi submit/check/approve/close; sau khi settlement chuyển khỏi `Draft`, receipt/expense cùng ca bị khóa. Receipt/expense/settlement/departure mutation lấy PostgreSQL advisory transaction lock theo `(station, business date)` và daily close dùng cùng lock để tuần tự hóa kiểm tra và ghi chốt. Lock chỉ có hiệu lực trên relational PostgreSQL; InMemory chỉ phù hợp unit/model tests.

## Phase 2 đã triển khai

- Receipt lưu snapshot context cho `VisitingVehicle`, `PublicBus`, `Parking`, `Premises` và `Other`; server kiểm tra source/reference, không tin `stationId` để mở scope.
- `AdjustmentEntry` chỉ trỏ đúng một receipt hoặc expense, yêu cầu target hợp lệ (`Issued`/`Approved`) và ngày đã chốt, maker-checker và lưu approver/time. Check constraint PostgreSQL lặp lại invariant đúng một target và amount khác 0. `Total*` là base, `*AdjustmentAmount` là adjustment đã duyệt, `Net*` là tổng hiển thị.
- Quyền tạo và duyệt adjustment tách riêng (`Reconciliation.Adjust` và `Reconciliation.AdjustApprove`); outbox có event settlement và adjustment versioned.
- Hồ sơ xe có ownership station cho bản ghi mới và endpoint renew/update; legacy record không có station chỉ fallback khi xác định được station duy nhất.
- Dashboard/compliance report cảnh báo hồ sơ xe, hợp đồng nhà xe và hợp đồng mặt bằng trong 30 ngày.

API adjustment:

- `GET/POST /api/bus-management/reconciliation/adjustments`
- `POST /api/bus-management/reconciliation/adjustments/{id}/approve`

## Phase 3 đã triển khai

- `GET /api/bus-management/exports/{revenue|departures|reconciliation|compliance}?format=xlsx|pdf|html` dùng query server-side, giữ filter/date/station scope.
- Route export yêu cầu `HCS.BusManagement.Reports.Export`; dashboard hiển thị breakdown theo từng station và hiện có link XLSX/PDF/trang in cho revenue khi user có quyền này. Các report type còn lại đã có route export server-side nhưng chưa có bảng UI riêng.
- XLSX dùng Open XML; PDF/HTML được tạo tại service. HTML có nút `window.print()` và CSS in. PDF wrap Unicode và chia trang (46 dòng dữ liệu/trang); export giới hạn 366 ngày và 100.000 dòng để tránh giữ payload không kiểm soát trong process. Compliance report nhận `asOf` để khoảng thời gian export có ý nghĩa.

## Phase 4A đã triển khai

- `ParkingTariff` lưu bến, loại xe, đơn vị tính theo phút, đơn giá, mức tối thiểu và thời hạn hiệu lực; không cho cấu hình tariff cùng loại bị overlap.
- `ParkingSession` chạy `Open → Closed|Cancelled`, chống hai phiên mở cùng biển số/bến/ngày bằng partial unique index và advisory business-day lock.
- Khi xe vào, session snapshot toàn bộ tham số tariff. Khi đóng, hệ thống tính `ceil(thời lượng / đơn vị phút) × đơn giá`, áp mức tối thiểu, tạo đúng một phiếu thu `Parking` và phát outbox event. Đóng lặp lại trả session đã đóng, không phát sinh phiếu thứ hai.
- Phiếu thu parking có liên kết one-to-one tới session và check constraint; FK một chiều là FK ghép `(ParkingSessionId, StationId)` từ receipt sang session, bảo đảm receipt không thể trỏ chéo bến và tránh vòng FK khi đóng phiên. Receipt parking thủ công không còn được tạo qua API generic.
- Migration thực hiện legacy parking backfill: đánh dấu các receipt `Parking` lịch sử chưa có session bằng `IsLegacyParking`, không làm dừng startup migration. Bản ghi legacy chỉ để bảo toàn lịch sử; mọi receipt parking mới bắt buộc liên kết session.
- `ArrivalUtc`/`ExitUtc` phải là UTC; arrival không được ở tương lai, exit không được ở tương lai hoặc trước arrival. `BusinessDate` được kiểm tra từ `ArrivalUtc` theo timezone của bến; tiền parking làm tròn theo VND (0 chữ số thập phân). Cấu hình tariff cùng bến/loại xe được serialize bằng PostgreSQL advisory lock.
- Parking create/close/cancel và daily close chạy trong explicit transaction (hoặc savepoint khi đã có transaction), dùng cùng PostgreSQL advisory transaction lock theo `(station, business date)`. Close parking ghi session, receipt, receipt line, revenue/parking outbox và audit trong cùng boundary; daily close khóa, kiểm tra và ghi chốt nguyên tử.
- DTO/event giữ snapshot cần cho downstream: `ParkingSessionDto` trả arrival/exit, duration, billed units, billing unit, rate, minimum charge, tariff description, charged amount, status và receipt; `RevenueReceiptDto` trả `ParkingSessionId`; `BusParkingSessionChangedEto` mang business date, status, charged amount, receipt id, billing unit và cancellation reason.

## Migration và validation evidence

Migration chain hiện có trong bounded context:

- `20260828104916_InitialBusManagement`
- `20260828105031_AddBusAuditOutbox`
- `20260828113032_FixBusRelationships`
- `20260828121058_AddBusPhaseTwo`
- `20260829040425_AddBusPhaseThreeScopeIntegrity`
- `20260829133927_AddBusPhaseFourParking`

Snapshot validation ngày 2026-08-29:

```text
Focused Bus Management tests: 27 passed, 0 failed
AuthServer.Tests:           18 passed, 0 failed
MigrationImporter.Tests:    11 passed, 0 failed
Full solution tests:        383 passed, 0 failed
Bus service build:          0 warnings, 0 errors
has-pending-model-changes:  No changes have been made to the model since the last migration.
```

Các lệnh tương ứng:

```bash
dotnet test services/bus-management/HCS.BusManagementService.Tests/HCS.BusManagementService.Tests.csproj --no-restore --verbosity normal
dotnet test apps/auth-server/HCS.AuthServer.Tests/HCS.AuthServer.Tests.csproj --no-restore --verbosity quiet
dotnet test tools/HCS.MigrationImporter/HCS.MigrationImporter.Tests/HCS.MigrationImporter.Tests.csproj --no-restore --verbosity quiet
dotnet ef migrations has-pending-model-changes \
  --project services/bus-management/HCS.BusManagementService/HCS.BusManagementService.csproj \
  --startup-project services/bus-management/HCS.BusManagementService/HCS.BusManagementService.csproj --no-build
```

Gateway `118/118` và EFCore `13/13` đã xanh trong lần cross-project verification ngày 2026-08-28; chưa có HTTP acceptance suite riêng cho Bus Management. `dotnet ef migrations list` hiện liệt kê đủ sáu migration nhưng không xác định được migration nào đã apply vì PostgreSQL `127.0.0.1:5432` chưa chạy. EF tooling cũng cảnh báo `10.0.5` thấp hơn runtime `10.0.9`.

Runtime limitation hiện tại: chưa có bằng chứng live cho advisory lock/explicit transaction, bãi đỗ, BFF/YARP route, Keycloak claim/role matrix hoặc browser export vì PostgreSQL/Gateway/Keycloak chưa được khởi động trong snapshot này. Cần chạy local infrastructure, seed role/permission, rồi smoke-test browser qua Gateway trước khi đánh dấu Phase 2–4A production-ready.

## Còn ngoài phạm vi Phase 4A

Chi tiết bản đồ/vị trí bãi đỗ, reservation/barrier/camera/payment, notification/workflow tài liệu, kế toán tổng hợp/VAT/công nợ/ngân hàng, realtime vận hành và tích hợp read model Work/Document vẫn để phase tiếp theo. Hai hệ thống không join database; dùng outbox event làm điểm nối.
