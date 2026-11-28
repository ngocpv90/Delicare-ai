---
screens: [acc-voucher, crm-billing, acc-amis-code, acc-amis-recon, acc-period]
roles: []
updated: 2026-11-28
---
# 14 – Kế toán (kết nối MISA AMIS)

Viết theo code ACC-1 … ACC-8 (D-131, 26/11/2026). Delicare tự lập chứng từ từ nghiệp vụ đã xong; Kế toán chỉ xử lý dòng lỗi, gửi sang AMIS (hiện bằng file Excel mẫu AMIS – gửi qua API làm ở ACC-7), rồi khóa kỳ. Hạch toán, hóa đơn, công nợ, giá vốn chính thức làm trên **AMIS**.

## 1. Bạn là ai

- Nhóm quyền **FIN-ACC – Kế toán**: Thêm / Sửa / Xóa / Nhập + In / Xuất cả 5 mục nhóm **Kế toán**; xem (In / Xuất) Trả hàng NCC, Định mức bao bì.
- **CEO / COO**: xem (In / Xuất) nhóm Kế toán; **COO duyệt** mở lại đối soát đã chốt và mở khóa kỳ.
- **SAL-HEAD – Trưởng KD**: Bảng kê thanh toán (gửi khách xác nhận).
- Bot **Điều hành**: 07:30 tóm tắt chứng từ của tháng; 08:00 từ ngày 5 nhắc khóa kỳ tháng trước.

## 2. Màn hình được dùng

Mỗi màn có thanh chuyển nhanh 5 mục ở đầu trang.

| Menu › Mục | screen-id | Dùng để |
|---|---|---|
| Kế toán › Chứng từ kế toán | `acc-voucher` | Xem chứng từ tự lập theo tháng, xử lý lỗi, tải Excel mẫu AMIS, đánh dấu đã lập trên AMIS |
| Kế toán › Bảng kê thanh toán | `crm-billing` | Bảng kê tháng từng khách → khách xác nhận → phiếu bán tháng sẵn sàng |
| Kế toán › Khớp mã AMIS | `acc-amis-code` | Nhận danh mục AMIS (Excel), ghép mã Delicare theo mã AMIS, xuất mã cần tạo trên AMIS |
| Kế toán › Đối chiếu với AMIS | `acc-amis-recon` | So 8 chỉ tiêu Delicare ↔ AMIS của kỳ |
| Kế toán › Khóa kỳ | `acc-period` | 5 điều kiện, khóa kỳ, xin mở khóa (COO duyệt), chứng từ điều chỉnh |
| Phê duyệt › Mở lại đối soát | `wf-inbox?type=crm-recon-reopen` | (COO) duyệt mở lại đối soát đã chốt |
| Phê duyệt › Mở khóa kỳ | `wf-inbox?type=acc-period-unlock` | (COO) duyệt mở khóa kỳ |

## 3. Việc trong tháng

| Khi nào | Việc | Màn |
|---|---|---|
| Đầu kỳ / khi AMIS đổi danh mục | Nhập danh mục AMIS, ghép mã, tạo mã còn thiếu trên AMIS | Khớp mã AMIS |
| Hằng ngày (07:30 bot) | Xử lý chứng từ Lỗi, tải Excel mẫu các chứng từ Sẵn sàng → nhập vào AMIS → Đánh dấu đã lập | Chứng từ kế toán |
| Ngày 1 – 5 tháng sau | Gửi khách xác nhận bảng kê, ghi nhận khách xác nhận / phản hồi lệch | Bảng kê thanh toán |
| Trước khi khóa | Nhập số AMIS (Excel mẫu / nhập tay), xử lý dòng lệch | Đối chiếu với AMIS |
| Hạn ngày 10 tháng sau | Khóa kỳ khi đủ 5 điều kiện | Khóa kỳ |

## 4. Thao tác từng bước

### 4.1 Khớp mã AMIS
1. Trên AMIS: Danh mục › (Vật tư hàng hóa / Đơn vị tính / Khách hàng / Nhà cung cấp / Kho) › Xuất khẩu ra Excel.
2. Bấm **Nhập danh mục (Excel)** → chọn loại (hoặc để tự nhận) → chọn tệp → **Nhập**. Mỗi lần nhập thay bản cũ của loại đó.
3. Lọc **Cần xử lý**: *Chưa có trên AMIS* (tạo trên AMIS – nút **Xuất mã cần tạo**), *Lệch tên / đơn vị*, hoặc **Ghép mã** (đổi mã Delicare theo mã AMIS – nguyên liệu, khách, NCC).

### 4.2 Chứng từ kế toán
1. Chọn tháng ◀ ▶. Tab: Cần xử lý · Bán hàng · Mua hàng (nhập mua, **trả lại hàng mua**) · Kho & sản xuất (xuất NVL, **xuất bao bì**, nhập / xuất thành phẩm, kho khác) · Bảng công · Tất cả.
2. Dòng **Lỗi / Chờ nghiệp vụ gốc** có nút đi thẳng tới chỗ sửa (Ghép mã, Đối soát, Bảng kê, Phiếu nhận…).
3. Tích chứng từ **Sẵn sàng** → **Tải file Excel mẫu AMIS** → nhập vào AMIS (Thêm bằng Excel) → quay lại tích → **Đánh dấu đã lập trên AMIS**.
4. Nghiệp vụ gốc sửa / hủy sau khi gửi → dòng **Đã sửa – cần điều chỉnh**.

### 4.3 Bảng kê thanh toán (bước 4 – khách xác nhận)
1. Khách có đơn đã đối soát hết → **Gửi khách xác nhận** (email: chưa được nâng cấp – gửi bảng kê qua kênh thường).
2. Khách đồng ý → tích khách → **Ghi nhận khách xác nhận**; khách báo lệch → **Phản hồi lệch** (ghi chú bắt buộc) → sửa đối soát (đối soát đã chốt: **Mở lại** – COO duyệt).
3. Khách đã xác nhận → phiếu bán tháng của khách thành **Sẵn sàng** ở Chứng từ kế toán.

### 4.4 Đối chiếu với AMIS
1. Chọn tháng. Bấm **Nhập sổ AMIS (Excel)** → **Tải Excel mẫu** (8 dòng chỉ tiêu) → điền cột **Số AMIS** từ sổ / báo cáo AMIS → **Nhập tệp**; hoặc gõ thẳng số vào bảng → **Lưu số AMIS**.
2. Đọc cột **Kết quả**: *Khớp* · *Lệch – còn chứng từ* (bấm nút → chứng từ chưa lập / lỗi) · *Lệch do giá xuất* (AMIS tính bình quân, Delicare theo giá lô – chỉ để biết, giá vốn chính thức lấy AMIS) · *Lệch* (kiểm tra chứng từ sửa tay trên AMIS) · *Chờ kiểm kê* (tồn cuối kỳ – ghi sổ kiểm kê cuối tháng trước).
3. Nút **Lấy lại** (API AMIS) báo "chưa được nâng cấp" tới khi làm ACC-7.

### 4.5 Khóa kỳ
1. Chọn tháng (mặc định tháng trước). 5 điều kiện: đối soát chốt hết · bảng kê khách xác nhận · kiểm kê cuối tháng ghi sổ (mọi kho) · mọi chứng từ đã lập trên AMIS · không còn chứng từ lỗi / cần điều chỉnh. Mỗi điều kiện chưa đạt có nút **Xem / Xử lý**.
2. Kỳ đã hết + đủ điều kiện → **Khóa kỳ** (ghi chú tùy chọn).
3. Cần sửa kỳ đã khóa → **Xin mở khóa** (lý do bắt buộc) → COO duyệt; hoặc để hệ thống lập **chứng từ điều chỉnh ĐC-…** vào kỳ đang mở.

## 5. Trạng thái

| Đối tượng | Trạng thái |
|---|---|
| Chứng từ kế toán | Sẵn sàng · Chờ nghiệp vụ gốc · Chờ lập trên AMIS · Đã lập trên AMIS · Lỗi · Đã sửa – cần điều chỉnh |
| Khách xác nhận bảng kê | Chưa gửi · Đã gửi · Đã xác nhận · Phản hồi lệch · Đã xuất hóa đơn |
| Kỳ kế toán | Đang mở · Đã khóa (· Chờ duyệt mở khóa) |
| Đối chiếu AMIS | Khớp · Lệch – còn chứng từ · Lệch do giá xuất · Lệch · Chưa có số AMIS · Chờ kiểm kê |

## 6. Lỗi / cảnh báo thường gặp

| Thông báo | Cách xử lý |
|---|---|
| Chưa nhận danh mục AMIS – nhập ở Khớp mã AMIS | Làm 4.1 trước |
| Chưa có trên AMIS: … | Tạo mã trên AMIS hoặc Ghép mã, nhập lại danh mục |
| Kỳ kế toán tháng MM/yyyy đã khóa – không … | Xin mở khóa kỳ (COO duyệt) hoặc lập chứng từ điều chỉnh ở kỳ đang mở |
| Tệp không đúng mẫu – cần cột «Mã chỉ tiêu» và «Số AMIS» | Tải lại Excel mẫu ở Đối chiếu với AMIS |
| Tính năng chưa được nâng cấp – … ACC-7 | Dùng file Excel cho tới khi có kết nối API AMIS |

## 7. Liên quan màn khác

- **Kho › Trả hàng NCC** (`05_KHO.md`): phiếu trả đã ghi sổ → chứng từ **Trả lại hàng mua** (đối tượng = NCC).
- **Đóng gói & Giao hàng › Định mức bao bì** (`08_DONG_GOI.md`): phiếu xuất bao bì theo ca → chứng từ **Xuất bao bì** theo buổi.
