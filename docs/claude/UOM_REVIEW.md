# Rà đơn vị đóng gói chưa ghi kích cỡ (UI-6.22 – D-103)

**Vì sao:** bảng quy đổi `UomConversion` **không gắn nguyên liệu** → "1 thùng = 10 kg" áp cho mọi nguyên liệu mua theo "thùng" (thùng cải 10 kg ≠ thùng cá 20 kg). Từ UI-6.22 mỗi kích cỡ là một đơn vị riêng ("thùng 10 kg", "bao 25 kg", "khay 30 quả"…), tạo ở màn **Đơn vị tính › + Đơn vị đóng gói** (tên tự sinh, quy đổi lưu cùng).

**Cách rà:** `DB/scripts/UI622_uom_pack.sql` (khối 4) liệt kê đơn vị có tên là loại bao bì (thùng / bao / khay / hộp / chai / can / túi / kiện / lốc) **không có số kích cỡ**, kèm số nguyên liệu / giá NCC đang dùng. **Không tự tách** khi chưa có xác nhận của anh / Mua hàng.

## Kết quả DB dev (2026-11-24, chạy thật)

| Hạng mục | Số |
|---|---|
| Đơn vị đóng gói chưa ghi kích cỡ | **0** (DB dev chỉ có đơn vị gốc dữ liệu thử) |
| Đơn vị gốc chuẩn thêm (kg, g, l, ml) | 4 |
| Quy đổi chuẩn thêm (1 kg = 1000 g, 1 l = 1000 ml) | 2 |
| Đơn vị gán loại Gốc (UomType trống → BASE) | 92 |

## Việc khi chạy trên DB thật

1. Chạy `UI622_uom_pack.sql` với `-v DryRun=1` → bảng cuối là danh sách cần xác nhận.
2. Mua hàng ghi kích cỡ thật từng trường hợp (vd "thùng" của cải = 10 kg, của cá = 20 kg) vào bảng dưới.
3. Sau khi anh duyệt: tạo đơn vị có kích cỡ ở màn Đơn vị tính, chuyển `PurchaseUomId` / `IssueUomId` của nguyên liệu và giá NCC sang đơn vị mới (chứng từ cũ giữ nguyên). Chỉ tự đổi tên khi cả hệ thống chỉ có **một** kích cỡ (vd "bao" luôn = 25 kg → "bao 25 kg").

| Đơn vị cũ | Nguyên liệu / giá NCC đang dùng | Kích cỡ thật (Mua hàng ghi) | Đơn vị mới | Đã chuyển |
|---|---|---|---|---|
| (chưa có trên DB dev) | | | | |

## GAP-UOM-01 – PROPOSED (chờ anh duyệt, không làm đợt này)

Quy đổi khác nhau **theo từng nguyên liệu** (vd "1 quả trứng ≈ 60 g" để công thức ghi g, kho ghi quả) cần thêm cột nguyên liệu vào `UomConversion` (đổi cấu trúc). Tạm thời: công thức dùng cùng đơn vị kho của nguyên liệu, hoặc đơn vị gốc có quy đổi chung.
