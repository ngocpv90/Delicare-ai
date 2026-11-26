# REAL-1 – Nạp dữ liệu vận hành thật làm dữ liệu demo / UAT (D-129)

Dựng lại trên một DB (dev / UAT) bộ dữ liệu REAL-1 đã nạp ngày 2026-11-26. **Dữ liệu nguồn KHÔNG nằm trong repo** (có giá mua, doanh thu thật): thư mục
`Chuan-hoa-REAL1` (sinh bằng `_nguon/normalize.py` từ Delicare-Share). Các script ở đây chỉ đọc thư mục đó và gọi API – không chèn SQL trực tiếp
(trừ 2 script dọn / gắn nhãn trong `DB/scripts`). Mọi bước **chạy lại = 0 việc**.

## Chuẩn bị

- API chạy (`dotnet run --project AppDelicare/AppDelicare.WebApi --launch-profile http` → `http://localhost:5125`), DB đã có seed T23 (tài khoản `system`, `uat.*`).
- Biến môi trường: `DC_USER` / `DC_PASS` (quản trị), `UAT_PASS` (mật khẩu chung tài khoản `uat.*`), `REAL1_DIR` (thư mục Chuan-hoa-REAL1),
  tùy chọn `REAL1_API`, `REAL1_05_GROSS=0` khi tệp 05 đã sinh lại bằng `normalize.py` bản sửa (đã là lượng tinh).
- Node ≥ 18 (fetch / FormData sẵn có), `sqlcmd`.

## Thứ tự

| Bước | Lệnh | Việc |
|---|---|---|
| 0 | `sqlcmd -S <server> -d <db> -E -b -f 65001 -v DryRun=1 -i DB/scripts/REAL1_retire_demo.sql -s "\|" -W` rồi `DryRun=0` | Dọn dữ liệu `[DEMO]` cũ (hủy / chấm dứt / xóa mềm – không xóa cứng). |
| 1 | `node tools/real1/01-import.mjs` | ImportExcel 01 ĐVT → 02 NCC → 03 NL → 04 món → 05 định mức (đổi lượng tinh) → 06 giá NCC → 07 khách → 08 điểm giao. |
| 1b | chạy lại script bước 0 (`DryRun=0`) | Ngừng món "Ngân hàng món" cũ trùng tên món REAL-1 + nhóm món cũ đã rỗng. |
| 2 | `node tools/real1/03-recipes.mjs` | Công thức Nháp → gửi (admin) → `uat.coo` duyệt (= khóa, tính giá thành chuẩn). |
| 3 | `node tools/real1/04-setup.mjs` | 3 kho thật + 4 dây chuyền (2000 suất / mẻ, 60 phút – giả định) gán 8 nhóm món. |
| 4 | `node tools/real1/05-contracts.mjs` | 11 hợp đồng (`uat.sal-head` lập → `uat.ceo` duyệt). |
| 5 | `node tools/real1/06-menus.mjs` | Thực đơn chung 3 tuần (`uat.ops-plan` lập + nhập Excel → `uat.coo` duyệt → công bố). |
| 6 | `node tools/real1/07-orders.mjs [tuần …]` | Đăng ký suất tuần (xác nhận) + dự báo công bố – mặc định tuần 30/11 và 07/12/2026. |
| 7 | `node tools/real1/08-stock.mjs` | Tồn đầu kỳ giả định (`uat.ops-wh` điều chỉnh → `uat.coo` duyệt). |
| 8 | `sqlcmd … -v DryRun=0 -i DB/scripts/REAL1_tag.sql` | Gắn nhãn `[DEMO] REAL-1 · ` vào đầu Note. |

Sau mỗi lần chạy `seed-demo` / full-flow: chạy lại bước 0 (chỉ dọn bộ `[DEMO]` mới, không đụng REAL-1).

## Định biên ca (giả định, máy dev)

`AppDelicare/AppDelicare.WebApi/appsettings.Development.json` (đã .gitignore):

```json
{ "BusinessRules": { "Hr": { "ShiftMinStaff": {
  "OPS-KIT-PREP":  { "SANG_6H": 4, "TRUA": 4, "TOI": 3, "DEM_23H": 1 },
  "OPS-KIT-COOK":  { "SANG_6H": 4, "TRUA": 6, "TOI": 4, "DEM_23H": 2 },
  "OPS-KIT-RICE":  { "SANG_6H": 2, "TRUA": 3, "TOI": 2, "DEM_23H": 1 },
  "OPS-KIT-PORT":  { "SANG_6H": 5, "TRUA": 8, "TOI": 5, "DEM_23H": 2 },
  "OPS-KIT-CLEAN": { "SANG_6H": 2, "TRUA": 2, "TOI": 2, "DEM_23H": 1 },
  "OPS-KIT-QA":    { "SANG_6H": 1, "TRUA": 1, "TOI": 1, "DEM_23H": 1 } } } } }
```

## Ghi chú

- 05 định mức: tệp sinh bởi `normalize.py` bản cũ ghi lượng **trước sơ chế**; hệ thống coi "Định lượng" là lượng **tinh** (BUSINESS_RULES BIZ-1.1) nên
  `01-import` đổi = gộp × (1 − %SC) × (1 − %CB). Trên DB dev 2026-11-26, 74 công thức đã nạp bản gộp được sửa bằng phiên bản V2 (Recipe/CreateVersion).
- Kết quả / số dòng / quyết định tự chọn: `docs/claude/PROGRESS.md` mục REAL-1.
