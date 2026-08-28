# HCS Bus Management — vertical slice v1

Đây là bản tách local từ working tree `HCS_web_free_license`. Repository mới chưa có remote GitHub; bến xe là bounded context độc lập và không join database với Work/Document.

## Runtime contract

- Service: `HCS.BusManagementService`
- URL local: `https://localhost:44416`
- API prefix: `/api/bus-management`
- Database/schema: `hcs_bus_management`
- Browser chỉ gọi BFF/YARP; service nhận bearer token từ Gateway.

## Luồng v1

`station assignment → master data → departure/readiness → tariff/receipt → expense approval → shift settlement → daily close → dashboard/report`

Mọi truy vấn có station scope ở server. `admin` và `lanhdao` là global; các role còn lại chỉ thấy station assignment còn hiệu lực. `stationId` trong query/body không thể mở rộng scope.

## Chạy cục bộ

```bash
dotnet restore HCS.slnx
dotnet build HCS.slnx --no-restore
dotnet test services/bus-management/HCS.BusManagementService.Tests/HCS.BusManagementService.Tests.csproj --no-build --no-restore
dotnet run --project services/bus-management/HCS.BusManagementService/HCS.BusManagementService.csproj
```

Service tự chạy migration cho `BusManagement` khi khởi động. Khi chạy Compose, database `hcs_bus_management` được tạo trong PostgreSQL init script và connection string phải lấy từ secret môi trường.

## Endpoint chính

- `/api/bus-management/stations`
- `/api/bus-management/master-data/*`
- `/api/bus-management/operators/contracts`
- `/api/bus-management/compliance/vehicle-documents`
- `/api/bus-management/departures/*`
- `/api/bus-management/revenue/receipts`
- `/api/bus-management/expenses`
- `/api/bus-management/premises/leases`
- `/api/bus-management/reconciliation/shifts/*`
- `/api/bus-management/reconciliation/daily/close`
- `/api/bus-management/dashboard`
- `/api/bus-management/reports/revenue|departures|reconciliation|compliance`

Các mutation tài chính phát sinh outbox và audit event trong cùng DbContext/SaveChanges với mutation. Phiếu thu đã `Issued`, kỳ đã `Closed` và dữ liệu nguồn sau chốt không có đường sửa/xóa trực tiếp; adjustment là đường mở rộng cho phase 2.

## Ngoài phạm vi v1

Xe vãng lai/buýt công cộng/bãi đỗ đầy đủ, notification, workflow tài liệu, kế toán tổng hợp/VAT/công nợ, realtime và export XLSX/PDF hoàn chỉnh sẽ triển khai ở phase 2–3. API report đã là server-side và không phụ thuộc page hiện tại của UI.
