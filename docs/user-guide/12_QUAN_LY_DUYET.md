# 12 – Quản lý duyệt (Hộp phê duyệt)

## 1. Bạn là ai

- Người duyệt theo seed quyền hiện tại: **CEO**, **COO**, **FIN-HR** (đơn nghỉ / đổi ca), **OPS-SCM** (đề nghị mua cấp 1), **FIN-ACC** (kiểm kê cấp 2), **QA** (phiếu QC đầu vào – duyệt trực tiếp trên màn QC).
- Quyền duyệt = quyền **Truy cập + Sửa** trên mục menu **"Phê duyệt › …"** (`#/wf-inbox?type=<màn>`) do quản trị cấp (`01_QUAN_TRI.md`).
- Bot: duyệt trên bot **Điều hành** / **Vận hành** (⏳ Chờ tôi duyệt) – chỉ khi tài khoản Telegram đã **liên kết tài khoản web** (xem 7 và `90_CHENH_LECH.md` C-02).

## 2. Màn hình được dùng

| screen-id | Menu › Mục | Dùng để |
|---|---|---|
| `wf-inbox` | Nhân sự › Hộp phê duyệt | Duyệt / từ chối / hủy mọi loại yêu cầu |
| `wf-step` | (màn con, ẩn) | Các cấp duyệt của yêu cầu – xem trong tab Tiến trình duyệt |
| `prod-plan` | Sản xuất › Kế hoạch sản xuất | Nút **Duyệt** kế hoạch (cần quyền Phê duyệt › CEO / COO) |
| `menu-menu` | Thực đơn › Thực đơn | Nút **Duyệt** thực đơn (CEO / COO) |
| `menu-recipe` | Thực đơn › Công thức | **Khóa & áp dụng** phiên bản (CEO / COO) |
| `qc-inspection` | QC › Phiếu kiểm tra QC | Kết luận QC (QA) |

## 3. Các loại yêu cầu duyệt

| Loại (Loại nghiệp vụ) | Phát sinh khi | Cấp duyệt (mục "Phê duyệt › …") | Nhóm có quyền theo seed |
|---|---|---|---|
| Hợp đồng (CONTRACT) | KD **Gửi duyệt** hợp đồng | `crm-contract` | CEO |
| Đơn lệch > 30% (ORDER_DEVIATION) | KD chốt / xác nhận đơn lệch dự báo / lịch sử > 30% | `crm-order` | CEO |
| Đổi đơn sau giờ chốt (ORDER_CHANGE_AFTER_CUTOFF) | Yêu cầu đổi đơn sau 20:00 D-1 (nút **Yêu cầu sửa sau chốt** trong ngăn chi tiết đơn) | `crm-order` | CEO |
| Đề nghị mua (PURCHASE_REQUEST) | Mua hàng **Gửi duyệt** PR | Cấp 1 `scm-pr-l1` → Cấp 2 `scm-pr-l2` | OPS-SCM → COO |
| Đơn mua (PURCHASE_ORDER) | **Gửi duyệt** PO | `scm-po`; PO ≥ 50.000.000 đ thêm `scm-po-director` | CEO / COO; thêm CEO |
| Kiểm kê (STOCK_COUNT) | Kiểm kê chênh lệch > 2% | `scm-stockcount` → `scm-stockcount-l2` | CEO / COO → FIN-ACC |
| Điều chỉnh tồn (STOCK_ADJUSTMENT) | Kho lập điều chỉnh tồn | `scm-adjustment` | CEO / COO |
| Công thức (RECIPE_VERSION) | Kế hoạch **Gửi duyệt** phiên bản công thức | `menu-recipe` | CEO / COO |
| Đề xuất đổi món (MENU_CHANGE) | Lập đề xuất đổi món | `menu-change-request` | CEO / COO |
| Đơn nghỉ (LEAVE_REQUEST) | Nhân viên tạo đơn nghỉ | `hr-leave` | CEO / COO / FIN-HR |
| Đổi ca (SHIFT_SWAP) | Nhân viên tạo đơn đổi ca | `hr-swap` | CEO / COO / FIN-HR |

- Mỗi cấp có **hạn xử lý 48 giờ**; quá hạn hiện nhãn **Quá hạn** nhưng **vẫn duyệt / từ chối được** (BIZ-1.6, D-77 f) – không phải hủy gửi lại; người gửi vẫn hủy được nếu muốn. Leo thang qua bot: quá hạn → báo người duyệt của cấp; quá thêm 24 giờ → báo **COO**; quá thêm 24 giờ nữa (48 giờ sau hạn) → báo **CEO**.
- Một người không duyệt hai cấp của cùng một yêu cầu (cấp sau phải do người khác).

## 4. Thao tác từng bước – `wf-inbox`

1. Đầu màn: thẻ **Chờ tôi duyệt** (đúng người duyệt của bước hiện tại, tự làm mới 30 giây), **Đang chờ (tất cả)**, **Đã duyệt**, **Từ chối**.
2. Chip: **Chờ tôi duyệt** / **Đang chờ** / **Tất cả**.
3. Bảng: Số yêu cầu `XN-…`, Loại nghiệp vụ, Bản ghi nghiệp vụ, Người yêu cầu, Gửi lúc, Bước / Vai trò duyệt, Trạng thái; nút trên dòng **Duyệt**, **Từ chối**, **Hủy yêu cầu** (chỉ người gửi).
4. Bấm dòng → ngăn chi tiết:
   - Thanh các cấp (đã duyệt / đang chờ / từ chối), khối **Bản ghi nghiệp vụ** (thông tin chứng từ; với PR có danh sách dòng).
   - Tab **Tiến trình duyệt**: Người gửi, Trạng thái, Cấp duyệt (n / tổng), Hạn xử lý, Chứng từ; **Các cấp duyệt** (tên bước = tên mục "Phê duyệt › …", người quyết định, lúc, ý kiến); nút **Duyệt / Từ chối / Hủy yêu cầu** theo quyền của bạn.
   - Tab **Quyết định**: lịch sử quyết định.
   - Chân ngăn: **Từ chối**, **Duyệt bước hiện tại**.
5. **Duyệt** → nhập **Ý kiến phê duyệt** (không bắt buộc) → **Duyệt**. Cấp cuối duyệt → chứng từ chuyển trạng thái (vd PR **Đã duyệt**, hợp đồng **Hiệu lực**, đơn nghỉ **Đã duyệt** + bỏ ca).
6. **Từ chối** → **Ý kiến phê duyệt** bắt buộc → chứng từ về trạng thái từ chối / nháp.
7. **Hủy yêu cầu** (người gửi) → **Lý do hủy*** → chứng từ không còn chờ duyệt (không hoàn tác).

Đơn nghỉ / đổi ca cũng duyệt được ngay trên `hr-leave` / `hr-swap` (tab **Xử lý đơn**).

## 5. Trạng thái yêu cầu duyệt

| Yêu cầu | Cấp duyệt |
|---|---|
| Chờ duyệt → Đang duyệt (đã qua ≥ 1 cấp) → Duyệt / Từ chối; Hủy | Chờ → Duyệt / Từ chối / Bỏ qua |

## 6. Lỗi / cảnh báo thường gặp

| Hiện tượng / thông báo | Cách xử lý |
|---|---|
| Không thấy yêu cầu trong "Chờ tôi duyệt" | Chưa tới cấp của bạn, hoặc bạn không có quyền mục "Phê duyệt › …" tương ứng – xem tab "Đang chờ" |
| Bản ghi nghiệp vụ hiện "–" ở yêu cầu điều chỉnh tồn | Điều chỉnh tồn chỉ tạo bút toán sau khi duyệt – xem chi tiết đề xuất trong yêu cầu. Đơn mua / đơn lệch / sửa đơn sau chốt mở được chứng từ; sửa đơn sau chốt hiện bảng **Đề xuất sửa đơn** (đang đặt → đề xuất) |
| Bạn không có quyền duyệt loại yêu cầu này | Xin quản trị cấp mục "Phê duyệt › …" |
| Bạn đã quyết định cấp … của yêu cầu này – cấp sau phải do người khác duyệt | Chuyển người khác duyệt |
| Bước duyệt cấp … đã quá hạn (…) – người gửi hủy yêu cầu và gửi duyệt lại | Báo người gửi hủy và gửi lại |
| Nội dung chứng từ đã thay đổi sau khi gửi duyệt – gửi duyệt lại | Người gửi gửi duyệt lại |
| Yêu cầu không còn chờ duyệt (trạng thái: …) | Đã có người xử lý – làm mới |
| Chỉ người gửi yêu cầu được hủy / Phải ghi lý do hủy… | |
| Chưa cấu hình bước duyệt cho loại … | Báo IT |
| Không thấy nút **Duyệt** kế hoạch / thực đơn / **Khóa & áp dụng** công thức | Tài khoản không có quyền Duyệt loại đó (mục Phê duyệt tương ứng trên ma trận quyền) – nút chỉ hiện với người được duyệt |

## 7. Duyệt trên bot

- **⏳ Chờ tôi duyệt** (`/cho`) – danh sách yêu cầu đến lượt bạn (cần đã liên kết tài khoản web).
- Thẻ duyệt: tiêu đề, người gửi, nội dung tóm tắt, hạn → **✅ Duyệt** / **❌ Từ chối**.
  - **Rủi ro thấp** (đơn nghỉ, đổi ca, đổi món): duyệt **1 chạm**, có **↩️ Hoàn tác** trong 10 giây; nhiều mục cùng loại có **✅ Duyệt nhanh N mục**.
  - Loại khác: bấm Duyệt → bot hỏi lại xác nhận (2 bước).
  - Từ chối: bot yêu cầu **trả lời tin** với lý do.
- Bot báo kết quả duyệt cho người gửi (🟡 APPROVAL_RESULT); yêu cầu quá hạn: 24 giờ báo COO, 48 giờ báo CEO.
- **Liên kết tài khoản web cho người duyệt:** bot yêu cầu mã lấy ở web *menu tài khoản (góc phải) → Liên kết Telegram* rồi gõ `/start <mã>` (mã 15 phút). Trên web: bấm **Mở bot** ngay trong hộp mã để khỏi gõ lệnh.
