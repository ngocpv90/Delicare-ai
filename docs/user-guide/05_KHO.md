# 05 – Kho (nhận hàng, tồn kho, lô, xuất kho, kiểm kê)

## 1. Bạn là ai

- Nhóm quyền: **OPS-SCM** (1.2 Mua hàng và Kho) – thủ kho, nhân viên kho. Nhóm **QA** cũng có quyền sửa các màn kho.
- Bot dùng: **Vận hành** (phân hệ 📦 Kho & mua hàng); QR dán ở kệ / vị trí kho.
- Việc chính: nhận hàng theo PO, ghi sổ nhập kho sau QC, theo dõi tồn theo lô (FEFO), xuất nguyên liệu cho sản xuất, chuyển kho, điều chỉnh tồn, kiểm kê, xử lý lô hết hạn, truy xuất.

**Nguyên tắc sổ kho:** tồn kho tính từ **sổ cái giao dịch**, không sửa trực tiếp. Mọi thay đổi đi qua phiếu nhập, phiếu xuất, chuyển kho, điều chỉnh, kiểm kê. Bút toán đã ghi **không sửa, không xóa** được.

## 2. Màn hình được dùng

| screen-id | Menu › Mục | Dùng để |
|---|---|---|
| `scm-po` | Mua hàng & Kho › Đơn mua hàng | Nút **Nhận hàng** |
| `scm-gr` | … › Nhập kho | Phiếu nhập: Mở QC, Ghi sổ, Hủy, In |
| `scm-inv-balance` | … › Số dư tồn kho | Tồn theo kho × nguyên liệu, theo lô; Chuyển kho; Điều chỉnh tồn |
| `scm-lot` | … › Lô nguyên liệu | Lọc hạn dùng, Tạm giữ / Mở giữ, Xử lý lô hết hạn, Truy xuất |
| `scm-inv-txn` | … › Giao dịch tồn kho | Sổ cái; Chuyển kho; Điều chỉnh tồn |
| `scm-reservation` | … › Đặt trước vật tư | Giữ chỗ nguyên liệu theo kế hoạch / lệnh SX |
| `scm-issue` | … › Xuất vật tư | Phiếu xuất sản xuất: gợi ý lô, tạo, ghi sổ, hủy, in |
| `scm-stockcount` | … › Kiểm kê | Tạo phiếu, kiểm đếm, gửi duyệt, ghi sổ, in |
| `scm-consumption-var` | … › Chênh lệch tiêu hao | So định mức với thực xuất (xem) |
| `scm-warehouse` | … › Kho | Kho + **Sơ đồ** |
| `scm-zone`, `scm-location` | … › Khu vực / Vị trí lưu trữ | Danh mục (khu có ngưỡng nhiệt độ) |
| `scm-inv-policy` | … › Chính sách tồn kho | Tối thiểu ≤ an toàn ≤ điểm đặt hàng ≤ tối đa, FEFO / FIFO |

Màn `scm-transfer` (Chuyển kho) và `scm-adjustment` (Điều chỉnh tồn) **ẩn trên menu** (MENU-4) – hai việc này làm bằng nút trên `scm-inv-balance` / `scm-inv-txn`; link cũ tới hai màn này tự mở màn Tồn kho. Các mục "Chi tiết phiếu nhập / phiếu xuất / kiểm kê" cũng đã bỏ: xem dòng trong chi tiết phiếu, hoặc **Lọc theo nguyên liệu** trên danh sách Phiếu nhập / Phiếu xuất.

## 3. Việc trong ngày

| Giờ | Việc | Màn |
|---|---|---|
| 00:15 | Hệ thống tự chuyển lô quá hạn → **Hết hạn** + ghi hủy tồn | tự động |
| Sáng | Xem cận date / dưới tồn tối thiểu (chuông, bot) | `scm-lot`, `scm-inv-balance` |
| Khi hàng về | **Nhận hàng** theo PO → QC kết luận → **Ghi sổ** | `scm-po` → `qc-inspection` → `scm-gr` |
| Trước giờ nấu | **Xuất kho** phần nguyên liệu đã giữ chỗ cho lệnh SX | `scm-issue` |
| Khi hàng về sau | **Giữ chỗ bổ sung** cho lệnh còn thiếu | `scm-reservation` |
| Định kỳ | **Kiểm kê** | `scm-stockcount` |
| 21:00 | Bot gửi nguyên liệu cần cho ngày mai | bot |

## 4. Thao tác từng bước

### 4.1 Nhận hàng theo PO – `scm-po` → **Nhận hàng**
1. Trên dòng PO (Đã duyệt / Đã gửi NCC / Nhận một phần) bấm **Nhận hàng**.
2. Hộp **Nhận hàng – PO…**: chọn **Kho nhận**; mỗi dòng còn chờ nhận nhập **Số nhận** (mặc định = còn lại; 0 = không nhận lần này), **Số lô NCC**, **Hạn dùng** (trống = ngày nhận + hạn bảo quản của nguyên liệu), **Nhiệt độ °C** (bắt buộc với hàng Mát / Đông lạnh).
3. **Tạo phiếu nhập** → phiếu `PN-…` ở **Chờ QC**, kèm phiếu QC; màn chuyển sang **Phiếu kiểm tra QC** để QA kết luận.
- Nhận vượt tối đa 10% số đặt mỗi dòng: *… nhận … vượt số còn được nhận …*.
- Cảnh báo nhiệt độ ngay khi tạo: *nhiệt độ nhận … vượt ngưỡng … – vùng loại, chỉ kết luận KHONG_DAT* / *nhận có điều kiện – cần lý do…* / *chưa có nhiệt độ nhận*.

### 4.2 Ghi sổ phiếu nhập – `scm-gr`
| Nút | Khi nào | Kết quả |
|---|---|---|
| **Mở QC** | Chờ QC | Sang màn Phiếu kiểm tra QC |
| **Ghi sổ** | Đạt / Đạt một phần / Từ chối (QC đã kết luận) | Tạo **lô** `LO-…`, nhập kho **phần đạt**, cập nhật PO (Nhận một phần / Đã nhận đủ) |
| **In** | Đạt / Đạt một phần / Từ chối / Đã ghi sổ | Phiếu nhập kho PDF |
| **Hủy** | Chưa ghi sổ, chưa hủy | Bắt buộc lý do |
- *Phiếu nhập chưa kết luận QC – chưa ghi sổ được* → nhờ QA kết luận.

### 4.3 Tồn kho – `scm-inv-balance`
1. Lọc **Kho** (Tất cả kho) và tick **Chỉ nguyên liệu dưới mức tồn**.
2. Thẻ: Nguyên liệu có tồn, **Dưới tồn tối thiểu**, **Dưới điểm đặt hàng**, **Lô sắp hết hạn** (+ số lô đã hết hạn).
3. Bảng: Mã, Nguyên liệu, Kho, Tồn thực, Đã giữ, **Xuất được** (+ "bị chặn"), ĐVT, Tối thiểu / an toàn / đặt hàng, **Mức tồn** (Dưới tối thiểu / Cần đặt hàng / Đủ hàng), Số lô, Sắp hết hạn (+ ngày hết hạn gần nhất).
4. Bấm một dòng → **Tồn theo lô** (sắp theo thứ tự nên xuất trước, nhãn **Xuất trước**, lý do bị chặn, còn / quá hạn N ngày) với nút **Chuyển** / **Điều chỉnh** trên từng lô.
5. **Tính lại tồn** (chỉ quản trị): tính lại số tồn từ sổ cái, ghi đè số hiện tại.

### 4.4 Chuyển kho / vị trí – nút **Chuyển kho**
1. **Chuyển kho** (ở `scm-inv-balance` hoặc `scm-inv-txn`, hoặc trên một lô).
2. Chọn **Nguyên liệu***, Lô, **Từ kho*** + vị trí, **Đến kho*** + vị trí, **Số lượng chuyển***, **Lý do*** (≥ 3 ký tự) → **Chuyển kho** → xác nhận.
- *Nơi chuyển đến phải khác nơi chuyển đi*, *Không đủ tồn khả dụng của …: còn …, cần …*.

### 4.5 Điều chỉnh tồn – nút **Điều chỉnh tồn**
1. Chọn Nguyên liệu*, Lô, Kho*, Vị trí, **Số lượng điều chỉnh** (+ tăng / − giảm, khác 0), Đơn giá (khi nhập thêm), **Lý do***, Ghi chú → **Ghi điều chỉnh** → xác nhận.
2. Mặc định điều chỉnh tay **phải được duyệt** ("Phê duyệt › Phiếu điều chỉnh tồn") trước khi ghi sổ: *Đã gửi duyệt điều chỉnh tồn (yêu cầu …) – chỉ ghi sổ khi được duyệt*.

### 4.6 Lô nguyên liệu – `scm-lot`
- Chip lọc: **Tất cả / Còn tồn / Hết hạn trong 3 ngày / Trong 7 ngày / Trong 30 ngày**.
- Cột: Số lô, Nguyên liệu, NCC, Ngày nhận, HSD (đỏ nếu sắp hết), Tồn, Khả dụng, QC, Trạng thái.
- Nút trên dòng:
  - **Truy xuất** → chuỗi *Nhà cung cấp → Đơn mua → Phiếu nhập → Lô → Phiếu xuất → Lệnh SX → Mẻ (+ "truy ngược mẻ") → Nhiệt độ → Lệnh đóng gói → Đơn hàng → Đơn giao → Khách hàng*.
  - **Tạm giữ** (lô Khả dụng): bắt buộc lý do – lô tạm giữ không được gợi ý, không xuất được.
  - **Mở giữ** (lô Tạm giữ): lô trở lại khả dụng.
- **Xử lý lô hết hạn** (đầu trang): xem trước danh sách lô quá hạn (+ lô sắp hết hạn), giá trị hủy ước tính → **Chuyển hết hạn và hủy tồn N lô** → ghi chú → xác nhận. Lô đang tạm giữ được bỏ qua (xử lý riêng).

### 4.7 Giữ chỗ nguyên liệu – `scm-reservation`
1. Khối **Giữ chỗ theo kế hoạch sản xuất**: bấm nút kế hoạch (`KS-…` · ngày) → bảng giữ chỗ mọi lệnh (Cần, Đang giữ, **Chưa giữ được**, trạng thái); bấm số lệnh để mở lệnh.
2. Nút **Lệnh SX** trên dòng → hộp giữ chỗ của lệnh:
   - **Giữ chỗ bổ sung** (khi còn thiếu và hàng đã về) → giữ thêm từ tồn khả dụng.
   - **Xuất kho cho lệnh** → mở hộp xuất kho (4.8).

### 4.8 Xuất nguyên liệu cho sản xuất – `scm-issue`
**Cách 1 – theo lệnh đã giữ chỗ**
1. Khối **Lệnh sản xuất đã giữ chỗ – chờ xuất kho**: bấm nút lệnh (`LS-…` · món).
2. Hộp **Xuất kho cho lệnh …**: chọn **Kho xuất***, Ghi chú; bảng nguyên liệu + **Số lượng xuất** (mặc định = đang giữ).
3. **Gợi ý lô** → mỗi dòng hiện lô nên xuất (theo hạn dùng gần nhất / nhập trước xuất trước), số thiếu, số lô bị chặn.
4. **Tạo phiếu xuất** → phiếu **Nháp**; hộp hỏi **Ghi sổ xuất kho ngay?** → **Ghi sổ xuất kho** (tồn bị trừ theo từng lô, không sửa được).
5. **Xuất theo định mức của mẻ** (nếu lệnh có mẻ Kế hoạch / Đang nấu): bấm nút mẻ → phiếu xuất nháp theo định mức × số phần của mẻ.

**Cách 2 – phiếu xuất tay:** **Tạo phiếu xuất** → chọn kho, thêm dòng (nguyên liệu, số lượng) → Gợi ý lô → Tạo phiếu xuất.

**Trên danh sách phiếu xuất:** **Ghi sổ** (Nháp), **Hủy** (Nháp; hoặc Đã xuất – ghi bút toán đảo, tồn trở lại; bắt buộc lý do), **In** (Đã xuất).
- Không chọn lô bằng tay – hệ thống tự phân bổ. *Cảnh báo: còn lô … nên xuất trước lô …*.

### 4.9 Kiểm kê – `scm-stockcount`
1. **Tạo phiếu kiểm kê** → **Kho kiểm kê***, Loại (Đột xuất / Hằng ngày / Hằng tuần / Hằng tháng), Ngày kiểm kê, Người phụ trách, Chỉ kiểm một nguyên liệu (tùy chọn), Ghi chú → **Tạo phiếu**. Hệ thống lấy mọi nguyên liệu + lô đang có tồn làm dòng và mở hộp kiểm đếm.
2. Hộp **Kiểm đếm – phiếu …**: mỗi dòng Nguyên liệu, Lô (HSD), **Tồn hệ thống**, **Số đếm thực tế**, Chênh lệch (đỏ nếu vượt ngưỡng), **Lý do chênh lệch** (bắt buộc khi lệch).
3. **Lưu số đếm** (phải lưu trước khi gửi duyệt / ghi sổ).
4. Nếu có dòng lệch **> 2%** → **Gửi duyệt** (2 cấp: Quản lý kho → Kế toán). Chờ duyệt xong mới ghi sổ.
5. **Ghi sổ** → mỗi dòng lệch tạo một bút toán điều chỉnh; tồn = số đếm. Không hoàn tác.
6. **In** biên bản kiểm kê bất kỳ lúc nào.
- Trên danh sách: **Kiểm đếm** (Nháp / Đang kiểm), **Ghi sổ** (đã duyệt), "Chờ duyệt →" (đang chờ), **In**.

### 4.10 Chênh lệch tiêu hao – `scm-consumption-var`
Chọn khoảng ngày, tick **Chỉ dòng vượt ngưỡng cảnh báo** (> 5%) → **Tải lại**. Số liệu sinh khi **đóng lệnh sản xuất**.

### 4.11 Kho, khu, vị trí – `scm-warehouse`, `scm-zone`, `scm-location`
Nút **Sơ đồ** trên dòng kho → các khu bảo quản (ngưỡng nhiệt độ) và vị trí trong khu.

## 5. Trạng thái

| Chứng từ | Luồng |
|---|---|
| Phiếu nhập | Chờ QC → Đạt / Đạt một phần / Từ chối → Đã ghi sổ; Hủy |
| Lô | Khả dụng ↔ Tạm giữ → Hết hạn / Đã dùng hết; QC: Đạt / Tạm giữ / Từ chối |
| Giữ chỗ | Đã giữ → Đã xuất một phần → Đã xuất → Đã giải phóng |
| Phiếu xuất | Nháp → Đã xuất; Hủy |
| Phiếu kiểm kê | Nháp → Đang kiểm → Chờ duyệt → Đã ghi sổ |

## 6. Lỗi / cảnh báo thường gặp

| Thông báo | Cách xử lý |
|---|---|
| Chỉ nhận hàng theo PO Đã duyệt / Đã gửi NCC / Nhận một phần | PO chưa duyệt – chờ duyệt |
| Nhập số nhận cho ít nhất một dòng / Đơn mua này không còn dòng nào chờ nhận | Kiểm số nhận |
| {nguyên liệu}: ngày sản xuất sau hạn dùng | Sửa hạn dùng |
| Không đủ tồn khả dụng của …: còn …, cần … | Giảm số xuất / chờ hàng về / giữ chỗ bổ sung |
| Đơn vị tính khác đơn vị của tồn kho hiện có (…) | Dùng đúng đơn vị tồn kho |
| Chỉ tạm giữ lô đang khả dụng / Chỉ mở lô đang tạm giữ | Kiểm trạng thái lô |
| Mẻ … đã có phiếu xuất … – xuất thêm qua MaterialIssue/Create hoặc hủy phiếu cũ | Dùng **Tạo phiếu xuất** theo lệnh |
| Lệnh này không còn nguyên liệu đang giữ chỗ để xuất | Đã xuất đủ / chưa giữ chỗ → Giữ chỗ bổ sung |
| Dòng … lệch … – phải ghi lý do chênh lệch | Nhập lý do |
| Chênh lệch kiểm kê …% vượt ngưỡng 2% – gửi duyệt … | Bấm Gửi duyệt |
| Phiếu kiểm kê đang chờ duyệt – chưa ghi sổ được | Chờ duyệt |
| Phiếu kiểm kê không có dòng lệch > 2% – không cần duyệt, ghi sổ trực tiếp | Bấm Ghi sổ |
| Tồn kho được tính từ sổ cái – không thêm / sửa / xóa trực tiếp | Dùng điều chỉnh / kiểm kê |
| Phải thỏa Tồn tối thiểu ≤ Tồn an toàn ≤ Điểm đặt hàng ≤ Tồn tối đa | Sửa chính sách tồn |

## 7. Trên bot Telegram

Phân hệ **📦 Kho & mua hàng** (bot Vận hành): 📦 Tồn dưới mức (`/ton`), ⌛ Cận date (`/candate`), 🥬 NL ngày mai (`/nlmai`), 🧾 PO sắp về / trễ (`/po`), 🚛 Hàng về hôm nay (`/hangve`).
- **QR ở kệ / vị trí** (`?start=LOC_<mã vị trí>`): tồn tại vị trí + lô cận date.
- **QR trên phiếu in** (phiếu nhập, phiếu xuất, phiếu kiểm kê): mở đúng phiếu trên bot.
- Thông báo: dưới tồn tối thiểu, lô cận / hết hạn, 21:00 nguyên liệu cho ngày mai, 🔴 QC đầu vào không đạt, IoT nhiệt độ kho ngoài ngưỡng.
