# 08 – Chia suất / đóng gói

## 1. Bạn là ai

- Người dùng: **Tổ Chia suất / Đóng gói** (OPS-KIT-PORT) và điều phối giao nhận khi lập lệnh đóng gói cho chuyến.
- Nhóm quyền: **OPS-KIT** (tổ Chia suất / Đóng gói) được **Thêm + Sửa** trên `log-packing-order`, `log-packing-result` (D-69); OPS-LOG, OPS-POS, SAL-CS như trước.
- Bot: chưa có chức năng đóng gói trên bot (chỉ web).
- Việc chính: lập lệnh đóng gói từ mẻ **Đạt QC**, bắt đầu, ghi kết quả (đạt / hỏng / loại, loại khay).

## 2. Màn hình được dùng

<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->
Quyền theo ma trận D-79 (đủ 19 nhóm: `docs/claude/MENU5_MATRIX.md`).

| Menu › Mục | screen-id | OPS-KIT | OPS-LOG |
|---|---|---|---|
| Việc của tôi › Việc của tôi | `hr-my` | X | X |
| Phê duyệt › Lệnh của agent | `wf-inbox?type=ai-agent-cmd` | Duyệt | Duyệt |
| Phê duyệt › Duyệt SOP | `wf-inbox?type=kb-sop` | Duyệt | Duyệt |
| Điều hành › Giao việc | `ai-tasks` | S | S |
| Kinh doanh › Khách hàng | `crm-customer` | — | X |
| Kinh doanh › Đơn hàng theo ngày | `crm-order` | — | X |
| Bán lẻ › Đơn khách lẻ | `sal-retail-order` | X (IX) | Sửa (IX) |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | X | — |
| Thực đơn & Món ăn › Công thức chế biến | `menu-recipe` | X | — |
| Thực đơn & Món ăn › Món ăn | `menu-dish` | X | — |
| Kho › Xuất kho sản xuất | `scm-issue` | X | — |
| Kho › Tồn kho | `scm-inv-balance` | X | — |
| Sản xuất › Kế hoạch sản xuất | `prod-plan` | X | X |
| Sản xuất › Mẻ nấu | `prod-batch` | S | — |
| Sản xuất › Hao hụt | `prod-waste` | S | — |
| Chất lượng & ATTP › Kiểm tra theo ca / mẻ | `qc-checklist-result` | S | — |
| Chất lượng & ATTP › Nhật ký nhiệt độ | `qc-temperature` | S | X |
| Chất lượng & ATTP › Vệ sinh khay hộp | `qc-cleaning` | S | — |
| Chất lượng & ATTP › Bộ kiểm tra ATTP | `qc-checklist` | X | — |
| Đóng gói & Giao hàng › Chia suất & đóng gói | `log-packing-order` | S | X |
| Đóng gói & Giao hàng › Điều phối giao hàng | `log-route` | — | S |
| Đóng gói & Giao hàng › Theo dõi giao hàng | `log-execution` | — | S |
| Đóng gói & Giao hàng › Khay hộp theo khách | `log-packaging-ledger` | — | S |
| Đóng gói & Giao hàng › Loại hộp / khay | `log-packaging-type` | X | S |
| Đóng gói & Giao hàng › Định mức bao bì | `log-packaging-norm` | X | X |
| Nhân sự › Phân ca | `hr-shift-assign` | X | — |
| Nhân sự › Chấm công | `hr-attendance` | X | — |
| Nhân sự › Nghỉ & đổi ca | `hr-leave` | X | — |
| Danh mục hệ thống › Ca làm việc | `hr-shift` | X | — |
| Danh mục hệ thống › Dây chuyền & trạm bếp | `prod-line` | X | — |
| Danh mục hệ thống › Xe giao hàng | `log-vehicle` | — | Q |
| Tri thức › Kho tri thức | `knowledge` | X | X |
| Tri thức › 20 Kinh doanh & Hợp đồng | `knowledge?folder=20` | — | X |
| Tri thức › 30 Thực đơn & Công thức | `knowledge?folder=30` | S | — |
| Tri thức › 45 ATTP | `knowledge?folder=45` | X | X |
| Tri thức › 50 Sản xuất | `knowledge?folder=50` | S | — |
| Tri thức › 55 Giao vận | `knowledge?folder=55` | — | S |
| Tri thức › 80 Đào tạo | `knowledge?folder=80` | X | X |
| Tri thức › 99 Mẫu | `knowledge?folder=99` | X | X |
<!-- MENU5:END -->

## 3. Việc trong ca

| Thời điểm | Việc |
|---|---|
| Mẻ được QC **Đạt** | Lập lệnh đóng gói (tự chia theo đơn, hoặc lập tay từng đơn) |
| Bắt đầu chia suất | **Bắt đầu** lệnh |
| Xong | **Ghi kết quả** – số đạt + hỏng + loại phải bằng số kế hoạch |
| Trước giờ xe chạy | Mọi mẻ của ngày / ca phải **Đã đóng gói** và đã lưu mẫu, nếu không xe không xuất phát được |

## 4. Thao tác từng bước

### 4.1 Chia suất & đóng gói – `log-packing-order` (UI-6.34)
**Luật:** một thực đơn nhiều món, mỗi món nhiều mẻ – **chỉ giao khi điểm giao đã đủ toàn bộ thực đơn** (không giao theo món / mẻ xong trước). Lệnh đóng gói vẫn **tự sinh theo mẻ** khi mẻ đạt (dữ liệu nền); không lập tay.
1. Chọn **ngày** (◀ ▶) và **ca**. Dải 4 bước + 4 thẻ: Món sẵn sàng chia · Điểm giao đủ thực đơn x/y · Phần đã chia % · Hỏng / thiếu khi chia.
2. **Ma trận:** dòng = điểm giao (nhóm theo chuyến, cuối "Chưa gán chuyến"), cột = món của ca (đầu cột "x/y mẻ đạt").
   - Ô **chờ** (xám): món chưa đủ mẻ đạt – khóa. Mẻ nấu thêm chưa đạt → món quay lại chờ cho điểm còn thiếu phần.
   - Ô **sẵn sàng** (viền xanh): bấm → hộp nhỏ, mặc định đủ số; chỉ gõ khi **hỏng / thiếu** (số + lý do).
   - Ô **đã chia** (xanh nhạt) · **hỏng / thiếu** (đỏ, "thiếu N") · **không áp dụng** (món không thuộc lựa chọn của điểm).
3. **Xong món này** (đầu cột, khi món đủ mẻ) – ghi đủ số cho mọi điểm giao của món. **Xong điểm giao** (cột cuối) – ghi mọi món sẵn sàng của điểm. Đủ mọi món → nhãn **Đủ thực đơn**.
- **Tra cứu lệnh theo mẻ** (nút đầu màn): danh sách lệnh cũ để tra cứu.
- Lỗi: *Món … chưa đủ mẻ đạt – chưa chia được*; lập tay: *Lệnh đóng gói do hệ thống tự sinh khi mẻ đạt.*
- Mục "Kết quả đóng gói" đã ẩn (link cũ mở màn này).

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

## 7. Định mức bao bì & tự xuất khi đóng gói (ACC-6, D-131 j) – `log-packaging-norm`

- Màn **Đóng gói & Giao hàng › Định mức bao bì**: ma trận vật tư bao bì (hộp, đũa thìa, cốc, găng tay… mã CC…) × loại suất (Sáng · Trưa · Tối · Đêm · Cơm suất), ô = **số dùng cho 100 suất**.
- **Kế hoạch** (OPS-PLAN) sửa trong ô → **Lưu bản nháp** → **Gửi duyệt** ở đầu cột (duyệt như công thức món ở Phê duyệt › Công thức). Duyệt xong mới áp dụng; dòng nhỏ "đang áp dụng: …" cho thấy số cũ. Đang chờ duyệt thì không sửa được.
- Bếp **ghi kết quả đóng gói** (màn chia suất & đóng gói) → hệ thống lập phiếu xuất bao bì Nháp theo ca cho số suất đạt (mỗi đơn tính 1 lần theo món đóng nhiều suất nhất). Ghi thêm trong ca → chỉ lập phiếu bổ sung phần suất mới. Thông báo sau khi ghi cho biết số phiếu đã lập hoặc "Chưa có định mức bao bì đã duyệt…".
- Thủ kho sửa / ghi sổ phiếu ở Kho; không ghi thì tự ghi sổ khi hết ca (23:30). Kế toán thấy chứng từ **Xuất bao bì** theo buổi.
- Thiếu vật tư bao bì → tin bot Mua hàng có nút **🛒 Tạo đề nghị mua** (lập đề nghị mua nháp phần thiếu của ca, bấm lại không lập trùng).
- Món bao bì (BB-…) không hiện ở danh mục món / chọn món thực đơn – chỉ sửa ở màn Định mức bao bì.
- Cơm suất (bán lẻ) chưa tự lập phiếu (ca không cho biết cơm suất) – kho lập tay.

## Loại hộp / khay và phí thất thoát (PKG-FEE, 28/11/2026)

- **Đóng gói & Giao hàng › Loại hộp / khay**: danh mục loại hộp / khay (Khay inox, Hộp giữ nhiệt, Thùng giữ nhiệt…) dùng khi giao, thu hồi, ghi thất thoát ở Sổ hộp khay.
- Mỗi loại có **phí thất thoát / cái** (đ). Bảng kê thanh toán tính phí cho phần hộp thất thoát **vượt ngưỡng** của khách (số giao × ngưỡng %) × phí của loại. Loại "chưa đặt" phí → bảng kê không tính tiền.
- Ai làm gì: **Giao vận** thêm / sửa / ngừng dùng loại; **Kế toán** sửa phí (không thêm / xóa); điểm phát, bếp, kinh doanh chỉ xem.
- Loại đã có giao dịch ở Sổ hộp khay: **không đổi mã, không xóa** – bấm **Ngừng dùng** (không chọn được cho giao dịch mới, số liệu cũ vẫn đúng tên và phí).
