# 05 – Kho (nhận hàng, tồn kho, lô, xuất kho, kiểm kê)

## 1. Bạn là ai
> **BIZ-2.5 – Phiếu xuất theo ca:** phát hành lệnh SX → hệ thống tạo sẵn **phiếu xuất Nháp** cho mọi lệnh của ca (số = số đã giữ chỗ, lô FEFO phân bổ sẵn), bot kho nhận "Phiếu xuất ca … sẵn sàng". Màn **Xuất kho** có khối **Phiếu xuất theo ca**: kiểm / sửa từng phiếu như phiếu thường rồi bấm **Ghi sổ cả ca** một lần. Kho xuất = kho có nhiều tồn nhất cho nguyên liệu của lệnh.


- Nhóm quyền: **Thủ kho** (OPS-WH – thủ kho, nhân viên kho: ghi sổ nhập / xuất, kiểm kê; MENU-5 D-79); **Trưởng mua hàng & kho** (OPS-SCM-HEAD) duyệt kiểm kê cấp 1. Quyền từng màn: `docs/claude/MENU5_MATRIX.md`.
- Bot dùng: **Vận hành** (phân hệ 📦 Kho & mua hàng); QR dán ở kệ / vị trí kho.
- Việc chính: nhận hàng theo PO, ghi sổ nhập kho sau QC, theo dõi tồn theo lô (FEFO), xuất nguyên liệu cho sản xuất, chuyển kho, điều chỉnh tồn, kiểm kê, xử lý lô hết hạn, truy xuất.

**Nguyên tắc sổ kho:** tồn kho tính từ **sổ cái giao dịch**, không sửa trực tiếp. Mọi thay đổi đi qua phiếu nhập, phiếu xuất, chuyển kho, điều chỉnh, kiểm kê. Bút toán đã ghi **không sửa, không xóa** được.

## 2. Màn hình được dùng

<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->
Quyền theo ma trận D-79 (đủ 19 nhóm: `docs/claude/MENU5_MATRIX.md`).

| Menu › Mục | screen-id | OPS-WH | OPS-SCM-HEAD |
|---|---|---|---|
| Việc của tôi › Việc của tôi | `hr-my` | X | X |
| Phê duyệt › Đề nghị mua – cấp 1 | `wf-inbox?type=scm-pr-l1` | — | Duyệt |
| Phê duyệt › Phiếu kiểm kê – cấp 1 | `wf-inbox?type=scm-stockcount` | — | Duyệt |
| Phê duyệt › Xếp xe chưa đủ hàng | `wf-inbox?type=log-load-short` | — | Duyệt |
| Điều hành › Bảng điều hành | `dash-exec` | — | X |
| Điều hành › Chỉ số kinh doanh | `kpi-daily` | — | X |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | — | X |
| Thực đơn & Món ăn › Công thức chế biến | `menu-recipe` | — | X |
| Mua hàng › Tính nhu cầu nguyên liệu | `scm-requirement` | — | S (IX) |
| Mua hàng › Đề nghị mua hàng | `scm-pr` | — | S (IX) |
| Mua hàng › Đơn mua hàng | `scm-po` | X | S (IX) |
| Mua hàng › Nhà cung cấp | `scm-supplier` | X | Q |
| Mua hàng › Bảng giá nhà cung cấp | `scm-supplier-material` | — | Q |
| Mua hàng › Nguyên liệu | `scm-ingredient` | X | Q |
| Kho › Nhận hàng | `scm-gr` | S (IX) | S (IX) |
| Kho › Trả hàng NCC | `scm-supplier-return` | S (IX) | S (IX) |
| Kho › Xuất kho sản xuất | `scm-issue` | S (IX) | S (IX) |
| Kho › Tồn kho | `scm-inv-balance` | Q (IX) | Q (IX) |
| Kho › Kiểm kê | `scm-stockcount` | S (IX) | S (IX) |
| Sản xuất › Kế hoạch sản xuất | `prod-plan` | X | X |
| Chất lượng & ATTP › Sổ kiểm tra hàng nhập | `qc-inspection` | — | X |
| Chất lượng & ATTP › Sự cố nhà cung cấp | `qc-supplier-issue` | — | X |
| Đóng gói & Giao hàng › Định mức bao bì | `log-packaging-norm` | X | X |
| Danh mục hệ thống › Đơn vị tính | `scm-uom` | — | Q |
| Danh mục hệ thống › Kho & vị trí | `scm-warehouse` | Q | Q |
| Tri thức › Kho tri thức | `knowledge` | X | X |
| Tri thức › 30 Thực đơn & Công thức | `knowledge?folder=30` | — | X |
| Tri thức › 40 Kho & Truy xuất | `knowledge?folder=40` | S | S |
| Tri thức › 45 ATTP | `knowledge?folder=45` | X | — |
| Tri thức › 60 Kế toán | `knowledge?folder=60` | — | X |
| Tri thức › 80 Đào tạo | `knowledge?folder=80` | X | X |
| Tri thức › 99 Mẫu | `knowledge?folder=99` | X | X |
<!-- MENU5:END -->

**UI-6.29 (D-110): menu Kho còn 5 mục** – Nhận hàng · Xuất kho sản xuất · Tồn kho · Kiểm kê · Kho & vị trí. Các mục cũ đã ẩn và mở đúng chỗ: Lô hàng & hạn dùng (`scm-lot`) → Tồn kho tab **Theo lô & hạn dùng**; Sổ kho (`scm-inv-txn`) → Tồn kho tab **Sổ kho**; Định mức tồn kho (`scm-inv-policy`) → Tồn kho tab **Định mức theo kho**; Khu bảo quản / Vị trí kệ → Kho & vị trí tab tương ứng; Giữ chỗ nguyên liệu → Xuất kho sản xuất tab **Giữ chỗ nguyên liệu**; Chênh lệch tiêu hao → Giá vốn & lãi gộp tab Theo món / Bảng sản xuất ngày. Quyền ở mục cũ được gộp vào mục cha (không ai mất quyền xem). Chuyển kho / Điều chỉnh tồn chỉ còn ở Tồn kho tab đầu (`scm-transfer`, `scm-adjustment` ẩn từ MENU-4). Các mục "Chi tiết phiếu nhập / phiếu xuất / kiểm kê" cũng đã bỏ: xem dòng trong chi tiết phiếu, hoặc **Lọc theo nguyên liệu** trên danh sách Phiếu nhập / Phiếu xuất.

## 3. Việc trong ngày

| Giờ | Việc | Màn |
|---|---|---|
| 00:15 | Hệ thống tự chuyển lô quá hạn → **Hết hạn** + ghi hủy tồn | tự động |
| Sáng | Xem cận date / dưới tồn tối thiểu (chuông, bot) | Tồn kho (tab Theo nguyên liệu / Theo lô) |
| Khi hàng về | **Nhận hàng & kiểm tra** trên một form (tự đạt → nhập kho ngay; cần QA → Gửi QA) | `scm-gr` |
| Trước giờ nấu | **Xuất kho** phần nguyên liệu đã giữ chỗ cho lệnh SX | `scm-issue` |
| Khi hàng về sau | **Giữ chỗ bổ sung** cho lệnh còn thiếu | Xuất kho sản xuất › tab Giữ chỗ nguyên liệu |
| Định kỳ | **Kiểm kê** | `scm-stockcount` |
| 21:00 | Bot gửi nguyên liệu cần cho ngày mai | bot |

## 4. Thao tác từng bước

### 4.1 Nhận hàng & kiểm tra – `scm-gr` (UI-6.29)
1. Đầu màn **Nhận hàng**: khối **Đơn mua hẹn giao hôm nay + quá hẹn chưa nhận đủ** (◀ ▶ hoặc chọn ngày; nhãn đỏ **Quá hẹn**). Đơn khác: gõ số đơn mua ở ô **Đơn mua khác** → **Nhận hàng**. Nút **Nhận hàng** ở màn Đơn mua mở **cùng form**. Màn không còn nút "Tạo mới" chung.
2. Form **Nhận hàng & kiểm tra – PO…**: chọn **Kho nhận**; mỗi dòng còn chờ nhận: **Số nhận** (mặc định = còn lại; 0 = không nhận lần này), **Lô NCC**, **Hạn dùng** (trống = ngày nhận + hạn bảo quản), **Nhiệt độ °C** (bắt buộc với hàng Mát / Đông lạnh – dưới ô hiện vùng *Đạt nhiệt độ / Nhận có điều kiện / Vượt ngưỡng – loại* và ngưỡng), **Cảm quan**, **Bao bì** (mặc định Đạt – chỉ đổi khi có vấn đề), **Kết quả** (tự điền theo gợi ý: vùng loại / quá hạn dùng / cảm quan, bao bì không đạt → Không đạt), **Số đạt** (khi Đạt một phần), **Nhận xét** (bắt buộc khi không Đạt). Hàng đông lạnh nhận có điều kiện → tick **Không có dấu hiệu rã đông**.
3. Nút cuối form:
   - Người có quyền Duyệt "Kiểm tra hàng nhập" (QA): **Kết luận & nhập kho** – tạo phiếu, kết luận và tự ghi sổ (lập lô, nhập phần đạt, cập nhật đơn mua) một lần.
   - Thủ kho: **Gửi QA** – phiếu ở **Chờ QC**, vào **Phê duyệt › Kiểm tra hàng nhập** để QA xác nhận (QA mở cùng form đã điền sẵn, không nhập lại).
   - **Mọi dòng tự đạt** (nguyên liệu không yêu cầu kiểm tra + nhiệt độ đạt / không kiểm): hệ thống **nhập kho ngay** – *Mọi dòng tự đạt – hệ thống đã nhập kho*.
- Nhận vượt tối đa 10% số đặt mỗi dòng: *… nhận … vượt số còn được nhận …*. Dòng không đạt → hệ thống tự lập **Sự cố nhà cung cấp**.

### 4.2 Danh sách phiếu nhận – `scm-gr`
| Nút | Khi nào | Kết quả |
|---|---|---|
| **Kiểm tra & kết luận** | Chờ QC – chỉ QA | Mở form nhận hàng đã điền sẵn để kết luận |
| **Ghi sổ** | Đạt / Đạt một phần / Từ chối (tự ghi sổ lỗi) | Ghi sổ tay – tạo **lô** `LO-…`, nhập kho **phần đạt**, cập nhật PO |
| **In** | Đạt / Đạt một phần / Từ chối / Đã ghi sổ | Phiếu nhập kho PDF |
| **Hủy** | Chưa ghi sổ, chưa hủy | Bắt buộc lý do |

### 4.3 Tồn kho – `scm-inv-balance` (4 tab: Theo nguyên liệu · Theo lô & hạn dùng · Sổ kho · Định mức theo kho)
1. Lọc **Kho** (Tất cả kho) và tick **Chỉ nguyên liệu dưới mức tồn**.
2. Thẻ: Nguyên liệu có tồn, **Dưới tồn tối thiểu**, **Dưới điểm đặt hàng**, **Lô sắp hết hạn** (+ số lô đã hết hạn).
3. Bảng: Mã, Nguyên liệu, Kho, Tồn thực, Đã giữ, **Xuất được** (+ "bị chặn"), ĐVT, Tối thiểu / an toàn / đặt hàng, **Mức tồn** (Dưới tối thiểu / Cần đặt hàng / Đủ hàng), Số lô, Sắp hết hạn (+ ngày hết hạn gần nhất).
4. Bấm một dòng → **Tồn theo lô** (sắp theo thứ tự nên xuất trước, nhãn **Xuất trước**, lý do bị chặn, còn / quá hạn N ngày) với nút **Chuyển** / **Điều chỉnh** trên từng lô.
5. **Tính lại tồn** (chỉ quản trị): tính lại số tồn từ sổ cái, ghi đè số hiện tại.

### 4.4 Chuyển kho / vị trí – nút **Chuyển kho**
1. **Chuyển kho** (Tồn kho tab Theo nguyên liệu, hoặc trên một lô trong hộp Tồn theo lô).
2. Chọn **Nguyên liệu***, Lô, **Từ kho*** + vị trí, **Đến kho*** + vị trí, **Số lượng chuyển***, **Lý do*** (≥ 3 ký tự) → **Chuyển kho** → xác nhận.
- *Nơi chuyển đến phải khác nơi chuyển đi*, *Không đủ tồn khả dụng của …: còn …, cần …*.

### 4.5 Điều chỉnh tồn – nút **Điều chỉnh tồn**
1. Chọn Nguyên liệu*, Lô, Kho*, Vị trí, **Số lượng điều chỉnh** (+ tăng / − giảm, khác 0), Đơn giá (khi nhập thêm), **Lý do***, Ghi chú → **Ghi điều chỉnh** → xác nhận.
2. Mặc định điều chỉnh tay **phải được duyệt** ("Phê duyệt › Phiếu điều chỉnh tồn") trước khi ghi sổ: *Đã gửi duyệt điều chỉnh tồn (yêu cầu …) – chỉ ghi sổ khi được duyệt*.

### 4.6 Lô nguyên liệu – Tồn kho › tab **Theo lô & hạn dùng** (link cũ `#/scm-lot` mở tab này)
- Chip lọc: **Tất cả / Còn tồn / Hết hạn trong 3 ngày / Trong 7 ngày / Trong 30 ngày**.
- Bấm một dòng → **trang chi tiết lô** (mục 4.12).
- Cột: Số lô, Nguyên liệu, NCC, Ngày nhận, HSD (đỏ nếu sắp hết), Tồn, Khả dụng, QC, Trạng thái.
- Nút trên dòng:
  - **Truy xuất** → chuỗi *Nhà cung cấp → Đơn mua → Phiếu nhập → Lô → Phiếu xuất → Lệnh SX → Mẻ (+ "truy ngược mẻ") → Nhiệt độ → Lệnh đóng gói → Đơn hàng → Đơn giao → Khách hàng*.
  - **Tạm giữ** (lô Khả dụng): bắt buộc lý do – lô tạm giữ không được gợi ý, không xuất được.
  - **Mở giữ** (lô Tạm giữ): lô trở lại khả dụng.
- **Xử lý lô hết hạn** (đầu trang): xem trước danh sách lô quá hạn (+ lô sắp hết hạn), giá trị hủy ước tính → **Chuyển hết hạn và hủy tồn N lô** → ghi chú → xác nhận. Lô đang tạm giữ được bỏ qua (xử lý riêng).

### 4.7 Giữ chỗ nguyên liệu – Xuất kho sản xuất › tab **Giữ chỗ nguyên liệu** (thủ kho bấm được **Giữ chỗ bổ sung**)
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

### 4.10 Chênh lệch tiêu hao (mục menu đã ẩn – xem ở Giá vốn & lãi gộp tab Theo món, Bảng sản xuất ngày)
Màn cũ (link `#/scm-consumption-var` mở Giá vốn & lãi gộp): chọn khoảng ngày, tick **Chỉ dòng vượt ngưỡng cảnh báo** (> 5%) → **Tải lại**. Số liệu sinh khi **đóng lệnh sản xuất**.

### 4.11 Kho & vị trí – `scm-warehouse` (3 tab: Kho · Khu bảo quản · Vị trí kệ)
Tab **Kho**: nút **Sơ đồ** trên dòng kho → các khu bảo quản (ngưỡng nhiệt độ) và vị trí trong khu. Tab **Khu bảo quản** (nhiệt độ tối thiểu / tối đa), tab **Vị trí kệ** (thêm / sửa, nút **In QR vị trí kho**).

### 4.12 Trang chi tiết – phiếu nhập, lô (UI-6)
Bấm một dòng ở **Nhận hàng** hoặc tab **Theo lô & hạn dùng** → **trang chi tiết toàn màn** (đường dẫn riêng; Ctrl + bấm / chuột giữa → tab mới; **← Danh sách** giữ bộ lọc, trang, vị trí cuộn). Quyền xem = quyền xem màn danh sách; nút phần đầu là đúng các nút trên dòng, hiện theo trạng thái / quyền.
- **Phiếu nhập:** phần đầu số phiếu + trạng thái + nhãn kết luận QC, NCC, kho, ngày nhận, PO (bấm mở trang đơn mua); tiến trình **Chờ QC → QC kết luận (Đạt / Đạt một phần / Từ chối) → Đã ghi sổ** (Hủy = đỏ). Nút **Ghi sổ nhập kho**, **Mở phiếu QC** (khi Chờ QC), **In phiếu**; ⋯ **Hủy phiếu nhập**. 4 thẻ: Số dòng (số lô đã tạo) · Dòng bị loại · **Lệch nhiệt độ** (vùng có điều kiện / loại / thiếu số đo) · Giá trị đạt (giá trị nhận). Tab: Tổng quan (thông tin + dòng + **lô đã tạo** – bấm số lô mở trang lô) · Dòng & QC (nhiệt độ, lô NCC, HSD, kết quả, "Dùng trước") · Kiểm tra cảm quan · Tài liệu · Lịch sử.
- **Lô:** phần đầu số lô + trạng thái + QC + nhãn hạn (Đã hết hạn / Còn N ngày), nguyên liệu (bấm mở hồ sơ nguyên liệu), NCC, ngày nhận, HSD; khung vàng **Không xuất được** + lý do khi lô bị chặn. Nút **Truy xuất** (hộp truy xuất như ở danh sách), **Tạm giữ** / **Mở tạm giữ** (chỉ người có quyền Duyệt "Giữ / thả lô"). 4 thẻ: Tồn thực · Khả dụng (đã giữ) · Đã xuất (số nhập) · Hạn dùng còn bao nhiêu ngày. Tab: Tổng quan (nguồn: phiếu nhập, đơn mua, NSX, đơn giá nhập, số nhận / đạt; tồn theo kho; giao dịch gần nhất) · Giao dịch kho · **Truy xuất** (chuỗi NCC → … → khách hàng ngay trên trang) · Chứng từ liên quan · Lịch sử.

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

## 8. Trả hàng NCC (ACC-5, D-131 i) – `scm-supplier-return`

Trả lại NCC hàng **đã nhập kho** bị hỏng, sai quy cách, gần / quá hạn hoặc giao thừa. Hàng bị loại ngay lúc nhận thì ghi số loại ở phiếu nhận, không lập phiếu trả.

1. **Thủ kho** (Kho › Trả hàng NCC) → **Lập phiếu trả** → tìm lô (mã lô, nguyên liệu, NCC, số phiếu nhận) → chọn lô → nhập **Số trả** (không vượt tồn còn trả được) + **Lý do** (Hàng hỏng / biến chất · Sai quy cách · Gần / quá hạn dùng · Giao thừa) + ghi chú → **Lập phiếu trả**. Lô chuyển **Tạm giữ** (không xuất cho sản xuất), bot Vận hành báo Mua hàng.
2. **Mua hàng** hỏi NCC → mở phiếu → **NCC đồng ý** hoặc **NCC không nhận** (ghi lý do – lô bỏ tạm giữ).
3. NCC đến nhận hàng → **Thủ kho** mở phiếu → **Ghi sổ – NCC đã nhận hàng** → tồn kho giảm (sổ kho "Trả lại", giá lô). Kế toán thấy chứng từ **Trả lại hàng mua**.
4. Chưa ghi sổ thì **Hủy phiếu** được (lý do bắt buộc) – lô bỏ tạm giữ.

- Số phiếu dạng **TH-yyMMdd-xxxx**; trạng thái: Chờ NCC xác nhận → NCC đồng ý – chờ giao trả → Đã trả – đã ghi sổ (hoặc NCC không nhận / Đã hủy). Phiếu hiện cả ở Chất lượng › Sự cố NCC (loại "Trả hàng NCC") nhưng chỉ đổi trạng thái ở màn này.
- Ảnh hàng lỗi / biên bản: tải lên ở mục **Ảnh hàng lỗi / biên bản** trong chi tiết phiếu (Thủ kho, Mua hàng đều làm được).
- Mở nhanh: nút **Trả hàng NCC** ở trang chi tiết phiếu nhận (đã ghi sổ) hoặc trang lô → hộp lập phiếu đã lọc sẵn phiếu / lô đó.
- Mua hàng bấm thẳng **✅ NCC đồng ý / ⛔ NCC không nhận** trên tin bot Vận hành (không nhận → bot hỏi lý do).
- Lỗi thường gặp: "Số trả vượt tồn còn trả được của lô: tối đa …" (trừ phần đang chờ trả ở phiếu khác) · "Lô này không gắn nhà cung cấp" · "Kỳ kế toán tháng … đã khóa".

## 9. Phiếu xuất bao bì tự lập (ACC-6, D-131 j)

- Khi Bếp ghi kết quả đóng gói, hệ thống lập **phiếu xuất Nháp "Xuất bao bì"** theo ca = suất đạt × định mức (Đóng gói & Giao hàng › Định mức bao bì). Phiếu nằm ở **Kho › Xuất kho sản xuất**, ghi chú bắt đầu `[XUAT_BB:…]`.
- Thủ kho kiểm, **sửa số** nếu dùng thực tế khác, rồi Ghi sổ; không làm gì thì **23:30** job tự ghi sổ phiếu của ca đã hết giờ (lỗi thì bot báo Thủ kho).
- Vật tư thiếu tồn không đưa vào phiếu, bot báo Mua hàng lập đề nghị mua.

## 10. Nhận hàng theo nhà cung cấp (UI-6.50, D-132)

1. **Kho › Nhận hàng**: thẻ mỗi NCC hẹn giao hôm nay (số đơn, số dòng, giờ hẹn; quá hẹn tô đỏ). Xe NCC đến → bấm **Nhận hàng** trên thẻ NCC. Đơn khác ngày / NCC khác → gõ tên NCC, số đơn mua hoặc số hóa đơn NCC ở ô tìm → Enter.
2. Form gộp mọi đơn còn mở của NCC: bỏ tick đơn không giao hôm nay. Chọn **Kho nhận**, ghi **Số hóa đơn / phiếu giao NCC** (tùy chọn), **Nhiệt độ thùng xe** (một lần cho mọi hàng mát / đông; dòng nào đo riêng bấm "sửa").
3. Bấm **✓ Nhận đủ – Đạt tất cả** → mọi dòng = số còn phải nhận, kết quả Đạt; sửa dòng thiếu / hỏng (số nhận, kết quả, nhận xét bắt buộc khi không Đạt). Lô NCC / Hạn dùng chỉ hỏi với nguyên liệu quản lý lô / hạn; để trống = lô nội bộ / hạn tự tính; "↓ áp dòng dưới" chép cho các dòng dưới.
4. NCC gửi **Excel phiếu giao**: bấm **Mẫu** (gửi NCC điền) / **Tải lên** → form tự điền số giao, lô, hạn dùng; dòng không khớp hiện để nhập tay.
5. Bấm **Nhận hàng (N phiếu)** – hệ thống tách mỗi đơn mua một phiếu nhập (đơn cũ trước), tự đạt nhập kho ngay, cần QA gửi QA; QA đứng tại kho thấy **Kết luận & nhập kho (N phiếu)**.
- **Điện thoại:** dòng hiện dạng thẻ, nút to "Nhận đủ – Đạt tất cả". Web không dùng camera.
- **QR trên đơn mua in:** Mua hàng in đơn (PDF – nút "In đơn (PDF, có QR)" ở trang đơn mua) gửi NCC, đề nghị NCC in kèm khi giao. Thủ kho mở camera điện thoại quét QR → bot Vận hành mở nhận hàng của NCC đó → **✓ Nhận đủ – Đạt tất cả** (bot hỏi nhiệt độ thùng xe nếu có hàng mát / đông). Lệch số / lô → làm trên web. Hoặc trên bot: 📦 Nhận hàng theo NCC (`/nhanhang`).
- Nút **Phiếu giao NCC (Excel)** ở trang đơn mua: tải mẫu để thu mua gửi NCC.
