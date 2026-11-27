# REAL-1 – Dữ liệu vận hành thật làm dữ liệu demo / UAT (D-129, D-130)

Dựng **cùng một bộ dữ liệu** trên mọi môi trường (máy dev, máy FE, server test) cho đồng nhất: 19 ĐVT, 28 NCC, 370 nguyên liệu, 575 món,
109 công thức khóa, 482 giá NCC, 20 khách / 20 điểm giao, 11 hợp đồng, 3 thực đơn tuần, 280 đơn xác nhận (2 tuần), dự báo, tồn đầu kỳ 32 mặt hàng;
dữ liệu `[DEMO]` cũ được dọn (hủy / chấm dứt / xóa mềm – không xóa cứng). Mọi bản ghi REAL-1 có Note bắt đầu `[DEMO] REAL-1`.

- Dữ liệu nguồn: `DB/data/real1/` (15 tệp Excel đã chuẩn hóa + `_nguon/normalize.py`). **D-130 (anh duyệt 2026-11-26): đưa vào repo** để clone là dựng được –
  có giá mua NCC / đơn giá hợp đồng thật: **không chia sẻ repo ra ngoài**.
- Script: `tools/real1/*.mjs` gọi API (đúng nghiệp vụ, đủ phê duyệt) + 2 script SQL `DB/scripts/REAL1_retire_demo.sql`, `DB/scripts/REAL1_tag.sql`.
- Mọi bước **chạy lại = 0 việc** (chạy lại an toàn).

## Chạy nhanh (1 lệnh)

Yêu cầu: API đang chạy và đã cập nhật code mới nhất (`main`), Node ≥ 18, `sqlcmd` trên máy chạy lệnh (kết nối được tới SQL Server của môi trường).

```bash
# 1) Chạy thử – chỉ in số dòng sẽ dọn, KHÔNG ghi
DC_USER=admin DC_PASS=<mật khẩu admin> node tools/real1/run-all.mjs
# 2) Chạy thật
DC_USER=admin DC_PASS=<mật khẩu admin> node tools/real1/run-all.mjs --yes
```

PowerShell: `$env:DC_USER='admin'; $env:DC_PASS='<…>'; node tools/real1/run-all.mjs --yes`

| Biến môi trường | Mặc định | Ghi chú |
|---|---|---|
| `DC_USER` / `DC_PASS` | – (bắt buộc) | Tài khoản quản trị web |
| `UAT_PASS` | – | Mật khẩu chung tài khoản `uat.*`; không đặt / không có tài khoản `uat.*` → mọi bước dùng quản trị |
| `REAL1_API` | `http://localhost:5125/api` | Server test: `http://<server>:5125/api` |
| `REAL1_SQL_SERVER` | `(localdb)\MSSQLLocalDB` | Server test: `<host>,<cổng>` |
| `REAL1_SQL_DB` | `AppDelicare` | Tên DB |
| `REAL1_SQL_USER` / `REAL1_SQL_PASS` | – | Đăng nhập SQL (server test, giống `DB_USER` / `DB_PASSWORD` trong `.env`); không đặt → Windows (`-E`) |
| `REAL1_START` | `2026-11-23` | Thứ Hai của tuần thực đơn đầu. Dựng **sau 29/11/2026** → đặt thứ Hai sắp tới (vd. `2026-12-07`): 3 thực đơn + 2 tuần đăng ký suất dời theo (đơn phải trước giờ chốt 20:00 hôm trước) |
| `REAL1_DIR` | `DB/data/real1` | Thư mục dữ liệu khác |
| `REAL1_05_GROSS` | `1` | `0` khi tệp 05 sinh lại bằng `normalize.py` bản đã sửa (đã là lượng tinh) |

## Theo môi trường

**Máy dev / máy FE (LocalDB):** `git pull` → chạy API (`dotnet run --project AppDelicare/AppDelicare.WebApi --launch-profile http`) → lệnh trên.
Định biên ca: thêm khối `BusinessRules:Hr:ShiftMinStaff` (dưới) vào `AppDelicare/AppDelicare.WebApi/appsettings.Development.json` (tệp riêng mỗi máy, .gitignore).

**Server test (Docker – `docker-compose.yml`):** `git pull` → `docker compose up -d --build` (db-sync cập nhật SP; REAL-1 không có migration mới) →
từ máy có Node + `sqlcmd` tới được SQL Server của server test:

```bash
REAL1_API=http://<server>:5125/api REAL1_SQL_SERVER=<host>,<cổng> REAL1_SQL_DB=<DB_NAME> \
REAL1_SQL_USER=<DB_USER> REAL1_SQL_PASS=<DB_PASSWORD> DC_USER=admin DC_PASS=<…> node tools/real1/run-all.mjs --yes
```

Định biên ca đã có trong `appsettings.Staging.json` (môi trường mặc định của Docker = Staging).

Sau mỗi lần chạy `seed-demo` / full-flow trên môi trường đó: chạy lại `run-all.mjs --yes` (chỉ dọn bộ `[DEMO]` mới, REAL-1 không đổi).

## Từng bước (khi cần chạy lẻ)

| Bước | Lệnh | Việc |
|---|---|---|
| 0 | `sqlcmd … -v DryRun=0 -i DB/scripts/REAL1_retire_demo.sql` | Dọn `[DEMO]` cũ: chứng từ ngày ≥ hôm nay hủy / chấm dứt / xóa mềm; danh mục ngừng + xóa mềm |
| 1 | `node tools/real1/01-import.mjs` | ImportExcel 01 ĐVT → … → 08 điểm giao (05 đổi lượng tinh) |
| 1b | lại bước 0 | Ngừng món "Ngân hàng món" cũ trùng tên + nhóm cũ đã rỗng |
| 2 | `node tools/real1/03-recipes.mjs` | Công thức Nháp → gửi → `uat.coo` duyệt (= khóa, giá thành chuẩn) |
| 3 | `node tools/real1/04-setup.mjs` | 3 kho + 4 dây chuyền (2000 suất / mẻ, 60 phút – giả định) |
| 4 | `node tools/real1/05-contracts.mjs` | 11 hợp đồng (`uat.sal-head` → `uat.ceo`) |
| 5 | `node tools/real1/06-menus.mjs` | Thực đơn chung 3 tuần (`uat.ops-plan` → `uat.coo` duyệt → công bố) |
| 6 | `node tools/real1/07-orders.mjs [tuần …]` | Đăng ký suất tuần (xác nhận) + dự báo công bố |
| 7 | `node tools/real1/08-stock.mjs` | Tồn đầu kỳ giả định (`uat.ops-wh` → `uat.coo` duyệt) |
| 8 | `sqlcmd … -v DryRun=0 -i DB/scripts/REAL1_tag.sql` | Gắn nhãn `[DEMO] REAL-1 · ` đầu Note |

## Định biên ca (giả định)

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

- 05 định mức: tệp gốc ghi lượng **trước sơ chế**; hệ thống coi "Định lượng" là lượng **tinh** (BUSINESS_RULES BIZ-1.1) → `01-import` đổi = gộp × (1 − %SC) × (1 − %CB).
  (DB dev 2026-11-26 đã sửa 74 công thức bằng phiên bản V2 – kết quả giống.)
- Kế hoạch / lệnh sản xuất / đề nghị mua của tuần đăng ký suất do hệ thống tự lập sau khi đơn tự chốt 20:00 hôm trước.
- Lỗi dữ liệu nguồn còn chờ anh xác nhận: Xôi thịt kho (MON-0574), 3 món thiếu giá NCC, nhóm món lệch giữa tệp món và thực đơn – xem PROGRESS mục REAL-1.

## Bổ sung: nhân sự + đội giao vận thật (`09-hr-delivery.mjs`, 2026-11-26)

Nguồn trong repo (anh duyệt đưa vào 2026-11-26, như D-130 – có họ tên, SĐT, địa chỉ: **không chia sẻ repo ra ngoài**): `DB/data/real1/HR/DSNS_*.xlsx` (tệp mới nhất) và `DB/data/real1/Delivery/Đội Giao vận.xlsx` (chép từ `Delicare-Share`). Có tệp mới hơn: chép đè vào 2 thư mục này (hoặc đặt `REAL1_SHARE` trỏ tới Delicare-Share) rồi chạy lại.

```bash
DC_USER=admin DC_PASS=<…> node tools/real1/09-hr-delivery.mjs          # chạy thử – in kế hoạch
DC_USER=admin DC_PASS=<…> node tools/real1/09-hr-delivery.mjs --yes    # ghi
# REAL1_SHARE=<thư mục chứa HR/ và Delivery/> nếu không dùng bản trong repo
```

- **Chức vụ** (`Position`, mã `CV-<tên không dấu>`): mỗi chức vụ khác nhau trong tệp nhân sự (53).
- **Nhân viên** (ImportExcel `Employee`, khớp theo mã NV): 139 người; đơn vị theo cột Bộ phận / Tổ → mã đơn vị ORG_STRUCTURE (Tổ chia suất → OPS-KIT-PORT, Giao vận → OPS-LOG, Bếp → OPS-KIT-COOK, Tổ sơ chế → OPS-KIT-PREP, Tổ tạp vụ → OPS-KIT-CLEAN, Kế toán → FIN-ACC, HCNS → FIN-HR, Kho / Thu mua → OPS-SCM, Trợ lý → CEO-PMO, Marketing → SAL-DEV, Kỹ thuật → OPS-MNT, Pháp chế → FIN, Vận hành → OPS; Giám đốc → CEO; Quản lý Bếp → OPS-KIT; Nấu cơm / Chia canh → OPS-KIT-RICE; không ghi tổ → DLC). Người không có mã NV → mã cố định `NV-<tổ>-<họ tên>`.
- **SĐT + địa chỉ** lái xe / phụ xe lấy từ tệp Đội Giao vận (khớp họ tên; SĐT thêm số 0 đầu).
- **Xe** (`Vehicle`, khớp biển số): loại xe, tải trọng (kg), ghi chú `[REAL-1] Hãng · BH TNDS đến · Tuyến · Lái xe`; lái xe mặc định khi cột lái xe chỉ nêu 1 tên gọi khớp duy nhất trong đội lái xe.
- **Chưa lưu** (không có cột): hình thức làm việc (Fulltime / Partime / Khoán / Remote); bảng tuyến theo buổi (sáng / trưa / chiều / tối) – chuyến giao lập theo ngày ở Điều phối giao hàng.
- Không tạo tài khoản đăng nhập (đề nghị cấp tài khoản làm ở Nhân sự › Nhân viên).
- Phần chưa nạp / em tự quyết tạm – chờ anh trao đổi: `docs/claude/GAP_REAL1_HR_DELIVERY.md`.
