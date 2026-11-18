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
| 14 | Nhập Excel – mẫu | Tải mẫu Customer / Ingredient / Dish / RecipeLine / SupplierMaterial, điền vài dòng (cố ý 1 dòng thiếu cột *) rồi `ImportExcel/Import` | Dòng đúng được nạp; dòng lỗi báo đúng số dòng + lý do; nạp lại không trùng | [ ] | |
| 15 | Nhập Excel – file thật | Anh tự nạp `Rule Menu.xlsx` (RuleMenu_DishBank rồi RuleMenu_DishDetail) và `Tool_Tong hop bao gia` (BaoGia_Summary) | Món / nhóm món / nguyên liệu / NCC / bảng giá đúng; ghi chú GAP-IMP-01 (không có định lượng), GAP-IMP-02 (VAT) | [ ] | Chỉ anh nạp dữ liệu thật |

## PILOT-FE – Giao diện (thử tay trên trình duyệt) · FE-0, FE-1 (2026-11-16)

| # | Màn hình | Thao tác | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Khung (FE-0) | Đăng nhập; tải lại trang; đổi mật khẩu; đăng xuất; để token hết hạn | Menu theo quyền; tải lại vẫn đúng menu; đổi mật khẩu xong đăng nhập bằng mật khẩu mới; 401 → về đăng nhập | [ ] | |
| 2 | Khung | Sidebar, ô chọn chi nhánh, màn `scm-uom`, máy tính bảng | Nhóm cấp 1 → màn cấp 2; chi nhánh lấy từ OrganizationUnit; chữ / nút đủ lớn; không còn chữ "phòng khám" / "W3CRM" | [ ] | |
| 3 | adm-user | Tạo user 2 nhóm + chọn hồ sơ → đăng nhập (mật khẩu = tên); sửa nhóm; khóa / mở; tự khóa mình; reset mật khẩu (admin) | Đúng; các trường khác không mất sau khi sửa; tự khóa → thông báo lỗi; nút reset chỉ admin thấy | [ ] | |
| 4 | adm-usergroup | Thêm / sửa / xóa; xóa nhóm đang dùng; nút "Phân quyền" | Xóa nhóm đang dùng → thông báo; "Phân quyền" mở adm-permission đúng nhóm | [ ] | |
| 5 | adm-permission | Tick Sửa → tự bật Truy cập + nhóm cha; bỏ Truy cập → xóa dòng; Lưu; Cấp / Thu hồi tất cả; Sao chép từ nhóm; Xem trước (admin); dòng "Duyệt" | Tải lại thấy đúng cờ; user nhóm đó thấy / mất màn hình sau khi đăng nhập lại; dòng duyệt lưu được | [ ] | |
| 6 | adm-objectmenu | Kéo thả / lên xuống; ẩn / hiện; thêm con; sửa; xóa | Thứ tự sidebar khớp sau khi tải lại | [ ] | |
| 7 | adm-orgunit | Cây; thêm con; đổi quản lý; đặt cha là con của mình; trùng mã | Vòng lặp / trùng mã → thông báo; đơn vị BRANCH hiện ở ô chọn chi nhánh | [ ] | |
| 8 | adm-employee, adm-position | Thêm / sửa (ngày không lệch 1 ngày, trường khác giữ nguyên) / lọc / xóa | Đúng | [ ] | |
| 9 | Mọi màn | User thường không có quyền Thêm / Sửa / Xóa | Nút ẩn đúng; không có quyền xem → trang "không tìm thấy" | [ ] | |
### T45R.8 – Hoàn thiện quản trị người dùng (backend, 2026-11-16) · file thử: `docs/api-tests/t45r8-admin-users.http` · tài liệu: `docs/frontend-handoff/P0-P2.md` mục 6
Trước khi thử trên staging: chạy `DB/seed/ObjectMenu_disable_legacy.sql` (hoặc `deploy/db/Deploy-Database.ps1` – đã có trong danh sách seed) và `DB/sp/uspUser_GetAll.sql`.

| # | Kịch bản | Thao tác | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Tắt menu cũ (D-37) | Chạy script 2 lần; đăng nhập xem `listMenu` | Lần 1 tắt 26 mục (21 cấp 2 + 5 cấp 1), lần 2 tắt 0, không lỗi; không còn "Quản lý…", "Khám bệnh", "Kho thuốc"… | [ ] | Không xóa, không đổi UserRole |
| 2 | Tạo người dùng 1 bước (D-38) | `Person/Insert` có Họ tên + `UserName` + `ListUserGroupId` + `IsActive=true` | Đúng 1 Person + 1 User + nhóm; response có `tempPassword` 12 ký tự | [ ] | |
| 3 | Chặn tài khoản thứ 2 / trùng tên | `User/Insert` cho hồ sơ vừa tạo; `Person/Insert` với tên đăng nhập trùng (khác hoa thường) | responseCode 400, không tạo hồ sơ thừa | [ ] | |
| 4 | Mật khẩu tạm (D-39) | Admin `User/ResetPassword` → đăng nhập bằng mật khẩu tạm → gọi 1 màn bất kỳ → `User/ChangePassword` → gọi lại | Login có `mustChangePassword = true`; API khác 403 "Cần đổi mật khẩu"; đổi xong dùng bình thường; mật khẩu tạm không có trong `AuditChange` (tab Lịch sử của User chỉ ghi "PassWord: đã thay đổi") | [ ] | |
| 5 | Mật khẩu tạm hết hạn | Đăng nhập bằng mật khẩu tạm sau 24 giờ | "Mật khẩu tạm đã hết hạn, liên hệ quản trị" | [ ] | Có test tự động (TestClock) |
| 6 | FE-1 follow-ups | `User/GetAll?IsActive=false`; `UserRole/SaveAccessAll`; `ObjectMenu/Insert` dưới mục cấp 2 | Chỉ user bị khóa; nhóm menu cấp 1 cũng được cấp; menu cấp 3 → 400 | [ ] | |

## TOÀN LUỒNG R1 bằng API (BE2.9, 2026-11-17) · file thử: `docs/api-tests/full-flow.http` · test tự động: `AppDelicare.Tests/Pilot/FullFlowSqlServerTests.cs`
**Thay phạm vi PILOT bên dưới (D-44: không chạy Pilot – kiểm theo toàn luồng).** Thử trên **staging** (`http://localhost:5200`, deploy/README.md mục 9 – khởi động lại theo mục 9 bước 4) hoặc dev. Chạy `full-flow.http` **từ trên xuống**; mỗi lần đổi `@key`, `@day` (ngày ăn tương lai chưa có kế hoạch SX, ≤ hôm nay + 20 ngày), `@dow`; trên staging đổi `@base` + `@password`. Lỗi nghiệp vụ = HTTP 200 + `responseCode` 400 + thông báo tiếng Việt. Dòng `# @expect` = số liệu cần thấy.
**Vai trò:** lần 1 chạy bằng `admin`; lần 2 đăng nhập đúng nhóm từng bước (cột "Vai trò") – thiếu quyền màn → HTTP 403, thiếu mục duyệt → 400 "không có quyền duyệt". Kết quả chạy thật: dev 216 / 216 (22 chốt an toàn, 60 @expect – key B29F2 / B29F3), staging 216 / 216 (key B29S, ngày 01/12/2026).

| # | Phân hệ – kịch bản (mục trong file) | Vai trò (nhóm gợi ý) | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Danh mục tối thiểu (0): đơn vị, kho, NL bò (MÁT) / gạo (KHÔ), NCC + bảng giá, 2 món + công thức (CreateVersion → dòng → Submit → Approve → Lock), khách + điểm giao 11:00, xe, dây chuyền 1000 suất / mẻ | OPS-PLAN, OPS-SCM, SAL-SALES | Mọi bước 200; công thức Đã khóa | [ ] | |
| 2 | **Chốt – lô bị chặn** (0.10–0.11): lô bò hết hạn 50 kg + lô `KHONG_DAT` 30 kg qua `InventoryTransaction/Adjust` → `ApprovalRequest/Decide` | OPS-SCM; duyệt: `scm-adjustment` | `pendingApproval = true`, duyệt xong mới ghi sổ; các bước sau không dùng 2 lô này | [ ] | |
| 3 | Hợp đồng (1): `Contract/Create` (kèm suất TRƯA 35.000) → `Submit` → `Approve` → `GetDetail` | SAL-SALES; duyệt: `crm-contract` | Số `HD-YYMMDD-NNN` Nháp → Hiệu lực; thêm loại suất khi đã Hiệu lực → 400 | [ ] | |
| 4 | Dự báo (2): `Forecast/Import` 1000 suất → `Publish` → `GetMatrix` | SAL-SALES / OPS-PLAN | Ma trận 1000 (đã công bố) | [ ] | |
| 5 | Thực đơn + đơn (3): menu → món → `GetCalendar` → `Approve` → phân công; `Order/GetNewTemplate` → `Create` 1000 → `Submit` → `Confirm` → `Lock` | OPS-PLAN; SAL-CS | Gợi ý `suggestedQuantity = 1000`; đơn `PH-YYMMDD-NNNN` 35.000.000; lệch dự báo 0% (không cảnh báo); Đã chốt | [ ] | |
| 6 | Hàng sắp về (4): đề nghị mua tay gạo 50 kg → Submit → duyệt 2 cấp → PO 1.000.000 (1 cấp) → gửi NCC | OPS-SCM; duyệt `scm-pr-l1`, `scm-pr-l2`, `scm-po` | PO Đã gửi, chưa nhận | [ ] | |
| 7 | MRP (5): `MaterialRequirement/Calculate` → `ExportPreProduction` | OPS-PLAN | Bò cần 200, bị chặn 80, mua 200; gạo cần 120, **hàng sắp về 50**, mua 70; KH- Nháp + đề nghị Nháp | [ ] | |
| 8 | Đề nghị 2 cấp + PO Giám đốc (6): Convert khi Nháp → 400; Submit → cấp 1 → `GetDetail` (`stepNo 2`) → cấp 2 → Convert → PO 61.400.000 → Submit; hộp `scm-po` **không** có, `scm-po-director` có → GĐ duyệt → Send → in PO | OPS-SCM; COO; CEO (GĐ) | `needsDirectorApproval = true`; người chỉ có `scm-po` duyệt → 400 | [ ] | |
| 9 | Nhận hàng + QC (7): `GoodsReceipt/Create` → Post trước QC → 400; bò 9 °C DAT → 400; QC 3,5 °C → Post; nhận PO định kỳ gạo 50 (hạn +20 ngày); in phiếu nhập xlsx / docx, pdf → 400 | OPS-SCM (kho), QA | Mỗi dòng 1 lô `NL-YYMMDD-<NCC>-NN` khác số; TemperatureLog RECEIVING; PO Đã nhận | [ ] | PDF: D-56 b |
| 10 | Kho (8): `InventoryBalance/GetSummary`; xuất tay đích danh lô hết hạn → 400 | OPS-SCM | Chặn lô hết hạn | [ ] | |
| 11 | Kế hoạch + giữ chỗ (9): `ProductionPlan/Approve` → `Release` → `ProductionOrder/GetDetail` | OPS-PLAN; duyệt kế hoạch | 2 mẻ `ME-YYMMDD-TRUA-NN` (Bò kho trước), giữ chỗ bò 200 (`listReservation`) | [ ] | |
| 12 | **Sản xuất theo tổ** (10): NAU trước SO_CHE → 400; SO_CHE → xuất NL FEFO (không lô -HH / -KD) → NAU (= bắt đầu mẻ) → Finish trước xong NAU → 400 → lõi 65 °C thiếu khắc phục → 400 → 65 + khắc phục → Finish / sản lượng 1000 → Release → 400 (lõi < 70) → 75,5 °C → lưu mẫu → CHIA_SUAT trước Đạt → 400 → Release → CHIA_SUAT → `GetStageBoard` | OPS-KIT (tổ sơ chế / nấu / chia), QA | Mọi chốt 400 đúng; bảng tiến độ theo tổ | [ ] | |
| 13 | Đóng gói (11.1–11.3): `PackingOrder/GenerateFromBatch` → `ProductionBatch/Pack` → 400 → xong CHIA_SUAT → `Start` → `RecordResult` 1000 | OPS-KIT | Mẻ Đã đóng gói, 3 công đoạn Đã xong | [ ] | |
| 14 | **Checklist lỗi trọng yếu** (11.4–11.9): mẻ Cơm xuất FEFO 2 lô (lô định kỳ 50 trước) → nấu 82 °C → Đạt → QA_FG Cảm quan FAIL → `Release` → 400 → kiểm lại ĐẠT → Release → Pack (chưa lưu mẫu); `MealCost/Calculate` khi chưa giao → 400 | QA, OPS-KIT | `listCriticalFailure` ≥ 1; kế hoạch Completed | [ ] | |
| 15 | **Giao hàng** (12): chuyến `CX-` → 55 °C → Dispatch 400 → 65 °C nhưng mẻ chưa lưu mẫu → 400 (liệt kê `ME-`) → lưu mẫu → chưa xếp hàng → 400 → `Load` → Dispatch → Arrive → bàn giao 58 °C → 400 → 63 °C giao 1000 / nhận 998 | OPS-LOG (tài xế) | Chuyến Hoàn thành; POD; sổ bao bì OUT 998; phản ánh tự động (2 suất từ chối) | [ ] | |
| 16 | In / tệp giao hàng (12.11–12.13): biên bản giao chuyến, phiếu giao, POD; tải lên PDF bằng chứng POD → danh sách → tải xuống; .exe → 400 | OPS-LOG | Tệp lưu ngoài wwwroot (`App_Data/DocumentFiles`, tên SHA-256) | [ ] | |
| 17 | Hộp / khay (13.1–13.5): thu hồi 990 (hỏng 5) → kiểm tra DIRTY → mẻ vệ sinh tự lập → khử khuẩn thiếu hóa chất → 400 → 6 bước → PASS → số dư | OPS-LOG, QA | OUT 998 / IN 990 / còn 8; AVAILABLE 985 | [ ] | |
| 18 | Đối soát + phản ánh (13.6–13.8): `Reconcile` 990 / trả 5 / hủy 3 → `Close` → in biên bản; phản ánh High → Assign → Resolve → Close | SAL-CS / FIN-ACC; QA | 34.650.000; đơn Reconciled; khách có 2 phản ánh | [ ] | |
| 19 | Giá thành (14): đóng 2 lệnh SX (đóng lại → 400) → `ActualCost/GetList` → chênh lệch tiêu hao → `MealCost/Calculate` → `GetList` | OPS-PLAN / FIN-ACC | NVL 62.400.000 (bò 60.000 đ / phần); 990 suất nhận, giá vốn 62.400.000, doanh thu 35.000.000 | [ ] | Giá bán [DEMO] < giá vốn do số thử |
| 20 | Kiểm điểm dùng chung (15): phiếu xuất, in hàng loạt theo chuyến, thống kê trạng thái, xuất xlsx / csv, lịch sử PO + POD (có tệp), truy xuất ngược mẻ | Mọi nhóm có quyền In / Xuất / Xem | Tệp xlsx / docx; lịch sử có quyết định duyệt + tệp; truy xuất tới khách | [ ] | |
| 21 | Lô hết hạn (16): `MaterialLot/ProcessExpired` Preview → xử lý → chạy lại | OPS-SCM | Lô -HH Hết hạn + WASTE 50; lần 2 `expiredLotCount = 0` | [ ] | |
| 22 | Nhân sự smoke (17): nhân viên + trạm bếp có tọa độ → phân ca hôm nay → lưới ca → chấm hộ PROXY thiếu lý do → 400 → vào ca GPS ~22 m → bảng công | FIN-HR / tổ trưởng | Vào ca hợp lệ, không trả tọa độ | [ ] | Đủ kịch bản HR: `be2.6-hr.http` |
| 23 | Bảng điều hành thiếu ngày (lỗi BE2.9 đã sửa): `DeliveryExecution/GetBoard`, `DeliveryRoute/GetBoard`, `GetReadyOrders`, `ProductionBatch/GetBoard`, `GetStageBoard`, `Forecast/GetMatrix` không truyền ngày | mọi nhóm | 400 "Chọn ngày giao" / "Chọn ngày sản xuất" / "Khoảng ngày không hợp lệ" (trước: HTTP 500) | [ ] | |

Kịch bản chi tiết từng phân hệ (nhiều nhánh / chốt hơn): `be2.1-flow-gaps.http` (đóng gói, xếp xe, đơn giao), `be2.2-contract-forecast.http` (hợp đồng, dự báo, đổi món, sửa đơn sau giờ chốt), `be2.3-multilevel-approval.http` (duyệt nhiều cấp, kiểm kê), `be2.4-inventory-production.http` (giữ chỗ, hủy lệnh, công đoạn, giá thành), `be2.5-foodsafety-packaging.http` (ATTP, vệ sinh, hộp / khay, sự cố NCC), `be2.6-hr.http` (nhân sự), `be2.7-common-print.http` (in / xuất / tệp / lịch sử).

## DASHBOARD ĐIỀU HÀNH + HÀNH TRÌNH ĐƠN (BE3.5, 2026-11-17) · file thử: `docs/api-tests/be3.5-dashboard.http` · test: `AppDelicare.Tests/Dashboard/DashboardTests.cs`
| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Số liệu khớp danh sách | `Dashboard/GetExecutive?date=2026-12-02` so với `Order/GetBoard`, `ProductionBatch/GetBoard`, `DeliveryExecution/GetBoard`, chuông | Số đơn / trạng thái, suất, tiến độ, giao, tồn, phản ánh trùng khớp | [ ] | mẻ Hủy không tính (D-57 ae) |
| 2 | Lọc ca / chi nhánh | Chọn 1 ca, 1 chi nhánh | Suất / đơn / mẻ / ca làm chỉ của ca / chi nhánh (gồm đơn vị con); giao không lọc ca | [ ] | |
| 3 | Cache 60 giây | Tạo 1 đơn mới cho ngày đang xem, tải lại ngay rồi sau 60 giây | Lần đầu số cũ, sau 60 giây số mới (`generatedAt` đổi) | [ ] | |
| 4 | Cảnh báo theo người | Mở dashboard bằng admin rồi bằng người chỉ có quyền Xem dashboard | Thẻ số giống nhau; khối cảnh báo của người thứ 2 chỉ theo quyền của họ | [ ] | |
| 5 | KPI ngày | Bấm KPI Refresh (BE3.4) cho 1 ngày rồi mở dashboard ngày đó | 4 chỉ tiêu có giá trị / mục tiêu / đạt (chi phí càng thấp càng tốt) | [ ] | chưa tính → trống |
| 6 | Hành trình đơn | Bấm 1 đơn đã đối soát → `Dashboard/GetOrderJourney` | 8 chặng, số KH / lệnh SX / mẻ / đóng gói / đơn giao / chuyến / POD / đối soát / phản ánh đúng; chặng hiện tại đúng | [ ] | KH- dùng chung cả ngày |
| 7 | Quyền hành trình | Người chỉ Xem đơn hàng mở hành trình | Thấy trạng thái mọi chặng nhưng chỉ chứng từ đơn; người không có quyền → 403 | [ ] | |

## CHUÔNG CẢNH BÁO + TÌM KIẾM + LIÊN QUAN (BE3.3, 2026-11-17) · file thử: `docs/api-tests/be3.3-alerts-search.http` · test: `AppDelicare.Tests/Common/AlertsSearchRelatedTests.cs`
| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Chuông theo quyền | Đăng nhập thủ kho (chỉ Xem kho / lô) → `Common/GetAlerts` | Chỉ cảnh báo tồn / lô; không thấy phản ánh / nhiệt độ | [ ] | |
| 2 | Chuông theo chi nhánh | Chọn chi nhánh trên header | Số tồn / lô / phiếu nhập / phản ánh chỉ của chi nhánh (dữ liệu chưa gắn chi nhánh vẫn hiện) | [ ] | |
| 3 | Nhiệt độ IoT | Gửi 1 lần đo vượt ngưỡng qua `IotTemperature/Receive`, đợi ≤ 60 giây | `TEMPERATURE_OUT_OF_RANGE` tăng 1 | [ ] | cache 60 giây |
| 4 | Duyệt | Gửi duyệt 1 công thức; người có quyền Duyệt mở chuông | `APPROVAL_PENDING` = 1, bấm → `#/wf-inbox` | [ ] | |
| 5 | Tìm kiếm | Gõ "don hang", mã khách, số PO | Thấy màn hình đúng quyền; bản ghi ≤ 5 / loại; không có tên bảng | [ ] | |
| 6 | Liên quan | Mở khách hàng / lô → tab Liên quan | Đơn / hợp đồng / phản ánh; lô → phiếu nhập, phiếu xuất, mẻ dùng lô | [ ] | |
## KPI (BE3.4, 2026-11-17) · file thử: `docs/api-tests/be3.4-kpi.http` · test: `AppDelicare.Tests/Kpi/KpiTests.cs`
Cần: đã chạy `DB/seed/KpiMetricDefinition_seed.sql`; tài khoản admin để bấm làm mới.
| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Danh sách chỉ tiêu | `#/kpi-definition` | 11 chỉ tiêu, có mục tiêu + chiều (≥ / ≤); công thức + mục tiêu đúng ý BA / PO (PROPOSED D-57 y) | [ ] | |
| 2 | Phiên bản | Sửa mục tiêu 1 chỉ tiêu | Có phiên bản mới hiệu lực từ hôm nay, bản cũ đóng hôm qua, số KPI cũ giữ mục tiêu cũ; sửa bản cũ → báo lỗi | [ ] | |
| 3 | Làm mới KPI ngày | Admin bấm làm mới 1 ngày có đơn giao / mẻ | Số khớp tay (vd. giao đúng giờ = đơn trễ ≤ 15′ ÷ đơn đã giao + thất bại); bấm lại không nhân đôi | [ ] | |
| 4 | Thiếu dữ liệu | Làm mới ngày không có dữ liệu | Chỉ tiêu hiện "Thiếu dữ liệu" (INCOMPLETE), không đỏ / xanh | [ ] | |
| 5 | Đạt / không đạt | KPI ngày / tháng | Hao hụt, thất thoát, food cost: thấp hơn mục tiêu = đạt; giao đúng giờ, lưu mẫu: cao hơn = đạt | [ ] | |
| 6 | KPI tháng | Làm mới tháng này | Số tính trên cả tháng tới hôm nay | [ ] | |
| 7 | Quyền | User không phải admin | Không bấm được làm mới (403); xem KPI theo quyền Xem màn | [ ] | |
| 8 | Nhật ký | `#/kpi-refresh` | Mỗi lần bấm 1 dòng Thành công, người chạy, số dòng; đang chạy trùng → "Đang chạy ở nơi khác" | [ ] | |

## IOT NHIỆT ĐỘ (BE3.2, 2026-11-17) · file thử: `docs/api-tests/be3.2-iot.http` · test: `AppDelicare.Tests/Iot/IotTemperatureTests.cs`
Cần: `Iot__ApiKeys__0` + 1 thiết bị `Iot__Devices__<mã>__StorageZoneCode` (biến môi trường của API – deploy/README.md).
| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Khóa API | Gửi không khóa / sai khóa | HTTP 401 | [ ] | |
| 2 | Nhận lô | Gửi 1 lô (đạt, vượt ngưỡng, thiết bị lạ) | Đạt + vượt ngưỡng được ghi (IOT, người ghi system); thiết bị lạ trong `rejected` | [ ] | |
| 3 | Gửi lại | Gửi lại cùng lô | `accepted = 0`, `duplicate` = số đã ghi | [ ] | |
| 4 | Biểu đồ | `TemperatureLog/GetSeries` khu vực của thiết bị | Thấy lần đo IoT, lần vượt ngưỡng đỏ | [ ] | |

## BIỂU ĐỒ + SỰ KIỆN GIAO (BE3.1, 2026-11-17) · file thử: `docs/api-tests/be3.1-charts.http` · test: `AppDelicare.Tests/Charts/ChartTests.cs`
| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Biểu đồ đối soát | `OrderReconciliation/GetList` tháng có đơn | `chart` đủ mọi ngày; tổng Đặt / Giao / Nhận khớp màn đối soát từng đơn | [ ] | |
| 2 | Hao hụt theo loại / món / ca / ngày | `MealWasteRecord/GetSummary` 4 kiểu groupBy | Tổng suất bằng nhau ở 4 kiểu; giá trị = suất × giá thành chuẩn | [ ] | |
| 3 | Biểu đồ nhiệt độ kho | `TemperatureLog/GetSeries` khu vực MÁT | Ngưỡng = khu vực; lần đo vượt ngưỡng hiện đỏ | [ ] | |
| 4 | Timeline đơn giao | `DeliveryOrder/GetEvents` 1 đơn đã giao | Xếp hàng → xuất phát → đo nhiệt → đến điểm → bàn giao, đúng thứ tự giờ | [ ] | |
| 5 | Vị trí xe | Tài xế `DeliveryEvent/RecordPosition` khi đang giao → `GetVehiclePositions` | Điểm mới nhất của chuyến = vị trí vừa ghi; chuyến chưa xuất phát → 400 | [ ] | |

## JOB TỰ ĐỘNG (T23, 2026-11-17) · file thử: `docs/api-tests/t23-jobs.http` · test tự động: `AppDelicare.Tests/Jobs/JobTests.cs`
Chạy `AppDelicare.Job` cạnh API (deploy/README.md mục "Job tự động"). Kiểm bằng admin; giờ theo giờ Việt Nam.

| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Danh sách job | `dotnet AppDelicare.Job.dll --list` và `GET api/Job/GetAll` | 5 job, giờ 20:00 / 20:30 / 00:15 / 00:30 / 08:00, lần chạy kế tiếp đúng giờ VN | [ ] | |
| 2 | Khóa đơn 20:00 | Có đơn Đã xác nhận ngày mai → chờ 20:00 (hoặc `Job/Run` LockOrders sau 20:00) | Đơn → Đã khóa; `lastRun.message` liệt kê đơn chưa xác nhận / chờ duyệt | [ ] | Trước 20:00 → Skipped "chưa đến giờ chốt" |
| 3 | MRP 20:30 | Sau bước 2 | Kế hoạch KH- ngày mai, đề nghị mua Nháp cho nguyên liệu thiếu | [ ] | |
| 4 | Lô hết hạn 00:15 | Có lô Khả dụng hạn hôm qua | Lô → Hết hạn, phiếu hao hụt PH- | [ ] | |
| 5 | Hợp đồng hết hạn 00:30 | Hợp đồng Hiệu lực hết hiệu lực hôm qua (`Contract/ExpireOverdue` preview trước) | Hợp đồng → Hết hạn, ghi chú "Hết hạn ngày …" | [ ] | |
| 6 | Chốt vắng 08:00 | Ca hôm qua không chấm vào | Phân ca → Vắng | [ ] | |
| 7 | Lỡ giờ | Tắt Job qua 20:00, bật lại trước 24:00 | Chạy bù LockOrders / Mrp ngay khi bật | [ ] | |
| 8 | Chống chạy trùng | Chạy 2 bản Job cùng lúc / bấm `Job/Run` khi job đang chạy | Bản thứ 2: Locked "đang chạy ở nơi khác" | [ ] | |
| 9 | Quyền | User thường gọi `api/Job/GetAll` | HTTP 403 | [ ] | |
| 10 | Người ghi | Xem lịch sử bản ghi (`Common/GetHistory`) của đơn do job khóa | Người thao tác = `system` | [ ] | |

## PILOT – Luồng P3 → P8 bằng API (T50R1, 2026-11-17) – **ĐÃ THAY bằng mục TOÀN LUỒNG R1 ở trên (BE2.9)**, giữ để đối chiếu · file thử: `docs/api-tests/pilot-flow.http` · test tự động: `AppDelicare.Tests/Pilot/PilotFlowSqlServerTests.cs`
Thử trên **staging** (`http://localhost:5200`, deploy/README.md mục 9) hoặc dev. Chạy `pilot-flow.http` **từ trên xuống** (đổi `@key`, `@day`, `@dow` mỗi lần). Lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + thông báo tiếng Việt; không có quyền màn hình = HTTP 403.
**Vai trò / token:** chạy lần 1 bằng `admin` (mọi quyền) để thấy luồng thông suốt; lần 2 đăng nhập **đúng người** từng bước (cột "Vai trò") – quyền = ma trận `UserRole` × mục menu (D-31), nhóm gợi ý theo ORG_STRUCTURE (PROPOSED – anh chỉnh trên màn phân quyền). Quyền duyệt = `AllowEdit` trên mục "Duyệt – …" (`#/wf-inbox?type=<màn>`).

| # | Bước (thứ tự request) | Vai trò (nhóm gợi ý) | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Danh mục: đơn vị, kho, NL (bò MÁT, gạo KHÔ), NCC + bảng giá, món + công thức (CreateVersion → RecipeLine → Submit → Approve → Lock), khách + điểm giao (giao 11:00), hợp đồng Active + suất TRUA 35.000, xe, dây chuyền 1000 suất / mẻ (`pilot-flow.http` mục 0) | OPS-SCM, OPS-PLAN, SAL-SALES; duyệt công thức: mục "Duyệt – Công thức" | Mỗi Insert `responseCode 200`; công thức Locked; danh sách `GetAll` không lọc vẫn thấy dòng | [ ] | |
| 2 | **Chốt an toàn – lô bị chặn**: lô bò hết hạn hôm qua 50 kg + lô QC `KHONG_DAT` 30 kg (`MaterialLot/Insert` + `InventoryTransaction/Adjust`) | OPS-SCM | Ghi được tồn; các bước sau **không** dùng 2 lô này | [ ] | |
| 3 | P3 Thực đơn tuần: `Menu/Insert` → `MenuLine/Insert` ×2 → `Menu/GetCalendar` (FC%) → `Menu/Approve` → `MenuAssignment/Insert` | OPS-PLAN; duyệt: mục "Duyệt – Thực đơn & phiên bản" | Lịch có giá vốn / FC%; thực đơn Approved; `MenuAssignment/Resolve` ra thực đơn | [ ] | |
| 4 | P3 Đơn hàng: `Order/GetNewTemplate` → `Order/Create` 1000 suất → `Submit` → `Confirm` → `Lock` | SAL-CS | Số `PH-YYMMDD-NNNN`, tổng 35.000.000; trạng thái Draft → Submitted → Confirmed → Locked. Sau 20:00 ngày trước ngày ăn: tạo / sửa / xác nhận → 400 (giờ chốt) | [ ] | |
| 5 | P5 MRP: `MaterialRequirement/Calculate` (ngày ăn, kho bếp) → `ExportPreProduction` | OPS-PLAN | Kế hoạch `KH-YYMMDD` Nháp + đề nghị mua `PH-` Nháp; bò cần 200 kg, `blockedQuantity` 80 (lô hết hạn + KHONG_DAT không tính), mua 200; gạo 120; file Phiếu trước SX mở được | [ ] | |
| 6 | P6 PO: `PurchaseRequest/ConvertToPurchaseOrder` → `PurchaseOrder/GetDetail` → `Submit` | OPS-SCM | PO `PO-YYMMDD-NNNN` 62.400.000, `needsDirectorApproval = true`, `approvalScreenId = scm-po-director`; Submit → Chờ duyệt (PO ≥ 50 triệu không tự duyệt) | [ ] | |
| 7 | **Chốt an toàn – Giám đốc duyệt PO ≥ 50 triệu**: `ApprovalRequest/GetMyPending?type=scm-po` và `?type=scm-po-director`; người chỉ có quyền "Duyệt – Đơn mua hàng" gọi `PurchaseOrder/Approve` | COO (scm-po) rồi CEO (scm-po-director) | Hộp `scm-po` **không** có PO; hộp `scm-po-director` có; người chỉ có `scm-po` → 400 "không có quyền"; CEO duyệt → Approved + `ApprovalDecision`; `Send` → Đã gửi NCC; `ExportOrder` có cột VAT | [ ] | |
| 8 | P6 Nhận hàng: `GoodsReceipt/Create` (nhận đủ) → `GoodsReceipt/Post` trước QC | OPS-SCM (kho) | Phiếu `PH-` Chờ QC; Post trước QC → 400 | [ ] | |
| 9 | **Chốt an toàn – nhiệt độ nhận 3 vùng**: `QcInspection/Conclude` bò DAT 9 °C → 400; (thêm) 7 °C DAT không `remark` → 400, có `remark` → nhận có điều kiện; đông lạnh −16 °C thiếu `noThawConfirmed` → 400 | QA | Mát ≤ 5 nhận / 5–8 có điều kiện (bắt buộc lý do, lô "ưu tiên dùng trước", sự cố NCC) / > 8 loại (chỉ KHONG_DAT); đông lạnh ≤ −18 / −18…−15 / > −15; hàng khô không kiểm | [ ] | |
| 10 | P6 QC đạt 3,5 °C → `GoodsReceipt/Post` | QA → OPS-SCM | Mỗi dòng 1 lô **khác số** `NL-YYMMDD-<mã NCC>-01/02`; nhập kho phần đạt; TemperatureLog RECEIVING; PO → Đã nhận | [ ] | Lỗi trùng số lô đã sửa ở T50R1 |
| 11 | P7 `ProductionPlan/Approve` → `Release` (ShiftId trống = mọi ca) → `ExportProductionSheet` | OPS-PLAN; duyệt: mục "Duyệt – Kế hoạch sản xuất" | Lệnh SX `PH-` theo món + mẻ `ME-YYMMDD-TRUA-NN`; giao 11:00 → xuất bếp 10:00 → mẻ 08:30–09:30; đơn → Đang sản xuất | [ ] | |
| 12 | P7 `ProductionBatch/CreateMaterialIssue` (Post=true) từng mẻ → `Start` | OPS-KIT (bếp) / OPS-SCM (kho) | Phiếu xuất `PH-` chỉ lấy lô `NL-` vừa nhập (FEFO), **không** lấy lô hết hạn / KHONG_DAT; thiếu tồn → 400, không ghi gì | [ ] | |
| 13 | **Chốt an toàn – nhiệt độ lõi < 70 °C**: `RecordCoreTemperature` 65 không khắc phục → 400; 65 + khắc phục → lưu; `Finish` → `RecordOutput` → `Release` → 400; đo lại 75,5 → `RecordSampleRetained` → `Release` → `Pack` | OPS-KIT, QA | Dưới 70 °C chặn đạt QC và chặn đóng gói (lần đo gần nhất); đạt → Passed → Packed; lệnh SX / kế hoạch tự Hoàn thành khi mọi mẻ đóng gói | [ ] | |
| 14 | Mẻ 2: lõi 82 °C → đạt **khi chưa lưu mẫu** → đóng gói | OPS-KIT | Đạt kèm cảnh báo "chưa lưu mẫu" (D-42); `GetDetail` có `recordSampleRetained` trong `allowedActions` | [ ] | |
| 15 | P8 `DeliveryRoute/GetReadyOrders` → `Create` | OPS-LOG | Chuyến `CX-YYMMDD-<mã xe>-NN` + đơn giao `PH-` + điểm dừng; xuất phát kế hoạch 10:00 | [ ] | |
| 16 | **Chốt an toàn – nhiệt độ giao < 60 °C**: `RecordTemperature` 55 + khắc phục → `Dispatch` | OPS-LOG | 400 "… 55 °C dưới ngưỡng 60 °C"; lần đo vẫn lưu sổ (TemperatureLog DELIVERY) | [ ] | |
| 17 | **Chốt an toàn – lưu mẫu trước khi giao (D-42)**: `Dispatch` kèm 65 °C khi mẻ 2 chưa lưu mẫu → 400 liệt kê `ME-…-02`; `RecordSampleRetained` mẻ 2 → `Dispatch` | OPS-LOG, OPS-KIT | Sau khi lưu mẫu: Đang giao. Mẻ của ngày / ca chưa đóng gói cũng chặn xuất phát | [ ] | |
| 18 | P8 `DeliveryStop/Arrive` → `Deliver` 58 °C → 400 → `Deliver` 63 °C, giao 1000 / nhận 998 → `ExportDeliveryNote` | OPS-LOG (tài xế) | 58 °C chặn bàn giao; 63 °C → POD, đơn Đã giao, chuyến Hoàn thành; 2 suất từ chối → phản ánh `PA-` tự động; biên bản mở được | [ ] | |
| 19 | P8 Đối soát: `OrderReconciliation/GetPendingOrders` → `Reconcile` (phục vụ 990, trả 5, hủy 3) → `Close` | SAL-CS / FIN-ACC | Chênh −10 (−1%), giá trị 34.650.000 (= 35.000.000 × 990 ÷ 1000); phục vụ > nhận → 400; chênh > 1% đóng phải có ghi chú; đơn → Đã đối soát | [ ] | |
| 20 | P8 Phản ánh: `CustomerComplaint/Create` (High) → `Assign` → `Resolve` (bắt buộc nội dung) → `Close` → `GetDetail` | SAL-CS, QA | Số `PA-YYMMDD-NNNN`, hạn SLA +4 giờ; lịch sử CREATED / ASSIGNED / RESOLVED / CLOSED; Resolve khi Mới → 400 | [ ] | |
| 21 | Phân quyền | Đăng nhập user nhóm không có quyền màn (vd. OPS-LOG gọi `PurchaseOrder/Submit`) | HTTP 403; user không có mục duyệt gọi `ApprovalRequest/Decide` → 400 "không có quyền duyệt" | [ ] | |
| 22 | Danh sách không lọc | `MaterialLot/GetAll`, `Order/GetAll`, `Recipe/GetAll`, `ProductionBatch/GetAll` không truyền Id cha | Trả đủ dòng (trước T50R1 luôn rỗng) | [ ] | Lỗi đã sửa ở T50R1 |

Kết quả chạy thật trên DB dev (2026-11-17): luồng PowerShell 88/88 bước (key P9B), chính file `pilot-flow.http` 117 request (key P9C) – 8 chốt an toàn trả 400 đúng; số chứng từ ở PROGRESS mục T50R1.

### PILOT-FE – thao tác trên giao diện (để sau)
Theo D-40, các kịch bản **trên giao diện** của luồng Pilot (FE-P2 hộp duyệt, FE-P3 thực đơn / đơn hàng, FE-P4 kho, FE-P5 mua hàng – nhận hàng – QC, FE-P7 mẻ sản xuất, FE-P8 giao – đối soát – phản ánh) **viết và thử sau khi làm xong đợt FE** (thứ tự FE-3 → FE của T45R.8 → FE-P2 … FE-P8 → UAT-PILOT trên giao diện). Hiện staging đã có bản build Angular (FE-0, FE-1, FE-2, FE-3 WIP) – chỉ thử được các màn ở bảng PILOT-FE phía trên.

## KÊNH TIN NHẮN – LÕI (BOT-1.1, 2026-11-18) · file thử: `docs/api-tests/bot-1.1-channel.http` · test: `AppDelicare.Tests/Notifications/BotChannel*Tests.cs`
- [ ] Không có / sai `X-Client-Key` → `api/BotChannel/*` trả HTTP 401; từ internet qua nginx → 404.
- [ ] Gửi 2 lần cùng update (bot + update_id) → 1 dòng `InboundMessage`, lần 2 `duplicate = true`.
- [ ] ClaimInbound → Đang xử lý; CompleteInbound Đã xử lý; kết thúc lần 2 → 400; màn Tin nhắn vào: Xử lý lại chỉ với Lỗi / Bỏ qua.
- [ ] Màn Tin nhắn ra: gửi tay cho người đã liên kết → Chờ gửi; người chưa liên kết → báo lỗi; gửi lỗi → tự thử lại sau 1′; nút Gửi lại; người dùng chặn bot → Blocked, định danh Blocked.
- [ ] Màn Kênh: ConfigJson không lộ token / secret / key (hiện `***`); sửa kênh không làm mất bí mật.

## LIÊN KẾT TELEGRAM (BOT-1.2, 2026-11-18) · file thử: `docs/api-tests/bot-1.2-identity.http` · test: `AppDelicare.Tests/Notifications/BotIdentityTests.cs`
- [ ] Nhân viên quét QR → chia sẻ số → khớp đúng 1 hồ sơ → liên kết; số không khớp / trùng nhiều → bot báo liên hệ Nhân sự.
- [ ] Trưởng ca / người có quyền duyệt chỉ chia sẻ số → dùng bot nhưng không duyệt được; lấy mã trên web (hồ sơ cá nhân) → /start <mã> → duyệt được.
- [ ] HR cấp mã cho nhân viên (hạn 72 giờ) → nhân viên /start <mã> → liên kết; dùng lại mã → từ chối; mã quá hạn → từ chối.
- [ ] Khóa tài khoản trên web → bot ngừng cấp token (log TOKEN Failed). Admin gỡ liên kết → bot coi như chưa liên kết.
- [ ] Nhân viên không có tài khoản web: xem lịch ca, ca hôm nay, vào / ra ca (đúng quy tắc ca + bán kính), xem đơn của mình.

## BOT TELEGRAM – CONTAINER MESSAGING (BOT-1.3, 2026-11-18) · thử: `tools/bot-smoke/` (Telegram giả) hoặc bộ bot DEV thật · test: `AppDelicare.Tests/Messaging/MessagingHostTests.cs`
- [ ] Quét QR cổng (`/start CHAMCONG`) khi chưa liên kết → bot hiện nút "📱 Xác nhận số điện thoại".
- [ ] Chia sẻ số → liên kết, bot chào theo tên + 3 nút; người có quyền duyệt được nhắc lấy mã web; người có quyền bot DH nhận nút mở bot Điều hành.
- [ ] "⚡ Ca của tôi" hiện ca hôm nay; `/huylienket` gỡ liên kết.
- [ ] Gửi tay trên màn Tin nhắn ra → tin tới Telegram trong vài giây (tin thường không rung chuông).
- [ ] Production: webhook sai secret → 401; tắt api → Telegram tự gửi lại, không mất tin; `docker compose ps` messaging healthy.

## BOT – MENU, CHẤM CÔNG, DUYỆT (BOT-1.4, 2026-11-18) · thử: `tools/bot-smoke/flow-experience.mjs` · test: `AppDelicare.Tests/Messaging/MessagingExperienceTests.cs`
- [ ] Người ít chức năng (≤ 6) thấy các nút chức năng ngay; người nhiều chức năng / admin chỉ 3 nút, 📂 Chức năng → phân hệ → chức năng (≤ 2 lần bấm, sửa cùng tin, có ⬅️).
- [ ] Danh sách lệnh "/" của mỗi người chỉ gồm lệnh được dùng (người không duyệt không thấy /cho).
- [ ] 📍 Chấm công: gửi vị trí → vào / ra ca; vị trí chuyển tiếp hoặc cũ > 2 phút bị từ chối.
- [ ] ⏳ Chờ tôi duyệt chỉ có việc mình đúng là người duyệt; đơn nghỉ / đổi ca: 1 chạm + Hoàn tác 10″ (hoàn tác → không duyệt); PO / PR / hợp đồng: xác nhận 2 bước; Từ chối bắt buộc lý do.
- [ ] Bấm Duyệt 2 lần chỉ ghi 1 quyết định; người không có quyền / chưa nhập mã web → bot báo rõ.
- [ ] Gõ mã chứng từ (ME-…, CX-…, PO-…) → kết quả tra cứu theo quyền.
