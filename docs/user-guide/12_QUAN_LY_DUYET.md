# 12 – Quản lý duyệt (màn Phê duyệt tổng – D-81)

## 1. Bạn là ai

- Người duyệt theo ma trận D-79 (MENU-5): **CEO** (việc lớn: hợp đồng, đơn mua ≥ 50 triệu), **COO** (việc vận hành), **SAL-HEAD** (sửa đơn sau chốt), **OPS-SCM-HEAD** (đề nghị mua cấp 1, kiểm kê cấp 1), **FIN-ACC** (kiểm kê cấp 2), **FIN-HR** (đơn nghỉ / đổi ca), **QA** (kết luận kiểm tra hàng nhập, Đạt / Tạm giữ / Loại mẻ làm tay, Giữ / thả lô). Bảng đủ ở mục 3.
- **Người gửi không duyệt bất kỳ cấp nào của yêu cầu mình** (D-79 a5 – thay D-52 d; cấu hình `BusinessRules:ApprovalBlockSelfApproval`, mặc định bật; tài khoản quản trị được miễn). Yêu cầu do bạn gửi không hiện trong *Chờ tôi duyệt*; hộp duyệt hiện nhãn **Bạn gửi – chờ người khác duyệt**; bấm Duyệt báo *Bạn là người gửi yêu cầu này – cần người khác duyệt*. Bạn vẫn **hủy** được yêu cầu của mình.
- **Tab Tất cả** của màn Phê duyệt (xem mọi yêu cầu): chỉ CEO, CEO-PMO, COO (quyền *Tất cả yêu cầu duyệt* – chỉ xem, **không** làm người có nó duyệt được mọi loại) và FIN-IT. Người duyệt khác thấy Chờ tôi duyệt / Tôi đã gửi / Tôi đã duyệt.
- Quyền duyệt = quyền **Truy cập + Sửa** trên mục menu **"Phê duyệt › …"** (`#/wf-inbox?type=<màn>`) do quản trị cấp (`01_QUAN_TRI.md`).
- Bot: duyệt trên bot **Điều hành** / **Vận hành** (⏳ Chờ tôi duyệt) – chỉ khi tài khoản Telegram đã **liên kết tài khoản web** (xem 7 và `90_CHENH_LECH.md` C-02).

## 2. Màn hình được dùng

**APR-UI-1 (D-81):** sidebar chỉ còn **1 mục "Phê duyệt"** (ngay dưới "Việc của tôi", có số **chờ tôi duyệt**). Mục hiện cho người có ít nhất một quyền duyệt, người có quyền xem hộp chung, hoặc người đang có yêu cầu đã gửi. Các mục cũ "Phê duyệt › <loại>" vẫn giữ trong hệ thống làm **quyền duyệt** (cấp ở màn Phân quyền – khối *Quyền duyệt*, `01_QUAN_TRI.md` §4.5) nhưng không còn trên menu; link cũ `#/wf-inbox?type=…` vẫn mở được (áp làm bộ lọc).

| screen-id | Menu › Mục | Dùng để |
|---|---|---|
| `wf-inbox` | Phê duyệt | Màn tổng: mọi việc chờ bạn duyệt, việc bạn đã gửi, việc bạn đã duyệt; tab Tất cả (CEO / CEO-PMO / COO / FIN-IT – chỉ xem) |
| `prod-plan` | Sản xuất › Kế hoạch sản xuất | Kế hoạch hệ thống giữ Nháp chờ COO cũng hiện trong màn Phê duyệt; nút **Sửa kế hoạch** mở màn này |
| `menu-menu` | Thực đơn & Món ăn › Thực đơn tuần | Nút **Duyệt** thực đơn (COO) – thực đơn không có bước "gửi duyệt" nên không vào màn Phê duyệt |
| `menu-recipe` | Thực đơn & Món ăn › Công thức chế biến | Duyệt cấp cuối = **khóa & áp dụng** luôn (BIZ-2.3) |
| `qc-inspection` | Chất lượng & ATTP › Kiểm tra hàng nhập | Nút **Kết luận** (quyền duyệt "Kiểm tra hàng nhập" – QA) – không vào màn Phê duyệt (D-81 g) |
| `prod-batch` | Sản xuất › Mẻ nấu | Nút **QC: PASS / HOLD / REJECT**, **Loại mẻ** (quyền duyệt "Mẻ nấu" – QA) |
| `scm-lot` | Kho › Lô hàng & hạn dùng | Nút **Tạm giữ / Mở giữ** lô (quyền duyệt "Giữ / thả lô" – QA) |

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

## 4. Thao tác từng bước – màn Phê duyệt (`wf-inbox`)

1. Bấm **Phê duyệt** trên sidebar. Đầu màn: 4 thẻ **Chờ tôi duyệt** (số có cảnh báo / bình thường) · **Quá hạn** · **Hết hạn trong 8 giờ** · **Tôi gửi đang chờ** (lâu nhất bao lâu). Ô **Tìm** (số chứng từ, khách, NCC, người gửi) lọc ngay tại chỗ. Danh sách tự làm mới 30 giây.
2. Tab **Chờ tôi duyệt**: lọc nhanh theo phân hệ (Kinh doanh · Thực đơn · Mua hàng · Kho · Sản xuất · Nhân sự – có số). Xếp sẵn: **quá hạn → sắp hết hạn → cũ nhất**. Mỗi dòng: loại + số chứng từ, tóm tắt (vd. "NCC Rau sạch · 38.450.000 đ · 12 dòng"), **Vì sao cần duyệt**, nhãn **⚠ N cảnh báo: …** hoặc **✓ Bình thường**, người gửi + cấp, hạn (đỏ = quá hạn, cam = còn dưới 8 giờ).
3. Bấm dòng → **Xem nhanh** (màn rộng: ngăn bên phải; máy tính bảng: lớp phủ; điện thoại: toàn màn, nút cố định cuối): vì sao cần duyệt, cảnh báo, thông tin chính, các dòng chứng từ (ô bất thường tô màu), tiến trình các cấp, link **chứng từ gốc ↗**, **‹ Trước / Tiếp theo ›**.
4. **Duyệt & sang việc tiếp**: ý kiến không bắt buộc; duyệt xong tự mở việc kế tiếp. Yêu cầu bình thường = **≤ 2 lần bấm** (bấm dòng → Duyệt).
5. **Từ chối**: ô **Ý kiến** bắt buộc (bỏ trống → báo "Nhập lý do từ chối"). Kế hoạch sản xuất giữ Nháp không có Từ chối – bấm **Sửa kế hoạch**.
6. **Duyệt nhiều**: chỉ tick được dòng **Bình thường** thuộc loại rủi ro thấp (`BusinessRules:ApprovalBulkTypes` – mặc định đơn nghỉ, đổi ca) → **Duyệt các mục đã chọn**. Máy chủ kiểm lại từng yêu cầu: có cảnh báo / không còn chờ bạn → bỏ qua và báo lý do. **Không từ chối hàng loạt.** Hợp đồng, chính sách giá, PO ≥ 50 triệu, kiểm kê, sửa đơn sau chốt luôn mở từng yêu cầu.
7. Tab **Tôi đã gửi**: yêu cầu của bạn còn mở – đang chờ ai (tên người có quyền duyệt cấp đó), đã chờ bao lâu, cấp; nút **Nhắc qua bot** (mỗi yêu cầu tối đa **1 lần / 4 giờ** – `ApprovalRemindHours`; bấm sớm hơn báo "Đã nhắc lúc … – nhắc lại được sau …") và **Hủy** (lý do bắt buộc).
8. Tab **Tôi đã duyệt**: 50 quyết định gần nhất (đã duyệt / từ chối, ý kiến). Tab **Tất cả** (CEO / CEO-PMO / COO / FIN-IT): mọi yêu cầu – chỉ xem.

**Cảnh báo máy chủ tính (D-81 f – chỉ đọc dữ liệu có sẵn; ngưỡng trong `BusinessRules`):** PO – giá dòng cao hơn lần mua trước cùng NCC ≥ 10 %, NCC không phải ưu tiên cho nguyên liệu, PO lập tay không từ đề nghị mua; ĐN mua – tồn + đề nghị vượt định mức tối đa; sửa đơn sau chốt – kế hoạch ngày ăn đã duyệt / phát hành (sẽ sinh mẻ bổ sung), lệch > 30 %; kiểm kê – dòng lệch > 5 %; đơn nghỉ – ca đã phân trong ngày nghỉ chưa có người thay; hợp đồng – giá suất thấp hơn giá vốn chuẩn bình quân; mọi loại – người gửi bị từ chối loại này ≥ 2 lần / 30 ngày; người duyệt cấp này đang nghỉ (đã báo bậc leo thang).

**Người duyệt nghỉ (D-81 i):** mọi người có quyền duyệt cấp đang chờ đều có đơn nghỉ **Đã duyệt** hôm nay → hệ thống báo ngay **COO** (COO cũng nghỉ → **CEO**) qua bot Điều hành, không chờ 48 giờ; yêu cầu hiện cảnh báo "Người duyệt cấp này đang nghỉ". Ủy quyền đích danh: GAP-APR-04 (chưa làm).

Đơn nghỉ / đổi ca vẫn duyệt được ngay trên `hr-leave` / `hr-swap` (tab **Xử lý đơn**).

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

- **⏳ Chờ tôi duyệt** (`/cho`) – cùng danh sách với web: mỗi dòng 🔴 quá hạn / ⚠️ có cảnh báo / 🟢 bình thường + loại + số chứng từ + tóm tắt.
- **Thẻ duyệt** (cùng khuôn tóm tắt với web – APR-1.7): nhãn **🔴 CẦN DUYỆT · QUÁ HẠN** / **⚠️ CẦN DUYỆT · N CẢNH BÁO** / **🟢 BÌNH THƯỜNG** + hạn, loại + số chứng từ, tóm tắt, *Vì sao cần duyệt*, tối đa 3 cảnh báo, người gửi + cấp → **✅ Duyệt** / **❌ Từ chối** / **📄 Xem chi tiết** (thông tin chính + 8 dòng đầu chứng từ).
  - **Rủi ro thấp** (đơn nghỉ, đổi ca, đổi món) và **không có cảnh báo**: duyệt **1 chạm**, có **↩️ Hoàn tác** trong 10 giây; **✅ Duyệt nhanh N mục** chỉ gồm mục không cảnh báo.
  - Loại khác: bấm Duyệt → bot hỏi lại xác nhận (2 bước). Từ chối: **trả lời tin** với lý do.
- Tin "Chờ bạn duyệt" khi có yêu cầu mới cũng theo khuôn này. Người gửi bấm **Nhắc qua bot** trên web → người duyệt nhận tin "🔔 Người gửi nhắc …".
- **Tổng hợp cuối ngày** (bot Điều hành, từ `Messaging:ApprovalDigestHour` = 18 giờ, 1 lần / ngày): còn chờ bạn N (quá hạn, có cảnh báo) + nút **⏳ Mở Phê duyệt**. Phần "hệ thống tự duyệt hôm nay" chỉ có khi bật hậu kiểm (chưa bật – D-81 k).
- Bot báo kết quả duyệt cho người gửi (🟡 APPROVAL_RESULT); quá hạn: 24 giờ báo COO, 48 giờ báo CEO; người duyệt nghỉ → báo ngay bậc kế.
- **Liên kết tài khoản web cho người duyệt:** bot yêu cầu mã lấy ở web *menu tài khoản (góc phải) → Liên kết Telegram* rồi gõ `/start <mã>` (mã 15 phút). Trên web: bấm **Mở bot** ngay trong hộp mã để khỏi gõ lệnh.
