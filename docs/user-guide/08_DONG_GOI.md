# 08 – Chia suất / đóng gói

## 1. Bạn là ai

- Người dùng: **Tổ Chia suất / Đóng gói** (OPS-KIT-PORT) và điều phối giao nhận khi lập lệnh đóng gói cho chuyến.
- Nhóm quyền: **OPS-KIT** (tổ Chia suất / Đóng gói) được **Thêm + Sửa** trên `log-packing-order`, `log-packing-result` (D-69); OPS-LOG, OPS-POS, SAL-CS như trước.
- Bot: chưa có chức năng đóng gói trên bot (chỉ web).
- Việc chính: lập lệnh đóng gói từ mẻ **Đạt QC**, bắt đầu, ghi kết quả (đạt / hỏng / loại, loại khay).

## 2. Màn hình được dùng

| screen-id | Menu › Mục | Dùng để |
|---|---|---|
| `log-packing-order` | Đóng gói & Giao nhận › Đơn đóng gói | Lập lệnh, Bắt đầu, Ghi kết quả, Hủy |
| `log-packing-result` | … › Kết quả đóng gói | Danh sách kết quả; **Ghi kết quả đóng gói** |
| `prod-batch` | Sản xuất & Giá thành › Mẻ sản xuất | Nút **Tạo lệnh đóng gói** trên mẻ Đạt |

## 3. Việc trong ca

| Thời điểm | Việc |
|---|---|
| Mẻ được QC **Đạt** | Lập lệnh đóng gói (tự chia theo đơn, hoặc lập tay từng đơn) |
| Bắt đầu chia suất | **Bắt đầu** lệnh |
| Xong | **Ghi kết quả** – số đạt + hỏng + loại phải bằng số kế hoạch |
| Trước giờ xe chạy | Mọi mẻ của ngày / ca phải **Đã đóng gói** và đã lưu mẫu, nếu không xe không xuất phát được |

## 4. Thao tác từng bước

### 4.1 Lập lệnh đóng gói
**Cách 1 – tự chia từ mẻ** (`prod-batch`): mở mẻ **Đạt** → **Tạo lệnh đóng gói** → hệ thống chia số phần đạt cho các đơn **Đã chốt / Đang sản xuất** của ngày (ca) → "Đã sinh lệnh đóng gói (PackingOrder) từ mẻ". Phần dư được cảnh báo (*Mẻ … còn … phần chưa chia cho đơn (dư) – ghi hao hụt nấu dư nếu không dùng*).

**Cách 2 – lập tay** (`log-packing-order`):
1. **Lập lệnh đóng gói** → chọn **Mẻ nấu đã đạt kiểm tra*** (hiện số phần đạt), **Đơn hàng*** (đơn Đã chốt / Đang sản xuất), **Số phần đóng*** (> 0), Ngày đóng gói, Ghi chú.
2. **Lập lệnh** → lệnh `DG-…` ở **Kế hoạch**.

### 4.2 Danh sách lệnh – `log-packing-order`
- Lọc **Ngày đóng gói** (ô ngày) hoặc **Tất cả các ngày**.
- Cột: Số lệnh, Mẻ nấu, Món, Đơn hàng (+ khách), Số phần, Ngày đóng gói, Trạng thái.

| Trạng thái | Nút | Kết quả |
|---|---|---|
| Kế hoạch | **Bắt đầu** | → Đang đóng gói |
| Kế hoạch / Đang đóng gói | **Ghi kết quả** | Hộp nhập (4.3) |
| Kế hoạch | **Hủy** | Bắt buộc lý do; số phần trả về mẻ |

### 4.3 Ghi kết quả
1. **Ghi kết quả** trên dòng (hoặc `log-packing-result` → **Ghi kết quả đóng gói** → chọn lệnh → **Tiếp tục**).
2. Nhập **Số phần đóng đạt*** (mặc định = kế hoạch), **Số phần hỏng**, **Số phần bị loại**, **Loại khay / hộp** (gợi ý: KHAY, HOP_GIU_NHIET, THUNG_GIU_NHIET), Ghi chú.
3. **Đạt + hỏng + loại = số kế hoạch** → **Ghi kết quả** → lệnh **Đã đóng gói**; mẻ chuyển **Đã đóng gói** khi mọi lệnh của mẻ xong. Phần hỏng / loại ghi vào hao hụt.

## 5. Trạng thái

| Chứng từ | Luồng |
|---|---|
| Lệnh đóng gói | Kế hoạch → Đang đóng gói → Đã đóng gói; Hủy |
| Mẻ | … Đạt → Đã đóng gói |

## 6. Lỗi / cảnh báo thường gặp

| Thông báo | Cách xử lý |
|---|---|
| Chưa có mẻ nào đạt kiểm tra chất lượng để đóng gói | Chờ QA cho Đạt |
| Chưa có đơn hàng đã chốt / đang sản xuất để đóng gói | Đơn chưa chốt – báo KD |
| Chỉ lập lệnh đóng gói từ mẻ Đạt QC (mẻ … đang …) | Mẻ chưa đạt |
| Tổng số phần các lệnh đóng gói của mẻ … vượt số phần đạt của mẻ | Giảm số phần |
| Mẻ … đã chia hết số phần đạt cho lệnh đóng gói | Không lập thêm |
| Đạt + hỏng + loại phải bằng số kế hoạch (… phần) | Sửa số cho khớp |
| Số lượng không được âm / Nhập số phần đóng lớn hơn 0 | Sửa số |
| Mẻ … chưa được đóng gói: … | Mẻ chưa đủ điều kiện ATTP (nhiệt độ lõi, sản lượng, lỗi trọng yếu) |
| Mẻ …: phải xong công đoạn Chia suất trước khi đóng gói | Mở mẻ → tab **Công đoạn** → **Xong** công đoạn Chia suất (hoặc nút **⏹ Xong Chia suất** trên thẻ mẻ ở bot) |
| Bạn không có quyền … trên màn hình Đơn đóng gói | Thiếu quyền Sửa – C-17 |
