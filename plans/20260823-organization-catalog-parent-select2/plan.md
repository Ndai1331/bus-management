# Organization catalog parent Select2

## Objective

Đổi các trường liên kết danh mục/phòng ban trong form Organization catalog sang hạ tầng `CatalogSelect2` đang có, giữ nguyên API và hành vi create/edit.

**Status:** Complete

## Findings

- `src/HCS.Blazor.Client/Pages/Organization/OrganizationCatalog.razor` đang dùng Blazorise `<Select>` cho phòng ban cấp trên và phòng ban của đơn vị.
- `CatalogSelect2` đã có sẵn JS interop, tìm kiếm từ xa/local, clear selection và hỗ trợ modal dropdown.
- `OrganizationCatalog.Data.cs` đã tải đầy đủ department lookup trước khi mở form; không cần thêm endpoint hoặc thay đổi contract.
- Khi sửa phòng ban, danh mục hiện tại phải bị loại khỏi danh sách parent để giữ invariant backend.

## Implementation

1. Thay hai select liên kết trong form bằng `CatalogSelect2`.
2. Bổ sung callback chuyển `Guid?` của Select2 về các trường string hiện có trong form model.
3. Bổ sung search theo mã/tên, text item cho giá trị đang chọn, và loại phòng ban hiện tại khỏi parent options.
4. Giữ validation thủ công hiện tại cho department bắt buộc và không thay đổi request/API.

## Files

- Modify `src/HCS.Blazor.Client/Pages/Organization/OrganizationCatalog.razor`.
- Modify `src/HCS.Blazor.Client/Pages/Organization/OrganizationCatalog.Requests.cs`.
- Modify `src/HCS.Blazor.Client/Pages/Organization/OrganizationCatalog.Data.cs` nếu cần để backing search helper rõ ràng.

## Verification

- Build client/host hoặc solution với `--no-restore` nếu dependency đã có.
- Kiểm tra không còn `<Select>` cho các trường parent/department trong form catalog.
- Kiểm tra create, edit, clear parent, chọn department cho unit và loại self-parent.

## Verification results

- `./scripts/audit-license-clean.sh` — passed.
- `dotnet build src/HCS.Blazor.Client/HCS.Blazor.Client.csproj --no-restore` — passed, 0 warnings/errors.
- `dotnet build src/HCS.Blazor/HCS.Blazor.csproj --no-restore` — passed, 0 warnings/errors.
- `dotnet test HCS.slnx --no-build --no-restore` — passed; all discovered tests passed.
- `git diff --check` — passed.
