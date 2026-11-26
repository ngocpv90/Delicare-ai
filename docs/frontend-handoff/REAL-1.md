# REAL-1 – Dữ liệu vận hành thật làm dữ liệu demo / UAT (D-129, D-130)

Từ 2026-11-26, DB dev dùng **dữ liệu vận hành thật đã chuẩn hóa** thay bộ `[DEMO]` cũ. Máy FE và server test cần dựng cùng bộ này để màn hình, số liệu và ảnh chụp
khớp nhau. Hướng dẫn đầy đủ: [`tools/real1/README.md`](../../tools/real1/README.md).

## Việc cần làm sau khi `git pull`

1. Build + chạy API như thường lệ (`dotnet run --project AppDelicare/AppDelicare.WebApi --launch-profile http`).
2. Chạy thử, xem số dòng sẽ dọn:
   `DC_USER=admin DC_PASS=<mật khẩu admin> node tools/real1/run-all.mjs`
3. Chạy thật: thêm `--yes`. Chạy lại bao nhiêu lần cũng được (lần sau = 0 việc).
4. Thêm định biên ca giả định vào `appsettings.Development.json` (khối JSON trong README) – không bắt buộc cho các màn khác.

Server test (Docker): `docker compose up -d --build` rồi chạy lệnh trên với `REAL1_API`, `REAL1_SQL_SERVER`, `REAL1_SQL_DB`, `REAL1_SQL_USER`, `REAL1_SQL_PASS`
(xem README). Định biên ca đã có trong `appsettings.Staging.json`.

## Dữ liệu sẽ thấy

| Màn | Nội dung |
|---|---|
| Kinh doanh › Khách hàng / Hợp đồng | 20 khách mã kế toán `KH000xx` (11 đang hoạt động), 11 hợp đồng Hiệu lực |
| Kinh doanh › Đăng ký suất tuần | 2 tuần (mặc định 30/11, 07/12/2026), 20 điểm giao, ≈ 13.400 suất / ngày, Sáng 3 lựa chọn |
| Thực đơn & Món ăn › Thực đơn tuần | 3 tuần Đã công bố; bữa trưa nhiều món cùng nhóm → dòng **"Món chính 2 / 3"** (`row.slot`) |
| Thực đơn & Món ăn › Công thức / Món ăn | 575 món `MON-xxxx`, 109 món có công thức khóa (466 món chưa có công thức – đúng dữ liệu thật) |
| Mua hàng › Bảng giá NCC / Nguyên liệu | 28 NCC `NCC-…`, 370 nguyên liệu mã kế toán, 270 nguyên liệu có giá |
| Kho › Tồn kho | 3 kho (Kho khô / mát / đông), 32 mặt hàng tồn đầu kỳ giả định |
| Sản xuất › Kế hoạch | Đơn "chưa chốt" 7 ngày tới; kế hoạch / lệnh / mẻ tự lập sau 20:00 hôm trước |

Mọi bản ghi REAL-1 có Note bắt đầu `[DEMO] REAL-1`. Tài khoản `uat.*` có trên DB dev; máy khác không có thì đăng nhập `admin`.

## Thay đổi API / giao diện đi kèm (đã có trong `main`)

- `Menu/GetWeek`: `listRow[].slot` (0 = dòng đầu của nhóm); `Menu/SaveCell`, `Menu/FillRow` nhận `slot` (mặc định 0). Ô trống ở dòng `slot ≥ 1` không có cờ `EMPTY`.
- `InventoryBalance/GetSummary`: không trả dòng của kho / nguyên liệu đã xóa mềm.
- Màn Công thức: lớp `.rcp-layout` – ≤ 768px xếp 1 cột.

Lưu ý: `DB/data/real1/` chứa giá mua / đơn giá hợp đồng thật – **không chia sẻ repo, tệp hay ảnh chụp bảng giá ra ngoài**.
