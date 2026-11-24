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
| Kinh doanh › Khách hàng | `crm-customer` | X |
| Kinh doanh › Đăng ký suất tuần | `crm-order-week` | X |
| Kinh doanh › Đơn đặt suất | `crm-order` | X |
| Kinh doanh › Đối soát suất ăn | `crm-reconciliation` | S |
| Kinh doanh › Phản ánh khách hàng | `log-complaint` | S |
| Kinh doanh › Điểm giao hàng | `crm-location` | X |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | X |
| Đóng gói & Giao hàng › Theo dõi chuyến giao | `log-execution` | X |
| Đóng gói & Giao hàng › Xác nhận đã giao | `log-pod` | S |
| Đóng gói & Giao hàng › Thu hồi khay hộp | `log-container-return` | S |
| Đóng gói & Giao hàng › Khay hộp theo khách | `log-packaging-ledger` | X |
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

### 4.2 Đối soát suất – `crm-reconciliation`
1. Đầu màn: biểu đồ **Đặt – Giao – Nhận** theo ngày ăn. Bảng: Đơn hàng, Đặt, Sản xuất, Đóng gói, Giao, Nhận, **Chênh lệch %** (đỏ khi vượt 1%), Trạng thái.
2. **Ghi suất phục vụ** (đầu trang) → danh sách đơn **Đã giao** chờ đối soát (Số đơn, Ngày ăn, Khách hàng, Đặt, Giao, Khách nhận, Đối soát) → bấm **Ghi suất phục vụ** trên đơn.
3. Hộp **Ghi suất thực tế phục vụ**: hiển thị Suất đặt / Suất giao / Khách nhận khi bàn giao; nhập **Suất phục vụ thực tế*** (mặc định = số khách nhận), **Suất trả về**, **Suất hủy**, **Ghi chú** → **Lưu đối soát**.
   - Ràng buộc: phục vụ + trả về + hủy **≤ số suất khách nhận**.
   - **Tính tiền (BIZ-1.4, D-77 d):** theo **số khách ký nhận khi bàn giao trừ suất trả lại / hủy do lỗi Delicare**. Nhập ô **Suất trả lại / hủy do lỗi Delicare** + chọn **Lý do** (Thiếu suất, Hỏng / đổ vỡ, Không đạt nhiệt độ, Sai món, Chất lượng không đạt). Khách không dùng hết **không giảm tiền**. Hộp xanh hiện: Khách nhận − lỗi Delicare = tính tiền · phục vụ (tham khảo). Suất phục vụ thực tế chỉ để tham khảo và làm dự báo.
4. Trên dòng: **Ghi lại** (sửa số khi chưa chốt), **Chốt** (nhập ghi chú; chênh lệch > 1% bắt buộc giải trình) → đơn hàng **Đã đối soát**, không sửa được nữa; **In** (biên bản đối soát PDF – chỉ in được sau khi chốt).

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
