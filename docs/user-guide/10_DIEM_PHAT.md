# 10 – Điểm phát / CSKH (nhận hàng, đối soát suất, phản ánh)

## 1. Bạn là ai
> **BIZ-2.9 – Đối soát tự động:** bàn giao xong đơn (Đã giao) → hệ thống **tự tạo đối soát** với suất phục vụ = suất khách nhận (nhân viên điểm sửa nếu khác, ghi suất lỗi Delicare như BIZ-1.4). Đối soát **Khớp**, đơn không còn phản ánh mở, quá **24 giờ** → **tự Chốt** ("Hệ thống tự chốt" – chạy lúc 20:00 và khi mở màn đối soát). Chốt (tay / tự) → **tự tính giá vốn suất**.


- Nhóm quyền: **OPS-POS** (1.5 Vận hành điểm phát – quản lý / nhân viên điểm phát tại công trường / nhà máy) và **SAL-CS** (2.3 Chăm sóc khách hàng).
- Bot dùng: **Vận hành** – phân hệ 🍱 Điểm phát (OPS-POS) và 📣 CSKH (SAL-CS); QR dán tại điểm phát. Trưởng CSKH được cấp thêm bot **Điều hành**.
- Việc chính: theo dõi xe đến, đối chiếu số suất nhận / phục vụ thực tế, ghi phản ánh khách hàng và xử lý trong hạn (SLA).

## 2. Màn hình được dùng

<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->
Quyền theo ma trận D-79 (đủ 19 nhóm: `docs/claude/MENU5_MATRIX.md`).

| Menu › Mục | screen-id | OPS-POS |
|---|---|---|
| Việc của tôi › Việc của tôi | `hr-my` | X |
| Phê duyệt › Lệnh của agent | `wf-inbox?type=ai-agent-cmd` | Duyệt |
| Kinh doanh › Khách hàng | `crm-customer` | X |
| Kinh doanh › Đăng ký suất tuần | `crm-order-week` | X |
| Kinh doanh › Đơn hàng theo ngày | `crm-order` | X |
| Kinh doanh › Đối soát suất ăn | `crm-reconciliation` | S |
| Kinh doanh › Phản ánh khách hàng | `log-complaint` | S |
| Bán lẻ › Đơn khách lẻ | `sal-retail-order` | S (IX) |
| Bán lẻ › Sản phẩm bán lẻ | `sal-retail-product` | X |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | X |
| Đóng gói & Giao hàng › Theo dõi giao hàng | `log-execution` | S |
| Đóng gói & Giao hàng › Khay hộp theo khách | `log-packaging-ledger` | S |
| Đóng gói & Giao hàng › Loại hộp / khay | `log-packaging-type` | X |
| Tri thức › Kho tri thức | `knowledge` | X |
| Tri thức › 80 Đào tạo | `knowledge?folder=80` | X |
| Tri thức › 99 Mẫu | `knowledge?folder=99` | X |
<!-- MENU5:END -->

## 3. Việc trong ngày

| Thời điểm | Việc | Ở đâu |
|---|---|---|
| Trước bữa | Xem **Xe đến hôm nay** (giờ dự kiến, số suất) | bot `/xeden` |
| Xe đến | Tài xế ghi bàn giao (POD); điểm phát kiểm số suất, nhiệt độ, người nhận | `log-execution` / bot tài xế |
| Sau bữa | **Ghi suất phục vụ thực tế** (phục vụ / trả về / hủy) | `crm-reconciliation` / bot `/doisoat` |
| Có vấn đề | **Ghi phản ánh** (thiếu suất, chất lượng, giờ giao…) | `log-complaint` / bot |
| Trong hạn SLA | CSKH nhận xử lý → đã xử lý → đóng | `log-complaint` / bot `/phananh` |
| Cuối ngày | Kế toán / KD **Chốt** đối soát | `crm-reconciliation` |

## 4. Thao tác từng bước

### 4.1 Nhận hàng tại điểm – `log-execution`
- Tìm đơn giao của điểm mình (ngày giao) → khi xe **Đã đến**, tài xế bấm **Bàn giao & ghi POD**; điểm phát đọc **SL giao**, xác nhận **SL khách nhận**, tên **Người nhận**, nhiệt độ khi giao.
- Từ chối bớt suất (SL nhận < SL giao) → hệ thống **tự lập phản ánh thiếu suất** `PA-…`.
- Xem / in: **Sự kiện**, **In phiếu**, **In POD**. Chi tiết nút xem `09_GIAO_VAN.md` mục 4.3.

### 4.2 Đối soát suất – `crm-reconciliation` (UI-6.8, D-86)
So số suất qua từng khâu Đặt → Sản xuất → Đóng gói → Giao → Khách nhận, chốt số tính tiền cho từng đơn. Đơn giao xong tự có đối soát (phục vụ = khách nhận); khớp số thì tự chốt sau 24 giờ. **Không còn hộp thoại** – xác nhận ngay trên bảng.

1. **Kỳ:** **7 ngày / 30 ngày / Tháng** + ◀ ▶; lọc **khách hàng**.
2. **3 bước:**
   - **① Xác nhận số** – nút *Xác nhận N đơn khớp* (nhận = đặt).
   - **② Xử lý chênh lệch** – nút *N đơn có chênh lệch* mở tab tương ứng.
   - **③ Chốt đối soát** – nút *Chốt N đơn đã xác nhận* (ghi chú bắt buộc khi chênh lệch vượt ngưỡng) + **Bảng kê tháng** (mở màn Bảng kê thanh toán).
3. **4 thẻ:** Suất đặt · Suất khách nhận (% so đặt) · Giá trị đã chốt · Chờ / chênh lệch.
4. **Hai biểu đồ cạnh nhau** (màn hẹp xếp dọc):
   - trái **Đặt – Giao – Nhận theo ngày ăn** (cột = đặt, đường = giao, nét đứt = khách nhận; rê chuột xem chênh Nhận − Đặt);
   - phải **chuỗi 5 khâu** cả kỳ + 4 ô **hao hụt giữa từng khâu** (âm tô đỏ).
5. **Bảng xác nhận** – tab **Chờ đối soát · Có chênh lệch · Đã chốt · Tất cả**; lọc ngày ăn, điểm giao.
   - Cột: chọn · Đơn (ngày ăn) · Khách / điểm giao · Đặt · **Khách nhận** (đỏ nếu khác đặt) · **Phục vụ** (mặc định = khách nhận, chữ mờ, không cần nhập) · Trả về · **Lỗi Delicare** (> 0 mới hiện ô **Lý do**: Thiếu suất, Hỏng / đổ vỡ, Không đạt nhiệt độ, Sai món, Chất lượng không đạt) · **Tính tiền** = nhận − lỗi · Thành tiền (giá cố định lúc chốt đơn) · **Nguồn** (Bàn giao / Bot / Web / Tự động).
   - Sửa ô → dòng tô cam, tự được chọn, tiền tự tính; **Enter xuống dòng dưới cùng cột**. Đơn theo lựa chọn: bấm **▸** mở các lựa chọn để ghi phục vụ từng lựa chọn (tổng tự cộng).
   - Nút **Xác nhận tất cả dòng khớp (N)** (nhận = đặt, không sửa) và **Xác nhận dòng đã chọn** (chặn dòng có lỗi chưa chọn lý do). Dòng không lưu được hiện riêng trong hộp kết quả, các dòng khác vẫn lưu.
   - Ràng buộc như cũ: phục vụ + trả về + hủy ≤ số khách nhận; lỗi ≤ số khách nhận.
6. Dòng **Đã chốt**: chỉ xem, nút **In** biên bản đối soát (PDF). Tính tiền (BIZ-1.4, D-77 d): theo số khách ký nhận trừ suất lỗi Delicare; khách không dùng hết không giảm tiền; phục vụ chỉ để dự báo.
7. **Bot điểm phát (D-86 c):** 2 giờ sau khi bạn ký nhận bàn giao (cấu hình `BusinessRules:ReconcilePromptMinutes`), bot Vận hành hỏi "đã phục vụ đủ chưa?" với 2 nút: **✅ Đã phục vụ đủ** (1 chạm → phục vụ = khách nhận, dòng tự xác nhận, nguồn *Bot*) hoặc **✏️ Nhập số khác** (màn số điền sẵn số khách nhận).

**Quyền:** Điểm phát (OPS-POS), Trưởng KD, **Kế toán** (FIN-ACC – chốt phần còn lại) sửa / xác nhận / chốt; NV KD, CSKH chỉ xem.

### 4.3 Phản ánh khách hàng – `log-complaint`
**Tạo phản ánh** (đầu trang):
1. **Khách hàng***, **Đơn hàng liên quan** (hoặc "Không gắn đơn hàng"), **Kênh tiếp nhận** (gợi ý theo dữ liệu cũ), **Phân loại** (vd thiếu suất, chất lượng, giờ giao), **Mức độ** (Cao / Trung bình / Thấp), **Nội dung phản ánh***.
2. **Tạo phản ánh** → số `PA-…`, **hạn xử lý** tính theo mức độ: Cao 4 giờ · Trung bình 24 giờ · Thấp 72 giờ.

**Danh sách:** Số KN, Khách hàng, Phân loại, Mức độ, Tiếp nhận, **Hạn SLA** (đỏ "Quá hạn N giờ" / "Còn N giờ"), Trạng thái. Dòng thông báo *"N phản ánh đã quá hạn xử lý"*.

**Xử lý:** mỗi dòng có nút **→ {trạng thái kế}**; bấm → nhập ý kiến / cách xử lý → xác nhận:

| Từ | Nút | Đến |
|---|---|---|
| Mới | → Đang xử lý | Đang xử lý (bạn nhận xử lý) |
| Đang xử lý | → Đã xử lý | Đã xử lý (ghi cách xử lý) |
| Đã xử lý | → Đóng | Đóng |

Lịch sử thay đổi ở tab **Lịch sử xử lý** của ngăn chi tiết.

### 4.4 Khay hộp
Ghi số khay / hộp khách trả và xem số còn ở khách: `09_GIAO_VAN.md` mục 4.5–4.6.

## 5. Trạng thái

| Chứng từ | Luồng |
|---|---|
| Đối soát | Mở → Khớp / Chênh lệch → Đóng |
| Đơn hàng | … Đã giao → Đã đối soát |
| Phản ánh | Mới → Đang xử lý → Đã xử lý → Đóng; Từ chối |

## 6. Lỗi / cảnh báo thường gặp

| Thông báo | Cách xử lý |
|---|---|
| Không có đơn Đã giao nào đang chờ đối soát | Đơn chưa bàn giao xong – kiểm `log-execution` |
| Nhập số suất phục vụ thực tế (≥ 0) / Suất trả về / hủy không được âm | Sửa số |
| Phục vụ + trả về + hủy không được vượt số suất khách nhận (N) | Giảm số |
| Chênh lệch …% vượt ngưỡng 1% – phải ghi chú khi đóng đối soát | Nhập ghi chú khi **Chốt** |
| Đối soát của đơn … đã đóng | Đã chốt – không sửa được |
| Đơn … chưa có biên bản giao nhận (POD) | Tài xế chưa ghi bàn giao |
| Đối soát của đơn … còn Mở – chốt đối soát rồi mới in biên bản | **Chốt** trước khi **In** |
| Nhập nội dung phản ánh | Điền nội dung |
| Không chuyển … sang … | Bước chuyển không hợp lệ |

## 7. Trên bot Telegram

**🍱 Điểm phát** (OPS-POS):
- 🚚 **Xe đến hôm nay** (`/xeden`) – chuyến tới điểm mình: giờ dự kiến, số suất, trạng thái.
- 🧮 **Đối soát suất** (`/doisoat`) – chọn đơn đã giao → số suất phục vụ thực tế trên **màn số** (điền sẵn số khách đã nhận – chưa có thì số đã giao, −10 / −1 / +1 / +10, ✅ Xác nhận).
- 📣 **Phản ánh tại điểm** – chọn khách → mức độ (🔴 / 🟡 / ⚪) → gõ nội dung (chữ tự do – vẫn gõ tay).
- **QR tại điểm phát** (`?start=LOC_<mã>`): mở thông tin điểm phát.

**📣 CSKH** (SAL-CS):
- 📋 **Phản ánh đang mở** (`/phananh`) – mỗi phản ánh: **🙋 Nhận xử lý**, **✅ Đã xử lý** (gõ cách xử lý), **💬 Ghi chú**.
- ⏱ **Sắp quá hạn** (`/saphan`) – phản ánh còn < 60′ tới hạn.
- 📣 **Ghi phản ánh** (`/ghiphananh`) – khách → mức → nội dung.
- Thông báo: 🟡 phản ánh mới, 🔴 phản ánh sắp / quá hạn SLA, 🔴 báo thiếu suất (leo thang COO 15′, CEO 45′).
