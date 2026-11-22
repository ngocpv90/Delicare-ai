# 02 – Kinh doanh, hợp đồng, đơn hàng

## 1. Bạn là ai

- Nhóm quyền: **SAL-SALES** (2.2 Bán hàng) và **SAL-CON** (2.3 Quản lý hợp đồng / đơn hàng). Cùng một bộ quyền.
- Bot dùng: **Vận hành** (phân hệ 💼 Kinh doanh). Trưởng phòng được cấp thêm bot **Điều hành**.
- Việc chính: hồ sơ khách hàng, hợp đồng + loại suất, dự báo, chính sách giá, **đơn hàng hằng ngày (chốt trước 20:00)**, đối soát suất.

## 2. Màn hình được dùng

| screen-id | Menu › Mục | Quyền |
|---|---|---|
| `crm-customer` | Kinh doanh & Đơn hàng › Khách hàng | Xem, sửa, xuất |
| `crm-contact` | … › Người liên hệ | Xem, sửa, xuất |
| `crm-location` | … › Điểm giao hàng | Xem, sửa, xuất |
| `crm-contract` | … › Hợp đồng | Xem, sửa, xuất |
| `crm-price-policy` | … › Chính sách giá | Xem, sửa, xuất |
| `crm-forecast` | … › Dự báo | Xem, sửa, xuất |
| `crm-order` | … › Đơn hàng | Xem, sửa, xuất |
| `crm-reconciliation` | … › Đối soát đơn hàng | Xem, sửa, xuất |
| `menu-menu`, `menu-assignment`, `menu-dish`, `menu-change-request` | Thực đơn & Món ăn | Chỉ xem |
| `log-*`, `prod-*`, `cost-*` | Đóng gói & Giao nhận, Sản xuất & Giá thành | Chỉ xem |
| `dash-exec`, `kpi-daily`, `kpi-monthly` | Tổng quan / KPI | Xem |

Mặc định nhóm KD **không có quyền duyệt** (lệch suất, sửa đơn sau giờ chốt, hợp đồng do Ban điều hành duyệt – xem `12_QUAN_LY_DUYET.md`).

## 3. Việc trong ngày

| Giờ | Việc | Màn |
|---|---|---|
| Đầu kỳ | Tạo / gia hạn hợp đồng, gửi duyệt | `crm-contract` |
| Hằng tuần | Nhập + công bố dự báo 1–2 tuần tới | `crm-forecast` |
| Hằng ngày, trước 19:00 | Tạo đơn ngày mai → Gửi → Xác nhận | `crm-order` |
| 19:00 | Bot nhắc "⏰ khách chưa chốt" – xử lý nốt | bot |
| **20:00** | Hệ thống tự **chốt** mọi đơn Đã xác nhận của ngày mai | tự động |
| Sau 20:00 | Thay đổi đơn ngày mai → xin duyệt (mục 4.6) | `crm-order` |
| Sau giao hàng | Ghi suất thực tế phục vụ, chốt đối soát | `crm-reconciliation` |

**Giờ chốt:** đơn cho ngày ăn D phải xong trước **20:00 ngày D-1**.

## 4. Thao tác từng bước

### 4.1 Khách hàng 360° – `crm-customer`
- **Thêm khách hàng (D-63, UI-5):**
  1. Bấm **Tạo mới** → form mở ra với 2 nút lớn **Doanh nghiệp** / **Cá nhân** (chưa chọn sẵn). Chọn loại → hiện các ô của loại đó. Trước khi lưu vẫn đổi loại được ở ô *Loại khách hàng*, dữ liệu đã nhập được giữ.
  2. Nhập các ô (bảng dưới). *Doanh nghiệp*: gõ **Tên doanh nghiệp** → **Tên giao dịch** tự điền giống vậy khi ô này còn trống (sửa lại được).
  3. Ô chọn: **Ngành**, **Quy mô**, **Giới tính**, **Nguồn** chỉ chọn trong danh mục; **Chi nhánh phục vụ** chỉ liệt kê đơn vị loại chi nhánh (có đúng 1 chi nhánh → chọn sẵn); **NV phụ trách** chọn trong tài khoản đang hoạt động – gõ vào ô tìm phía trên để lọc theo tên / tên đăng nhập; **Trạng thái** mặc định *Đang hoạt động*. Các ô ngày chỉ chọn ngày.
  4. (Tùy chọn) bấm **Thêm điểm giao ngay** → nhập **Điểm giao đầu tiên**: tên, địa chỉ (gợi ý sẵn địa chỉ khách), giờ nhận từ – đến.
  5. Bấm **Tạo mới**. Thành công → *"Đã tạo khách hàng – mã KHB-00013 · 1 liên hệ chính · 1 điểm giao DP-00013"*. Khách doanh nghiệp tự có **người liên hệ chính** (từ Người liên hệ + SĐT + email người liên hệ); điểm giao tạo cùng lúc. Lỗi bất kỳ (kể cả ở điểm giao) → không tạo gì, form giữ nguyên.

  | Ô | Doanh nghiệp | Cá nhân |
  |---|---|---|
  | Bắt buộc | Tên doanh nghiệp, Tên giao dịch, Mã số thuế, Người đại diện pháp luật, Người liên hệ, SĐT người liên hệ, Địa chỉ, Điện thoại | Họ, Tên, Địa chỉ, Điện thoại |
  | Bắt buộc khi công ty đã khai báo chi nhánh | Chi nhánh phục vụ | Chi nhánh phục vụ |
  | Tùy chọn | Số ĐKKD (trống = MST), Ngày / nơi cấp ĐKKD, Email, Email người liên hệ, Ngành, Quy mô, Phân khúc, Nguồn, NV phụ trách, Trạng thái, Ghi chú | Giới tính, Ngày sinh, Số CCCD + ngày / nơi cấp, Nghề nghiệp, Email, Mã số thuế cá nhân, Nguồn, NV phụ trách, Trạng thái, Ghi chú |

  - **Kiểm ngay khi rời ô** (lỗi đỏ dưới đúng ô, giống hệt máy chủ): Mã số thuế 10 số hoặc 10-3 số (cá nhân: 10 số); CCCD đúng 12 số; điện thoại `0` + 9 số hoặc `+84` + 9 số (được gõ dấu cách / chấm / gạch); email đúng dạng; ngày sinh / ngày cấp không sau hôm nay; ngày cấp CCCD từ ngày sinh trở đi; giờ nhận từ < giờ nhận đến. Lỗi máy chủ (vd. MST trùng) hiện ở đầu form và tô đỏ ô liên quan.
  - Mã `KHB-…` / `KHC-…` máy chủ tự sinh. Dữ liệu cũ có Ngành / Nguồn… ngoài danh mục: form hiện "(giá trị cũ)", lưu lại không đổi thì giữ; muốn đổi thì chọn trong danh mục.
- **Sửa khách hàng:** nút **Sửa** – cùng form, loại khách giữ nguyên (không có bước chọn loại, không có khối điểm giao – thêm điểm giao ở `crm-location` / tab Điểm giao).
  - Loại khách **khóa sau khi lưu**; khách chưa có hợp đồng / đơn mới đổi loại được (cấp mã mới). Khách đã có hợp đồng / đơn → ngừng hoạt động và tạo khách mới.
- Danh sách: Mã, Khách hàng, Loại, Chi nhánh phục vụ, Hợp đồng hiệu lực, số điểm giao, Suất TB/ngày, Trạng thái. Lọc theo **Đơn vị làm việc**.
- Đầu danh sách: 4 thẻ **Tổng khách hàng**, **Đang hoạt động** (kèm "/ tổng · %"), **Hợp đồng hiệu lực** (bấm mở danh sách hợp đồng), **Suất TB/ngày (7 ngày)** = tổng suất đơn Đã giao / Đã đối soát 7 ngày đã qua ÷ 7 – tính theo đơn vị làm việc.
- **Bấm một dòng → màn Hồ sơ khách hàng 360 (cả trang)** – ← về danh sách: phần đầu (chữ viết tắt, tên, mã, loại, trạng thái, ngành, chi nhánh, NV phụ trách, địa chỉ, điện thoại, MST / CCCD) + nút **Sửa**, **Tạo đơn**, **Ghi phản ánh**, ⋯ (**Tạo hợp đồng**, **In hồ sơ**, **Ngừng hoạt động**) theo quyền; 4 thẻ chỉ số; các tab **Tổng quan** (khối *Cần chú ý*: hợp đồng còn ≤ 30 ngày, phản ánh quá hạn, khay thất thoát vượt ngưỡng, chưa chốt đơn ngày mai sau 19:00, thiếu điểm giao / liên hệ chính · Thông tin khách hàng · Điểm giao và liên hệ · Hợp đồng hiện hành · biểu đồ Suất đặt – giao – nhận 30 ngày · Đơn hàng gần đây), **Hợp đồng và giá**, **Đơn hàng** (lọc ngày ăn từ – đến, trạng thái), **Giao nhận**, **Đối soát**, **Khay hộp**, **Phản ánh**, **Dự báo và thực đơn**, **Tài liệu** (tải lên ≤ 20 MB), **Lịch sử** (+ Liên quan). Tab chỉ hiện khi tài khoản có quyền xem màn tương ứng; bấm dòng trong tab để mở chứng từ.
- **Tab Tổng quan (UI-2R, D-69):** hai cột – **trái 1/3 "Thông tin khách hàng"**: người liên hệ chính (+ chức vụ) · điện thoại liên hệ + tổng đài · email · địa chỉ · NV phụ trách · chi nhánh phục vụ; nhóm **Pháp lý** (mở sẵn; khách cá nhân: ngày sinh / CCCD chỉ người có quyền Sửa hồ sơ khách mới thấy – có biểu tượng khóa), **Phân loại**, **Ghi chú**, **Hệ thống** (tạo / cập nhật lúc, bởi). **Phải 2/3**: Cần chú ý → **Điểm giao và liên hệ** (suất/ngày từng điểm = TB 30 ngày; nút + Điểm giao, + Liên hệ) cạnh **Hợp đồng hiện hành** (còn N ngày, điều khoản thanh toán, đơn giá theo loại suất, Xem / Gia hạn) → **Suất đặt – giao – nhận 30 ngày** (kèm chênh đặt – nhận trung bình) → **Đơn hàng gần đây** (số đơn, ngày ăn, suất, thành tiền, trạng thái; lọc trạng thái, Xem tất cả).
  - **Khối thông tin bên trái (UI-2R, dùng chung 3 màn chi tiết):** 6 dòng chính luôn hiện (điện thoại / email bấm để gọi / gửi thư); bên dưới là các **nhóm gập** – bấm tiêu đề nhóm (hoặc Tab + Enter) để mở / gập, dòng xám dưới tiêu đề là tóm tắt; trình duyệt **nhớ nhóm đang mở** cho lần sau. Ô trống không hiện; nhóm chưa có gì ghi "Chưa có thông tin · Bổ sung" (bấm Bổ sung = Sửa, chỉ khi có quyền sửa). Nút sao chép cạnh MST / CCCD. Màn rộng: khối này đứng yên khi cuộn; màn < 1200px: xếp trên cùng.
- Mở khách từ màn khác (liên kết, tìm kiếm) → ngăn xem nhanh **360°** + nút **Mở hồ sơ đầy đủ**: thẻ **Hợp đồng hiệu lực** / **Suất TB/ngày (7 ngày)** / **Doanh thu đã đối soát** (kèm số đơn) / **Khiếu nại đang mở** (kèm "/ tổng · % đúng hạn"); tab **Liên hệ & điểm giao**, **Hợp đồng & giá**, **Đơn hàng** (15 đơn gần nhất), **Liên quan**, **Lịch sử xử lý**.
- Thêm / sửa khách bằng **Tạo mới** / **Sửa**.

### 4.2 Người liên hệ, điểm giao – `crm-contact`, `crm-location`
Danh sách chung (xem `00_CHUNG.md` mục 3). Điểm giao cần: tên, địa chỉ, **giờ nhận từ – đến**, người nhận + số điện thoại, tọa độ (để chấm "đã đến" trên bot).
- *Giờ giao từ phải nhỏ hơn giờ giao đến*; *Vĩ độ phải trong [-90, 90]*; *Kinh độ phải trong [-180, 180]*.
- *Còn đơn giao chưa hoàn tất tại điểm này – không ngừng hoạt động được*.

### 4.3 Hợp đồng – `crm-contract`
**Tạo hợp đồng**
1. **Tạo mới** → hộp **Tạo hợp đồng**.
2. Nhập: **Khách hàng***, **Tên hợp đồng***, Số hợp đồng (máy chủ tự sinh `HD-…`; số hợp đồng giấy của khách ghi ở Ghi chú), Ngày ký, **Hiệu lực từ***, Hiệu lực đến, Điều khoản thanh toán, Giá trị hợp đồng, Ghi chú.
3. Bảng **Loại suất & đơn giá***: mỗi dòng **Loại suất** (gợi ý mã có sẵn, vd. SANG / TRUA / TOI / DEM), Tên suất, Đơn giá. **Thêm loại suất** để thêm dòng, 🗑 để bỏ dòng.
4. **Tạo hợp đồng** → hợp đồng ở **Nháp**.
- Lỗi tại chỗ: *Chọn khách hàng*, *Nhập tên hợp đồng*, *Nhập ngày bắt đầu hiệu lực*, *Ngày hết hiệu lực phải sau ngày bắt đầu*, *Nhập ít nhất 1 loại suất*, *Mỗi dòng phải có mã loại suất*, *Mỗi loại suất chỉ được xuất hiện một lần*, *Đơn giá phải lớn hơn 0*.

**Gửi duyệt:** dòng Nháp có nút **Gửi duyệt** → "Đã gửi duyệt hợp đồng …" → trạng thái **Chờ duyệt**. Duyệt xong → **Hiệu lực**. Bị từ chối → về **Nháp**, sửa rồi gửi lại.

**Xem chi tiết:** tab **Loại suất & đơn giá** (khách, trạng thái, yêu cầu duyệt đang chờ, số đơn đang mở, cảnh báo). Chỉ **Chỉnh sửa** được khi Nháp.

**Thanh lý:** dòng Hiệu lực có **Thanh lý** → nhập **Lý do thanh lý*** → **Thanh lý** → **Chấm dứt**.
- *Còn … đơn hàng chưa giao có ngày ăn sau …: … – hủy / giao xong trước khi chấm dứt*.

**Quét hợp đồng hết hạn:** nút đầu trang → hệ thống liệt kê hợp đồng Hiệu lực đã quá ngày hết hiệu lực → **Chuyển sang Hết hạn** (không hoàn tác). Job 00:30 cũng tự làm việc này.

### 4.4 Dự báo – `crm-forecast`
**Xem ma trận:** chọn **Từ – đến** (tối đa 62 ngày) → bảng khách × ngày. Số chính = đã công bố; chữ nhỏ "nháp N" = đã nhập chưa công bố.

**Nhập dự báo**
1. **Nhập dự báo** (cần quyền Nhập).
2. Chọn **Khách hàng***, Điểm giao (trống = chung cho khách), **Loại suất***, Ca (trống = mọi ca), **Từ ngày* – Đến ngày*** (≤ 62 ngày).
3. Nhập số suất từng ngày, hoặc gõ vào **Điền nhanh số suất mỗi ngày** để áp cho mọi ngày. Ngày để trống không được nhập.
4. **Nhập dự báo** → tạo phiên bản mới ở **Nháp**.

**Công bố:** **Công bố** → chọn Từ – Đến, Khách hàng (trống = tất cả) → **Công bố**. Bản Nháp → **Đã công bố**; bản cũ cùng khách / điểm giao / ngày / ca / loại suất → **Bị thay thế**. Không hoàn tác.
- Dự báo đã công bố được dùng làm **số gợi ý** khi tạo đơn và để cảnh báo lệch khi xác nhận đơn (> 10%).

### 4.5 Đơn hàng – `crm-order`
**Xem đơn**
- Chip **Ngày ăn** (mặc định ngày mai), **Tất cả** để xem mọi ngày; thanh trạng thái đếm số đơn mỗi bước.
- **Dạng Kanban** (mặc định): 8 cột theo trạng thái, mỗi thẻ = số đơn, khách, điểm giao, ngày, số suất. **Dạng bảng**: lọc theo trạng thái.

**Tạo đơn**
1. **Tạo đơn hàng** → chọn **Khách hàng***, **Ngày ăn*** (mặc định hôm nay + 4).
2. Hệ thống tải mẫu: hợp đồng hiệu lực, **hạn chốt đơn**, mỗi **điểm giao** một bảng: Loại suất, Ca, Đơn giá, Dự báo, **Số lượng** (điền sẵn = dự báo).
   - Điểm giao đã có đơn cho ngày này → báo *"Điểm giao này đã có đơn … – mở đơn đó để sửa số lượng"*.
3. Sửa số lượng → **Tạo đơn (Nháp)**. Mỗi điểm giao có số > 0 thành **1 đơn** riêng (số `DH-…`).
4. Nếu **đã quá giờ chốt**: hộp báo vàng + ô **Lý do thêm đơn sau giờ chốt*** → đơn tạo ở Nháp và **tự gửi duyệt**; duyệt xong đơn chuyển **Đã chốt**.

**Xử lý đơn (bấm vào thẻ / dòng)** – ngăn chi tiết:
- Đầu ngăn: thanh tiến trình trạng thái; hộp thông tin: **Hạn chốt đơn**, "đã quá giờ chốt", **Đang chờ duyệt** (lệch suất / thay đổi sau giờ chốt), dự báo + % lệch.
- Bảng dòng: Ca, Loại suất, Điểm giao, **Số lượng** (ô sửa được khi cho phép), Dự báo, Đơn giá, Thành tiền.
- Nút (chỉ hiện nút máy chủ cho phép):

| Nút | Khi nào | Kết quả |
|---|---|---|
| **Lưu số lượng** | Nháp / Đã gửi / Đã xác nhận, trước giờ chốt, không có yêu cầu duyệt đang chờ | Cập nhật số suất, tính lại tiền |
| **Gửi đơn (Submitted)** | Nháp, trước giờ chốt | → Đã gửi (đơn phải có ít nhất 1 dòng có Ca + Loại suất) |
| **Xác nhận (Confirmed)** | Đã gửi, trước giờ chốt | Hộp xác nhận hiện % lệch so với dự báo → Đã xác nhận |
| **Chốt số lượng (Locked)** | Đã xác nhận | → Đã chốt (không cần đợi job 20:00) |
| **Hủy đơn** | Nháp / Đã gửi (mọi lúc); Đã xác nhận (trước giờ chốt) | Bắt buộc **Lý do hủy** → Hủy |

- **Lệch suất lớn:** sửa số lượng của đơn **Đã xác nhận** mà lệch > 30% → hệ thống **không ghi ngay** mà gửi duyệt *"Số suất lệch …% (> 30%) … đã gửi duyệt …, số mới chỉ ghi khi được duyệt"*.
- Tab **Hành trình**: các chặng đơn → kế hoạch → sản xuất → đóng gói → giao → đối soát → phản ánh, kèm chứng từ từng chặng.

- **Chốt đơn theo ngày** (đầu màn `crm-order`, cần quyền Sửa): chọn ngày ăn → **Chốt đơn** → xác nhận → khóa mọi đơn **Đã xác nhận** của ngày (chỉ khi đã tới giờ chốt 20:00 hôm trước; bình thường job 20:00 tự chốt). Màn báo số đơn đã chốt và số đơn còn chưa xác nhận / đang chờ duyệt.

### 4.6 Thay đổi đơn sau giờ chốt
- Trên web: sau giờ chốt (20:00 D-1), ngăn chi tiết đơn có nút **Yêu cầu sửa sau chốt** (chỉ hiện khi đã quá giờ chốt): nhập số suất mới từng dòng + **Lý do*** → **Gửi yêu cầu** → yêu cầu duyệt ORDER_CHANGE_AFTER_CUTOFF (số `XN-…`) chờ CEO duyệt; duyệt xong số lượng mới mới được ghi.
- Cách làm hiện có:
  - **Thêm đơn mới** sau giờ chốt: dùng **Tạo đơn hàng** + lý do (mục 4.5 bước 4).
  - **Sửa số suất** đơn đã chốt: báo Ban điều hành / IT (chờ bổ sung nút).
  - **Đổi món**: bộ phận Kế hoạch tạo **Yêu cầu đổi món** (`03_KE_HOACH.md`).

### 4.7 Chính sách giá – `crm-price-policy`
- Danh sách chính sách giá theo khách / hợp đồng / loại suất / thời gian hiệu lực (thêm / sửa bằng form chung).
- **Tra đơn giá áp dụng**: chọn **Khách hàng***, Hợp đồng, Loại suất (trống = mọi loại), Ngày áp dụng → **Tra giá** → đơn giá + nguồn (*Chính sách giá theo hợp đồng* → *theo khách hàng* → *Đơn giá loại suất trong hợp đồng*).
- Lỗi: *Chồng thời gian hiệu lực với chính sách giá khác…*, *Đơn giá phải lớn hơn 0*, *Không tìm thấy đơn giá áp dụng…*.

### 4.8 Đối soát suất – `crm-reconciliation`
Cùng thao tác với Điểm phát – xem `10_DIEM_PHAT.md` mục 4.2.

## 5. Trạng thái chứng từ

| Chứng từ | Luồng | Ai chuyển |
|---|---|---|
| Hợp đồng | Nháp → Chờ duyệt → Hiệu lực → Hết hạn (job 00:30 / Quét) / Chấm dứt (Thanh lý) | KD gửi duyệt, Ban điều hành duyệt |
| Dự báo | Nháp → Đã công bố → Bị thay thế | KD |
| Đơn hàng | Nháp → Đã gửi → Đã xác nhận → Đã chốt → Đang sản xuất → Đã giao → Đã đối soát; Hủy | KD tới Đã chốt (hoặc job 20:00); Đang sản xuất khi phát hành kế hoạch SX; Đã giao khi bàn giao xong; Đã đối soát khi đối soát |
| Đối soát | Mở → Khớp / Chênh lệch → Đóng | KD / Điểm phát |

## 6. Lỗi / cảnh báo thường gặp

| Thông báo | Nguyên nhân → cách xử lý |
|---|---|
| Khách hàng không có hợp đồng đang hiệu lực tại ngày … | Hợp đồng chưa duyệt / hết hạn → gửi duyệt hoặc gia hạn hợp đồng |
| Chọn khách hàng có hợp đồng hiệu lực | Như trên |
| Đã quá giờ chốt đơn (…) cho ngày ăn … – không … được | Sau 20:00 D-1 → xem mục 4.6 |
| Đã có đơn … cho khách / điểm giao / ngày ăn này – sửa đơn đó thay vì tạo mới | Mở đơn cũ để sửa |
| Đơn phải có ít nhất 1 dòng số suất lớn hơn 0 / Nhập ít nhất 1 dòng số lượng > 0 | Nhập số suất |
| Không xác định được ca giao cho loại suất … – chọn ca | Loại suất chưa gắn ca mặc định → báo IT / chọn ca |
| Đơn đang có yêu cầu duyệt … – chờ duyệt / từ chối trước khi sửa tiếp | Đợi người duyệt; hoặc người gửi **Hủy yêu cầu** ở hộp duyệt |
| Lệch dự báo …% … vượt ngưỡng 10% – đã ghi nhận | Chỉ cảnh báo, không chặn |
| Hủy đơn phải ghi lý do | Nhập lý do |
| Chỉ sửa hợp đồng / loại suất khi hợp đồng ở trạng thái Nháp | Hợp đồng đã duyệt chỉ Thanh lý; muốn đổi giá → chính sách giá hoặc hợp đồng mới |
| Số hợp đồng … đã tồn tại | Để trống ô số để tự sinh |
| Hợp đồng đã hết hạn (…) – không duyệt được | Sửa ngày hiệu lực rồi gửi lại |
| Khoảng ngày không hợp lệ (tối đa 62 ngày) | Thu hẹp khoảng ngày dự báo |
| Ngành "…" không có trong danh mục – chọn một trong: … (tương tự Quy mô / Giới tính / Nguồn) | Chọn giá trị trong ô chọn |
| Không tìm thấy nhân viên phụ trách (tài khoản người dùng đang hoạt động) | Tài khoản đã khóa / xóa → chọn người khác |
| Điểm giao đầu tiên: thiếu tên điểm giao / địa chỉ · giờ nhận từ phải nhỏ hơn giờ nhận đến | Sửa khối Điểm giao đầu tiên hoặc bấm **Bỏ điểm giao** |
| Mã số thuế … đã có ở khách hàng doanh nghiệp KHB-… | Khách đã có – mở khách đó thay vì tạo mới |

## 7. Trên bot Telegram

Phân hệ **💼 Kinh doanh** (bot Vận hành):
- 📦 **Đơn ngày mai** (`/donmai`) – đơn ngày mai theo trạng thái (khách mình phụ trách).
- ⏰ **Khách chưa chốt** (`/chuachot`) – đơn ngày mai chưa xác nhận.
- Thông báo: 🔴 19:00 còn khách chưa chốt; kết quả duyệt đơn / hợp đồng mình gửi.
Tạo / sửa đơn chỉ làm trên web.
