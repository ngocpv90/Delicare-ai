# 12 – Quản lý duyệt (Hộp phê duyệt)

## 1. Bạn là ai

- Người duyệt theo ma trận D-79 (MENU-5): **CEO** (việc lớn: hợp đồng, đơn mua ≥ 50 triệu), **COO** (việc vận hành), **SAL-HEAD** (sửa đơn sau chốt), **OPS-SCM-HEAD** (đề nghị mua cấp 1, kiểm kê cấp 1), **FIN-ACC** (kiểm kê cấp 2), **FIN-HR** (đơn nghỉ / đổi ca), **QA** (kết luận kiểm tra hàng nhập, Đạt / Tạm giữ / Loại mẻ làm tay, Giữ / thả lô). Bảng đủ ở mục 3.
- **Người gửi không duyệt bất kỳ cấp nào của yêu cầu mình** (D-79 a5 – thay D-52 d; cấu hình `BusinessRules:ApprovalBlockSelfApproval`, mặc định bật; tài khoản quản trị được miễn). Yêu cầu do bạn gửi không hiện trong *Chờ tôi duyệt*; hộp duyệt hiện nhãn **Bạn gửi – chờ người khác duyệt**; bấm Duyệt báo *Bạn là người gửi yêu cầu này – cần người khác duyệt*. Bạn vẫn **hủy** được yêu cầu của mình.
- **Hộp phê duyệt chung** (xem mọi yêu cầu, tab Đang chờ / Tất cả): chỉ CEO, CEO-PMO, COO (mục *Phê duyệt › Tất cả yêu cầu duyệt* – chỉ xem, **không** làm người có nó duyệt được mọi loại) và FIN-IT. Người duyệt khác bấm mục *Phê duyệt › <loại>* của mình → chỉ thấy danh sách **Chờ tôi duyệt**.
- Quyền duyệt = quyền **Truy cập + Sửa** trên mục menu **"Phê duyệt › …"** (`#/wf-inbox?type=<màn>`) do quản trị cấp (`01_QUAN_TRI.md`).
- Bot: duyệt trên bot **Điều hành** / **Vận hành** (⏳ Chờ tôi duyệt) – chỉ khi tài khoản Telegram đã **liên kết tài khoản web** (xem 7 và `90_CHENH_LECH.md` C-02).

## 2. Màn hình được dùng

| screen-id | Menu › Mục | Dùng để |
|---|---|---|
| `wf-inbox?type=…` | Phê duyệt › <loại> | Duyệt / từ chối yêu cầu chờ mình ở cấp hiện tại (mỗi loại 1 mục, xem mục 3) |
| `wf-inbox?type=wf-inbox` | Phê duyệt › Tất cả yêu cầu duyệt | Xem mọi yêu cầu (CEO / CEO-PMO / COO – chỉ xem) |
| `wf-step` | (màn con, ẩn) | Các cấp duyệt của yêu cầu – xem trong tab Tiến trình duyệt |
| `prod-plan` | Sản xuất › Kế hoạch sản xuất | Nút **Duyệt** kế hoạch (quyền Phê duyệt › Kế hoạch sản xuất – COO) |
| `menu-menu` | Thực đơn & Món ăn › Thực đơn tuần | Nút **Duyệt** thực đơn (COO) |
| `menu-recipe` | Thực đơn & Món ăn › Công thức chế biến | Duyệt cấp cuối = **khóa & áp dụng** luôn (BIZ-2.3); nút **Áp dụng** chỉ cho bản Đã duyệt cũ (COO) |
| `qc-inspection` | Chất lượng & ATTP › Kiểm tra hàng nhập | Nút **Kết luận** (quyền Phê duyệt › Kiểm tra hàng nhập – QA) |
| `prod-batch` | Sản xuất › Mẻ nấu | Nút **QC: PASS / HOLD / REJECT**, **Loại mẻ** (quyền Phê duyệt › Mẻ nấu – QA); bếp thấy *Chờ QA kết luận*. Mẻ hệ thống tự Đạt (BIZ-2.7) không đổi |
| `scm-lot` | Kho › Lô hàng & hạn dùng | Nút **Tạm giữ / Mở giữ** lô (quyền Phê duyệt › Giữ / thả lô – QA) |

## 3. Các loại yêu cầu duyệt

| Loại (Loại nghiệp vụ) | Phát sinh khi | Cấp duyệt (mục "Phê duyệt › …") | Nhóm có quyền theo seed |
|---|---|---|---|
| Hợp đồng (CONTRACT) | KD **Gửi duyệt** hợp đồng | `crm-contract` | CEO |
| Đơn lệch > 30% (ORDER_DEVIATION) | **Không còn phát sinh** (BIZ-2.1: trước giờ chốt sửa số không đi duyệt, lệch > 30 % so TB 4 tuần chỉ cảnh báo). Yêu cầu loại này tạo trước đó vẫn duyệt / từ chối được | `crm-order` (Phê duyệt › Đơn đặt suất (sửa sau chốt)) | SAL-HEAD / COO |
| Đổi đơn sau giờ chốt (ORDER_CHANGE_AFTER_CUTOFF) | Yêu cầu đổi đơn sau 20:00 D-1 (nút **Yêu cầu sửa sau chốt** trong ngăn chi tiết đơn) | `crm-order` (Phê duyệt › Đơn đặt suất (sửa sau chốt)) | **SAL-HEAD (Trưởng KD) / COO** (D-79 d – thay SAL-CON) |
| Đề nghị mua (PURCHASE_REQUEST) | Mua hàng **Gửi duyệt** PR | Cấp 1 `scm-pr-l1` (Đề nghị mua – cấp 1) → Cấp 2 `scm-pr-l2` (Đề nghị mua – cấp 2) | OPS-SCM-HEAD → COO |
| Đơn mua (PURCHASE_ORDER) | **Gửi duyệt** PO | `scm-po` (Đơn mua hàng); PO ≥ 50.000.000 đ: `scm-po-director` (Đơn mua hàng ≥ 50 triệu) | COO; ≥ 50 triệu: CEO |
| Kiểm kê (STOCK_COUNT) | Kiểm kê chênh lệch > 2% | `scm-stockcount` (Phiếu kiểm kê – cấp 1) → `scm-stockcount-l2` (Phiếu kiểm kê – cấp 2) | OPS-SCM-HEAD / COO → FIN-ACC |
| Điều chỉnh tồn (STOCK_ADJUSTMENT) | Kho lập điều chỉnh tồn | `scm-adjustment` | COO |
| Công thức (RECIPE_VERSION) | Kế hoạch **Gửi duyệt** phiên bản công thức | `menu-recipe` | COO |
| Đề xuất đổi món (MENU_CHANGE) | Lập đề xuất đổi món | `menu-change-request` | COO |
| Kế hoạch sản xuất | Nút **Duyệt** trên kế hoạch | `prod-plan` | COO |
| Thực đơn tuần | Nút **Duyệt** trên thực đơn | `menu-menu` | COO |
| Đơn nghỉ (LEAVE_REQUEST) | Nhân viên tạo đơn nghỉ | `hr-leave` | FIN-HR / COO |
| Đổi ca (SHIFT_SWAP) | Nhân viên tạo đơn đổi ca | `hr-swap` | FIN-HR / COO |
| Kết luận kiểm tra hàng nhập | Nút **Kết luận** trên phiếu QC đầu vào (không qua hộp duyệt) | `qc-inspection` | QA |
| Đạt / Tạm giữ / Loại mẻ làm tay | Nút QC trên mẻ (không qua hộp duyệt) | `prod-batch` (Phê duyệt › Mẻ nấu) | QA |
| Giữ / thả lô | Nút **Tạm giữ / Mở giữ** trên lô (không qua hộp duyệt) | `scm-lot` (Phê duyệt › Giữ / thả lô) | QA |

- Hợp đồng · Chính sách giá · Đơn mua ≥ 50 triệu: CEO (D-79 a6 – CEO chỉ duyệt việc lớn; quá hạn vẫn leo thang lên CEO). Mục *Phê duyệt › Chính sách giá* đang **ẩn** vì chưa có loại duyệt nào dùng (chính sách giá có hiệu lực ngay khi lưu) – em ghi GAP-PERM-03; mục cũ *Đề nghị mua hàng* (`scm-pr`) ẩn (D-52 a).
- DB đang chạy: quyền duyệt đặt bằng `DB/scripts/D79_permission_matrix.sql` (thay `BIZ2_order_approvers.sql`).

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
