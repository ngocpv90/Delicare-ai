# UAT_CHECKLIST – kịch bản thử bằng API

Thử trên **staging** (deploy/README.md). Công cụ: Swagger (`/swagger`) hoặc VS Code REST Client (`docs/api-tests/*.http`).
Đánh dấu [x] khi đạt; ghi lỗi / góp ý vào cột "Ghi chú" hoặc báo Claude Code.

## R1.0 – Nền móng (2026-11-14) · file thử: `docs/api-tests/r1.0-foundation.http`, `docs/api-tests/r1.1-admin-masterdata.http` (T45R)

| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Sức khỏe hệ thống | `GET /health` | `Healthy`, database `Healthy`, đúng environment | [ ] | |
| 2 | Swagger | mở `/swagger` | Nhóm theo controller (`Shift`, `InventoryTransaction`, `AuditChange`, `UserRole`…); không còn route `/api/v1` (D-30) | [ ] | |
| 3 | Đăng nhập đúng / sai | `POST /api/Authentication/Login` | Đúng → token; sai mật khẩu → 400 "Tài khoản hoặc mật khẩu không đúng" | [ ] | |
| 4 | Chặn khi chưa đăng nhập | gọi API bất kỳ không token | 401 (v1: envelope `UNAUTHORIZED`) | [ ] | |
| 5 | Cơ cấu tổ chức | `GET /api/OrganizationUnit/GetAll` | 33 đơn vị đúng sơ đồ (DLC → CEO → 5 khối + Ban cố vấn; OPS-KIT có 6 tổ) | [ ] | Anh soát tên / mã |
| 6 | Nhóm quyền | `GET /api/UserGroup/GetAll` | 16 nhóm: CEO, CEO-PMO, COO, OPS-PLAN, OPS-SCM, OPS-KIT, OPS-LOG, OPS-POS, OPS-MNT, SAL-SALES, SAL-CON, SAL-CS, FIN-ACC, FIN-HR, FIN-IT, QA | [ ] | |
| 7 | Ma trận quyền | `GET /api/UserRole/GetByUserGroupId` cho từng nhóm | Đúng ma trận ORG_STRUCTURE mục 2 (PROPOSED, D-25) | [ ] | **Anh duyệt** |
| 8 | Phân quyền thật | tạo user, gán vào 1 nhóm (vd. OPS-SCM), đăng nhập user đó | Xem được màn được phép; thêm / sửa màn không được phép → 403 | [ ] | |
| 9 | Duyệt | (khi có API duyệt) user OPS-SCM duyệt kế hoạch sản xuất | Bị từ chối; COO / CEO được | [ ] | Nhóm duyệt: appsettings `Permissions:ApproverGroups` |
| 10 | Ca làm việc | `POST api/Shift/Insert` (form) | Tạo ca qua đêm được; trùng mã / giờ bắt đầu = kết thúc → responseCode 400 kèm thông báo; gửi 2 lần cùng Idempotency-Key chỉ tạo 1 | [ ] | |
| 11 | Xóa bản ghi đang dùng | `DELETE api/Shift/Delete?id=` với ca đang có phân ca | responseCode 400 "Không thể xóa dữ liệu… đang được sử dụng" | [ ] | |
| 12 | Sổ kho chỉ ghi thêm | gọi `Update` / `Delete` sổ kho; `POST api/InventoryTransaction/Reverse` 2 lần | Không có Update / Delete; đảo lần 2 → responseCode 400 "đã được đảo" | [ ] | |
| 13 | Lịch sử xử lý | `GET /api/AuditChange/GetAll?EntityName=<Entity>&IdChange=<Id>` sau khi sửa 1 bản ghi | Có dòng: thời điểm, người thao tác, nội dung thay đổi | [ ] | |
| 14 | Bảo mật | xem response đăng nhập / danh sách user | Không bao giờ thấy trường mật khẩu | [ ] | |

## R1.1 – Danh mục và quản trị (T45R + T45B.5, 2026-11-15) · file thử: `docs/api-tests/r1.1-admin-masterdata.http` · tài liệu: `docs/frontend-handoff/P0-P2.md`

| # | Kịch bản | Thao tác | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Ma trận quyền | `UserRole/GetMatrix` 1 nhóm → bật quyền 1 menu bằng `UserRole/Update` → user nhóm đó gọi lại ngay | Quyền áp dụng ngay (không chờ 5′) | [ ] | |
| 2 | Quyền duyệt | Nhóm OPS-PLAN không có mục "Duyệt – Công thức" → `Recipe/Approve` | 403; cấp `allowEdit` trên mục đó → duyệt được | [ ] | |
| 3 | Nhóm mới | `UserGroup/Insert` rồi xem ma trận | Mọi cờ = false (PROPOSED) | [ ] | |
| 4 | Khóa tài khoản | `User/SaveIsActive` user khác; thử tự khóa mình | User bị khóa → 403 ngay; tự khóa → responseCode 400 | [ ] | |
| 5 | Menu | `ObjectMenu/GetTree`, `UpdateOrder` tạo vòng lặp | Cây đúng; vòng lặp → responseCode 400 | [ ] | |
| 6 | Khách 360° | `Customer/GetOverview` | Liên hệ, điểm giao, hợp đồng + suất, 15 đơn gần nhất, KPI | [ ] | |
| 7 | Điểm giao | Ngừng điểm giao còn đơn chưa giao | responseCode 400 | [ ] | |
| 8 | Giá NCC | 2 NCC cùng nguyên liệu bật ưu tiên; `SupplierMaterial/GetAll?EffectiveOn=` | Chỉ còn 1 ưu tiên; lọc theo ngày đúng | [ ] | |
| 9 | Quy đổi | `UomConversion/Convert` kg → g, g → kg, bao → g qua kg | 1000 / 0,001 / qua trung gian đúng; không có quy đổi → 400 | [ ] | |
| 10 | Công thức | Tạo phiên bản → dòng → gửi duyệt → duyệt → khóa; tạo V2 sao chép → khóa | V1 Obsolete, EffectiveTo = ngày trước V2; giá thành chuẩn mới; sửa dòng khi không Draft → 400 | [ ] | |
| 11 | Giá vốn | `Recipe/CalculateCost?mealCount=50` | Đúng theo giá NCC ưu tiên + quy đổi; dòng thiếu giá có cảnh báo | [ ] | |
| 12 | Chính sách giá | `PricePolicy/Resolve`; thêm chính sách chồng hiệu lực | Thứ tự hợp đồng → khách → ContractMeal; chồng → 400 | [ ] | |
| 13 | Trạng thái / tham chiếu | `MetaData/GetStatuses`, `MetaData/GetReferenceLists` | 47 bộ trạng thái; MealType SANG / TRUA / TOI / DEM (PROPOSED) | [ ] | |
