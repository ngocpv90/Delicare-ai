# MA-1 – Quy tắc mã toàn hệ thống + khách hàng theo loại (D-63) – thay đổi API cho dev FE

> **Giao diện đã làm (Claude Code, D-63 l):** `core/store.js` (`Store.codeRule`, `Store.userCode`, `Store.save` ghi chờ API, danh mục `Customer.CustomerType`), `core/ui.js` (`UI.form`: ô Mã G1 chỉ đọc "(tự sinh khi lưu)", G2 tự chữ hoa không dấu, lỗi API hiện trong form, form khách hàng theo loại, ẩn CIF qua `UI.hidden`), `modules/menu.js` (nhân bản thực đơn không nhập mã), `modules/sales.js` (số hợp đồng tự sinh, cột Loại tiếng Việt). Màn hình dùng `Store.add / update` riêng (không qua form chung) vẫn ghi lạc quan như cũ – máy chủ bỏ qua mã G1 gửi lên.

Không bảng / cột mới. Sau `git pull`: build lại API, chạy lại `DB/sp/uspVehicle_GetAll.sql`, `DB/sp/uspMaterialLot_GetAll.sql` (hoặc `db-init`).

## 1. Mã do máy chủ sinh (nhóm G1) – ô Mã chỉ đọc
| Màn / API Insert | Mã mới | Ghi chú |
|---|---|---|
| `Customer` | `KHB-NNNNN` (doanh nghiệp) / `KHC-NNNNN` (cá nhân) | `CIF = Code`, **ẩn CIF** ở form / danh sách / hồ sơ |
| `Supplier`, `Ingredient`, `Dish`, `Warehouse`, `Vehicle`, `Employee`, `CustomerDeliveryLocation`, `PricePolicy` | `NC-`, `NL-`, `MA-`, `KO-`, `XE-`, `NV-`, `DP-`, `BG-` + 5 số | Employee: ghi cả `EmployeeCode` |
| `Menu` (Insert, `Menu/Clone`) | `NU-YYMMDD-NNN` (ngày = `EffectiveFrom`) | `MenuCloneRequest.Code` không còn bắt buộc, bị bỏ qua |
| `Contract/Create`, `Contract/Insert` | `HD-YYMMDD-NNN` | `contractNumber` gửi lên bị bỏ qua (số HĐ giấy → ghi chú) |
| `PurchaseRequest/Insert` | `MH-YYMMDD-NNNN` | `requestNumber` gửi lên bị bỏ qua |
| `MaterialLot/Insert` | `LO-YYMMDD-<mã NCC>-NN` (không NCC: `TDK`) | `lotNumber` gửi lên bị bỏ qua; Update không đổi số |

- **Insert:** giá trị `Code` / số gửi lên **bị bỏ qua** (không lỗi). **Update:** mã không đổi.
- **Thông báo thành công** kèm mã: `responseMessage = "Đã tạo <đối tượng> – mã KHB-00012"` → chỉ đóng form và báo thành công **sau** khi API trả 200, hiện đúng `responseMessage`.
- Chứng từ mới đổi tiền tố (đơn `DH-`, nhập `PN-`, xuất `PX-`, kiểm kê `KK-`, đề nghị mua `MH-`, lệnh SX `LS-`, đóng gói `DG-`, đơn giao `GH-`, đổi món `DM-`, đổi ca `DC-`, giao dịch kho tay `GD-`, lô `LO-`, kế hoạch `KS-`, phiếu QC `QC-`, nhu cầu NVL `MR-`). Số cũ (`PH-`, `KH-`, `NL-`) giữ nguyên – lọc / tìm phải nhận cả hai.

## 2. API mới `GET api/Common/GetCodeRules?entity=<Entity hoặc bảng>` (chỉ cần đăng nhập)
`listData[] { entity, table, column, group: G1|G2|G3, kind, prefix, pattern, catalog, note }`
- `G1` → ô Mã chỉ đọc, hiện "(tự sinh khi lưu)", **không gửi**.
- `G2` (ca, đơn vị tính, kho/khu/vị trí, dây chuyền, nhóm món / NL…) → tự chuyển chữ hoa không dấu, chỉ A–Z 0–9 `-` `_`.
- `G3` → không áp quy tắc.

## 3. Khách hàng theo loại (`crm-customer`)
`CustomerType`: chỉ `BUSINESS` (Doanh nghiệp) / `INDIVIDUAL` (Cá nhân) – chọn **trước**, hiện ô theo loại. Loại khóa sau khi lưu (đổi chỉ khi khách chưa có hợp đồng / đơn → máy chủ cấp mã mới).

| Loại | Bắt buộc | Tùy chọn |
|---|---|---|
| Doanh nghiệp | `LegalName`, `DisplayName`, `TaxCode` (10 số hoặc 10-3 số), `LegalRepresentativeName`, `ContactPersonName`, `ContactPersonPhone`, `Address`, `PhoneNumber`, `BranchId`* | `BusinessRegistrationNumber` (trống = MST), `RegistrationDate`, `RegistrationPlace`, `Email`, `ContactPersonEmail`, `Industry`, `CompanySize`, `Source`, `UserAssignId`, `Note` |
| Cá nhân | `LastName`, `FirstName`, `Address`, `PhoneNumber`, `BranchId`* | `Gender`, `DateOfBirth` (≤ hôm nay), `IdentityNumber` (12 số), `IdentityDate`, `IdentityPlace`, `Occupation`, `Email`, `TaxCode` (10 số), `Source`, `UserAssignId`, `Note` |

\* `BranchId` (Chi nhánh phục vụ, `api/OrganizationUnit/GetBranches`) chỉ bắt buộc khi hệ thống đã có đơn vị loại BRANCH.
Máy chủ tự gán `Code`, `CIF`, `Name` (DN = Tên doanh nghiệp, CN = "Họ Tên"), `DisplayName` (CN), `CompanyId`; xóa trống trường không thuộc loại. SĐT: `0` + 9 số hoặc `+84` + 9 số (dấu cách / chấm bị bỏ). MST không trùng trong khách DN, CCCD không trùng trong khách CN.

## 4. Khác
- `api/Vehicle/GetAll?SearchText=` tìm cả biển số.
- Bot: gõ mã chứng từ mới / mã danh mục (`KHB-00012`) / mã cũ đều tra được.
