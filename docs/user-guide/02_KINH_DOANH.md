# 02 – Kinh doanh, hợp đồng, đơn hàng

## 1. Bạn là ai

- Nhóm quyền: **SAL-SALES** (2.2 Bán hàng) và **SAL-CON** (2.3 Quản lý hợp đồng / đơn hàng). Cùng một bộ quyền.
- Bot dùng: **Vận hành** (phân hệ 💼 Kinh doanh). Trưởng phòng được cấp thêm bot **Điều hành**.
- Việc chính: hồ sơ khách hàng, hợp đồng + loại suất (điều chỉnh giá bằng phụ lục trong hợp đồng), dự báo, **đơn hàng hằng ngày (chốt trước 20:00)**, đối soát suất.

## 2. Màn hình được dùng

<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->
Quyền theo ma trận D-79 (đủ 19 nhóm: `docs/claude/MENU5_MATRIX.md`).

| Menu › Mục | screen-id | SAL-SALES | SAL-CON | SAL-HEAD | SAL-CS |
|---|---|---|---|---|---|
| Việc của tôi › Việc của tôi | `hr-my` | X | X | X | X |
| Phê duyệt › Đơn đặt suất (sửa sau chốt) | `wf-inbox?type=crm-order` | — | — | Duyệt | — |
| Điều hành › Bảng điều hành | `dash-exec` | — | — | X | — |
| Điều hành › Chỉ số theo ngày | `kpi-daily` | — | — | X | — |
| Điều hành › Chỉ số theo tháng | `kpi-monthly` | — | — | X | — |
| Kinh doanh › Khách hàng | `crm-customer` | S | Q (IX) | Q (IX) | X |
| Kinh doanh › Hợp đồng | `crm-contract` | S | Q (IX) | Q (IX) | — |
| Kinh doanh › Đăng ký suất tuần | `crm-order-week` | S | Q (IX) | Q (IX) | — |
| Kinh doanh › Đơn hàng theo ngày | `crm-order` | S | Q (IX) | Q (IX) | X |
| Kinh doanh › Dự báo suất ăn | `crm-forecast` | S | Q (IX) | Q (IX) | — |
| Kinh doanh › Đối soát suất ăn | `crm-reconciliation` | X | S | S | X |
| Kinh doanh › Bảng kê thanh toán | `crm-billing` | X (IX) | X (IX) | X (IX) | — |
| Kinh doanh › Phản ánh khách hàng | `log-complaint` | X | X | S | S |
| Kinh doanh › Điểm giao hàng | `crm-location` | S | Q (IX) | Q (IX) | X |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | X | X | X | X |
| Thực đơn & Món ăn › Món ăn | `menu-dish` | X | X | X | X |
| Giá thành › Giá thành chuẩn | `cost-standard` | — | — | X | — |
| Giá thành › Giá vốn suất ăn | `cost-meal` | — | — | X | — |
| Đóng gói & Giao hàng › Đơn giao hàng | `log-delivery-order` | X | X | X | — |
| Đóng gói & Giao hàng › Theo dõi chuyến giao | `log-execution` | X | X | X | X |
| Đóng gói & Giao hàng › Xác nhận đã giao | `log-pod` | X | X | X | X |
| Đóng gói & Giao hàng › Khay hộp theo khách | `log-packaging-ledger` | X | X | X | — |
<!-- MENU5:END -->

Mặc định nhóm KD **không có quyền duyệt** (lệch suất, sửa đơn sau giờ chốt, hợp đồng do Ban điều hành duyệt – xem `12_QUAN_LY_DUYET.md`).

## 3. Việc trong ngày

| Giờ | Việc | Màn |
|---|---|---|
| Đầu kỳ | Tạo / gia hạn hợp đồng, gửi duyệt | `crm-contract` |
| Thứ Năm – trước 16:00 thứ Sáu | Xem gợi ý dự báo tuần sau, sửa ô cần, Công bố (quá 16:00 thứ Sáu hệ thống tự công bố) | `crm-forecast` |
| Hằng ngày, trước 19:00 | Tạo đơn ngày mai → Gửi → Xác nhận | `crm-order` |
| 19:00 | Bot nhắc "⏰ khách chưa chốt" – xử lý nốt | bot |
| **20:00** | Hệ thống tự **chốt** mọi đơn Đã xác nhận của ngày mai | tự động |
| Sau 20:00 | Thay đổi đơn ngày mai → xin duyệt (mục 4.6) | `crm-order` |
| Sau giao hàng | Xác nhận dòng khớp (1 nút), xử lý dòng chênh lệch, chốt đối soát | `crm-reconciliation` |

**Giờ chốt:** đơn cho ngày ăn D phải xong trước **20:00 ngày D-1**.

## 4. Thao tác từng bước

### 4.1 Khách hàng 360° – `crm-customer`
**Điều khoản (BIZ-3.6):** form khách hàng có ô **Ngưỡng thất thoát khay (%)** – trống = theo cấu hình chung (3 %); vượt ngưỡng → cảnh báo thất thoát khay và tính phí trên bảng kê (nếu có phí / khay). **Cách tính tiền theo hợp đồng (D-78):** khi tạo hợp đồng chọn *Cách tính tiền đối soát* (Theo cấu hình chung / Theo số khách nhận / Theo suất ăn thực tế / Theo số đặt – chốt); chi tiết hợp đồng có nút **Đổi** (quyền Sửa) – áp cho các lần đối soát sau.
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
  | Tùy chọn | Số ĐKKD (trống = MST), Ngày / nơi cấp ĐKKD, Email, Email người liên hệ, Ngành, Quy mô, Nguồn, NV phụ trách, Trạng thái, Ghi chú | Giới tính, Ngày sinh, Số CCCD + ngày / nơi cấp, Nghề nghiệp, Email, Mã số thuế cá nhân, Nguồn, NV phụ trách, Trạng thái, Ghi chú |

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
- **Người nhận** (khách doanh nghiệp): **chọn trong danh sách người liên hệ của khách** (người liên hệ đang hoạt động, người liên hệ chính xếp đầu); chọn xong SĐT tự điền theo người liên hệ. Chưa có người cần chọn → thêm ở **Người liên hệ** trước. Điểm giao cũ có người nhận ngoài danh sách hiện "(giá trị cũ)" – vẫn sửa được trường khác; đổi người nhận thì phải chọn trong danh sách. Nhập Excel cũng kiểm như vậy. Khách cá nhân: nhập tự do.
- *Chọn người nhận trong danh sách người liên hệ của khách*; *Người nhận «…» không có trong danh sách người liên hệ của khách – thêm vào Người liên hệ trước*; *SĐT người nhận không khớp người liên hệ «…»*.
- *Giờ giao từ phải nhỏ hơn giờ giao đến*; *Vĩ độ phải trong [-90, 90]*; *Kinh độ phải trong [-180, 180]*.
- *Còn đơn giao chưa hoàn tất tại điểm này – không ngừng hoạt động được*.

**Trang chi tiết điểm giao (UI-6):** bấm 1 dòng ở **Điểm giao hàng** (hoặc tên điểm giao trong trang đơn hàng) → mở **trang riêng** `#/crm-location-detail?id=…` (Ctrl / chuột giữa = tab mới; **← Điểm giao hàng** quay lại đúng bộ lọc, trang, vị trí cuộn). Phần đầu: tên, mã, khách, địa chỉ, giờ nhận, người nhận; nút **Sửa** (cần quyền Sửa), **Bản đồ** (khi có tọa độ), ⋯ **Hồ sơ khách hàng**. 4 thẻ: **Suất TB / ngày** (30 ngày), **Đơn 30 ngày**, **Giao đúng giờ** (% đơn giao Đã giao trước giờ dự kiến + 15 phút / Đã giao + Thất bại, 30 ngày), **Phản ánh đang mở** (của các đơn tại điểm). Tab **Tổng quan** (thông tin điểm giao · đơn và giao hàng gần đây) · **Đơn hàng** · **Giao hàng** · **Phản ánh** (tab chỉ hiện khi có quyền xem màn tương ứng). Điểm giao chưa có tab Lịch sử / Tài liệu.

### 4.3 Hợp đồng – `crm-contract`
**Tạo hợp đồng**
1. **Tạo mới** → hộp **Tạo hợp đồng**.
2. Nhập: **Khách hàng***, **Tên hợp đồng***, Số hợp đồng (máy chủ tự sinh `HD-…`; số hợp đồng giấy của khách ghi ở Ghi chú), Ngày ký, **Hiệu lực từ***, Hiệu lực đến, Điều khoản thanh toán, Giá trị hợp đồng, Ghi chú.
3. Bảng **Loại suất & đơn giá***: mỗi dòng **Loại suất** (gợi ý mã có sẵn, vd. SANG / TRUA / TOI / DEM), Tên suất, Đơn giá. **Thêm loại suất** để thêm dòng, 🗑 để bỏ dòng.
4. **Tạo hợp đồng** → hợp đồng ở **Nháp**.
- Lỗi tại chỗ: *Chọn khách hàng*, *Nhập tên hợp đồng*, *Nhập ngày bắt đầu hiệu lực*, *Ngày hết hiệu lực phải sau ngày bắt đầu*, *Nhập ít nhất 1 loại suất*, *Mỗi dòng phải có mã loại suất*, *Mỗi loại suất chỉ được xuất hiện một lần*, *Đơn giá phải lớn hơn 0*.

**Gửi duyệt:** dòng Nháp có nút **Gửi duyệt** → "Đã gửi duyệt hợp đồng …" → trạng thái **Chờ duyệt**. Duyệt xong → **Hiệu lực**. Bị từ chối → về **Nháp**, sửa rồi gửi lại.

**Xem chi tiết:** tab **Loại suất & đơn giá** (khách, trạng thái, yêu cầu duyệt đang chờ, số đơn đang mở, cảnh báo). Chỉ **Chỉnh sửa** được khi Nháp.

**Trang chi tiết hợp đồng (UI-6):** bấm 1 dòng hợp đồng (ở danh sách hoặc hồ sơ khách 360) → **trang riêng** `#/crm-contract-detail?id=…` thay ngăn trượt (chế độ DEMO vẫn ngăn trượt). Phần đầu: số hợp đồng + trạng thái + "Chờ duyệt …" / "Còn N ngày"; thanh bước **Nháp → Chờ duyệt → Hiệu lực → Hết hạn** (Chấm dứt tô đỏ); nút theo trạng thái – **Gửi duyệt**, **Sửa** (Nháp), **Duyệt / Từ chối** (Chờ duyệt), ⋯ **Thanh lý hợp đồng** (Hiệu lực), **Mở màn Phê duyệt**, **Hồ sơ khách hàng** – đúng các thao tác như ở danh sách. 4 thẻ: **Giá trị hợp đồng**, **Còn hiệu lực** (ngày), **Đơn hàng** (chưa giao · suất 30 ngày), **Đã đối soát** (Σ tiền chốt). Tab **Tổng quan** (thông tin · loại suất & đơn giá · cách tính tiền đối soát – đổi được khi có quyền Sửa) · **Đơn hàng** (20 đơn gần nhất) · **Duyệt** (khi đã gửi duyệt: các cấp, cảnh báo) · **Chứng từ liên quan** · **Tài liệu** · **Lịch sử**.

**Thanh lý:** dòng Hiệu lực có **Thanh lý** → nhập **Lý do thanh lý*** → **Thanh lý** → **Chấm dứt**.
- *Còn … đơn hàng chưa giao có ngày ăn sau …: … – hủy / giao xong trước khi chấm dứt*.

**Quét hợp đồng hết hạn:** nút đầu trang → hệ thống liệt kê hợp đồng Hiệu lực đã quá ngày hết hiệu lực → **Chuyển sang Hết hạn** (không hoàn tác). Job 00:30 cũng tự làm việc này.

### 4.4 Dự báo suất ăn – `crm-forecast` (theo tuần – UI-6.5, D-84)
Ước số suất từng khách cho tuần tới khi khách chưa gửi đăng ký – dùng để mua nguyên liệu sớm. Khách gửi đăng ký rồi thì hệ thống dùng số đăng ký.

**Chọn tuần:** ◀ ▶ đổi tuần, **Tuần sau** về tuần mặc định (thứ Hai tuần sau → Chủ nhật).

**3 bước:**
1. **Tạo gợi ý tuần này** – hệ thống lấy **trung bình suất thực nhận cùng thứ 4 tuần gần nhất** (làm tròn lên; thực nhận = suất tính tiền trong đối soát → suất khách nhận khi bàn giao → số đặt đã chốt; tuần khách nghỉ không tính) ghi vào ô **Nháp**. Ô người đã sửa và ô khách đã đăng ký giữ nguyên. Muốn tạo lại cho riêng một khách: nút **⋯** ở cột Tổng của dòng khách.
2. **Sửa ô cần** (nghỉ lễ, tăng ca, khách mới): gõ số → **Enter** hoặc rời ô = lưu bản Nháp. Xóa trống ô = bỏ bản Nháp của ô.
3. **Công bố tuần này** – mọi ô Nháp của tuần → **Đã công bố** (số cũ cùng ô bị thay thế). Ô khách đã đăng ký không công bố; ô thiếu dữ liệu để trống.

**4 thẻ:** Tổng suất tuần (số khách) · Ô Nháp (chưa công bố / đã công bố / đã đăng ký) · Ô thiếu dữ liệu (nhập tay) · Sai số 4 tuần (dự báo so với thực nhận).

**Tab Dự báo tuần:** mỗi khách có **hợp đồng hiệu lực** tự hiện một nhóm (kể cả khách chưa có dự báo) → dòng **điểm giao · buổi** × 7 ngày + cột Tổng, dòng **Tổng cộng** cuối bảng. Bấm dòng khách để mở / thu gọn; **Mở tất cả / Thu gọn tất cả**; ô **Lọc khách hàng** (trên 10 khách thì mặc định thu gọn). Màu ô:

| Màu | Ý nghĩa | Sửa được |
|---|---|---|
| Xanh lá | Đã công bố | Có – sửa tạo bản Nháp mới |
| Cam | Nháp (chưa công bố) | Có |
| Xanh dương | Khách đã đăng ký (đơn Đã xác nhận trở lên) – dùng số đăng ký | Không |
| Xám, viền đứt, chữ "nhập" | Thiếu dữ liệu (dưới 2 tuần có đơn) – nhập tay | Có |
| Gạch chéo "–" | Ngày ngoài hiệu lực hợp đồng | Không |

Chữ nhỏ dưới ô: **TB** 4 tuần (· "đã sửa" nếu Nháp khác số gợi ý); rê chuột vào ô xem 4 số thực nhận cùng thứ. Trên điện thoại mỗi khách là một thẻ, mỗi điểm giao · buổi một lưới 7 ô.

**Tab Độ chính xác:** thẻ **Sai số dự báo** (Σ |dự báo − thực nhận| / Σ thực nhận), **Lệch chung** (dương = dự báo cao), **Khách sai số > 10%**; bảng theo khách (rê chuột xem từng tuần).

**Tab Lịch sử công bố:** các dòng dự báo đã công bố, mới nhất trước; ghi chú "Hệ thống tự công bố" = do hệ thống công bố lúc 16:00 thứ Sáu.

**Vòng tự động hằng tuần** (bot Điều hành gửi NV KD phụ trách khách + Trưởng KD):
| Lúc | Hệ thống làm |
|---|---|
| Thứ Tư 22:00 | Tự tạo gợi ý tuần sau cho mọi khách có hợp đồng hiệu lực |
| Thứ Năm 08:00 | Báo: dự báo tuần sau đã sẵn – N ô Nháp cần xem, M ô thiếu dữ liệu |
| Thứ Sáu 12:00 | Còn ô Nháp chưa công bố → nhắc kèm danh sách khách |
| Thứ Sáu 16:00 | Tự công bố mọi ô Nháp ("Hệ thống tự công bố"); báo danh sách ô thiếu dữ liệu còn trống |

**Quyền:** Kinh doanh (Trưởng KD, NV KD) xem + sửa + công bố; Kế hoạch SX chỉ xem (nút bị khóa, ô chỉ đọc).

**Nhập dự báo nhiều ngày** (nút đầu trang, cần quyền Nhập – dùng khi nhập một lúc nhiều ngày cho một khách)
1. Chọn **Khách hàng***, Điểm giao (trống = chung cho khách), **Loại suất***, Ca (trống = mọi ca), **Từ ngày* – Đến ngày*** (≤ 62 ngày).
2. Nhập số suất từng ngày, hoặc gõ vào **Điền nhanh số suất mỗi ngày** để áp cho mọi ngày. Ngày để trống không được nhập.
3. **Nhập dự báo** → tạo phiên bản mới ở **Nháp** → về lưới tuần bấm **Công bố tuần này**.

- Dự báo đã công bố được dùng làm **số gợi ý** khi tạo đơn và để cảnh báo lệch khi xác nhận đơn (> 10%).

### 4.5 Đơn hàng theo ngày – `crm-order` (UI-6.15, D-96)
Số suất hằng tuần nhập ở **Đăng ký suất tuần** (4.5b). Màn này để **theo dõi một ngày ăn và xử lý việc phát sinh** (Kinh doanh, bếp, điều phối, kế toán). Tên cũ "Đơn đặt suất" – link cũ vẫn mở đúng màn; chứng từ vẫn gọi là *đơn đặt suất*.

**Đầu màn**
- Chọn ngày: **Hôm nay** / **Ngày mai** (mặc định) hoặc **◀ ▶**; bên cạnh ghi trạng thái ngày: *chưa chốt · tự chốt lúc 20:00 …* / *đã chốt* / *đã giao xong*.
- **4 bước:** (1) Nhận số từ đăng ký tuần – "x / y điểm giao đã có số" + nút **Mở Đăng ký suất tuần** (đúng tuần của ngày); (2) Xử lý việc cần chú ý – nút cam **N việc cần xử lý** bật tab Cần xử lý; (3) Hệ thống tự chốt 20:00 – đếm ngược, chốt xong tự lập kế hoạch sản xuất; (4) Nấu → giao → đối soát – hệ thống tự cập nhật.
- **4 thẻ:** Tổng suất ngày (± % so dự báo) · Điểm giao có đơn (x / y, số khách) · Cần xử lý · Thành tiền dự kiến (chưa VAT).

**Bảng một ngày**
- Nhóm theo **khách** (bấm dòng khách để gập / mở; dòng khách có tổng từng buổi + "N cần xử lý"); dòng con là **điểm giao**, gồm cả điểm **chưa có đơn** (số dự báo hiện mờ "~150").
- Cột **Sáng · Trưa · Tối · Đêm** (chỉ buổi có trong hợp đồng) – rê chuột lên ô để xem chia theo lựa chọn; **Tổng**; **Tiến độ** 5 chấm Đã xác nhận → Đã chốt → Đang nấu → Đã giao → Đã đối soát (Nháp / khách gửi hiện chữ cam *Chưa xác nhận*; đơn hủy gạch ngang); **Cần chú ý**.
- Dưới tên điểm ghi nguồn: *Từ đăng ký suất tuần · DH-…* hoặc *Đơn phát sinh · DH-… · lý do*. Một điểm có đơn tuần + đơn phát sinh → mỗi đơn một dòng. Cuối bảng: **Tổng ngày**.
- **Cần chú ý** (hệ thống tự tính): *Chưa có số* (sau 18:00 thêm "đã nhắc") → nút **Nhập số** mở Đăng ký suất tuần đúng điểm / tuần; *Chưa xác nhận* → nút **Xác nhận**; *Lệch số / Sửa sau chốt – chờ duyệt* → nút **Duyệt** (người có quyền duyệt KD; mở trang đơn, tab Duyệt); *Lệch ±N% dự báo*; *Đơn phát sinh* (nhãn xanh, không tính vào cần xử lý). Sau giờ chốt điểm chưa có số ghi *Không có đơn (đã chốt)*.
- Tab **Tất cả · Cần xử lý · Đơn phát sinh · Đã hủy**; ô tìm khách / điểm giao / số đơn; **Xuất Excel** (tệp CSV đúng bảng đang xem).
- **Bấm một dòng có đơn** → trang chi tiết đơn (dưới). Sửa số trước giờ chốt làm ở Đăng ký suất tuần; sau giờ chốt dùng "Yêu cầu sửa sau chốt" trong trang chi tiết.
- **⋯ › Chốt đơn theo ngày** (chỉ người có quyền Sửa + quyền duyệt KD): chốt tay khi cần – job 20:00 vẫn là chính.

**Thêm đơn phát sinh** (quyền Thêm): chọn **Khách hàng*** → **Ngày ăn*** → **Điểm giao*** → **Lý do*** (Sự kiện / tiệc · Khách đặt thêm · Đơn bù do phản ánh · Khác) + ghi chú → nhập số suất theo **buổi × lựa chọn** (món trong set, đơn giá theo hợp đồng) → nút **Tạo đơn · N suất · tiền**.
- Điểm đã có đơn tuần cùng ngày → báo *"Điểm này đã có đơn … từ đăng ký tuần – đơn này là đơn thêm"* (không chặn).
- Trước giờ chốt: đơn **Đã xác nhận** luôn (vào kế hoạch). Sau giờ chốt: đơn Nháp + **tự gửi Trưởng KD duyệt** (lý do = lý do phát sinh).

**Trang chi tiết đơn (UI-6):** bấm dòng ở Đơn hàng theo ngày (hoặc số đơn ở màn khác) → **trang riêng** `#/crm-order-detail?id=…` (Ctrl / chuột giữa = tab mới; **← Đơn hàng theo ngày** quay lại đúng ngày ăn / bộ lọc / vị trí cuộn). Phần đầu: số đơn, trạng thái, "Chờ duyệt …", "Đã quá giờ chốt"; khách · điểm giao · ngày ăn · hợp đồng · NV phụ trách; thanh bước 7 trạng thái (Hủy tô đỏ). Nút (chỉ nút máy chủ cho phép, cần quyền Sửa): **Xác nhận**, **Chốt số lượng**, **Sửa số suất** (hộp sửa số từng dòng → *Lưu số lượng*), **Yêu cầu sửa sau chốt**, ⋯ **Hủy đơn**, **Mở màn Phê duyệt**, **Hồ sơ khách hàng**. 4 thẻ: **Tổng suất** (dự báo, % lệch), **Thành tiền**, **Giao hàng** (số phiếu giao, khách nhận), **Phản ánh đang mở**. Tab **Tổng quan** (thông tin đơn – bấm khách / điểm giao / hợp đồng để mở; cảnh báo; suất theo ca / loại suất; đối soát) · **Chi tiết dòng** · **Giao hàng** · **Phản ánh** · **Duyệt** (khi có yêu cầu lệch suất / sửa sau chốt) · **Chứng từ liên quan** · **Tài liệu** · **Lịch sử**. Chế độ DEMO vẫn mở ngăn chi tiết như dưới.

**Xử lý đơn (bấm vào thẻ / dòng)** – ngăn chi tiết (chế độ DEMO):
- Đầu ngăn: thanh tiến trình trạng thái; hộp thông tin: **Hạn chốt đơn**, "đã quá giờ chốt", **Đang chờ duyệt** (lệch suất / thay đổi sau giờ chốt), dự báo + % lệch.
- Bảng dòng: Ca, Loại suất, **Lựa chọn** (*Lựa chọn N* + món trong set; "–" = đơn cũ không theo lựa chọn), Điểm giao, **Số lượng** (ô sửa được khi cho phép), Dự báo, Đơn giá, Thành tiền. Mọi lựa chọn cùng buổi cùng đơn giá.
- Nút (chỉ hiện nút máy chủ cho phép):

| Nút | Khi nào | Kết quả |
|---|---|---|
| **Lưu số lượng** | Nháp / Đã gửi / Đã xác nhận, trước giờ chốt, không có yêu cầu duyệt đang chờ | Cập nhật số suất, tính lại tiền |
| **Xác nhận** | Nháp hoặc Đã gửi, trước giờ chốt | Hộp xác nhận hiện % lệch so với dự báo → **Đã xác nhận** (BIZ-2.1: đơn nhân viên nhập **không còn bước Gửi**; *Đã gửi* chỉ dùng cho đơn khách tự đăng ký) |
| **Chốt số lượng (Locked)** | Đã xác nhận | → Đã chốt (không cần đợi job 20:00) |
| **Hủy đơn** | Nháp / Đã gửi (mọi lúc); Đã xác nhận (trước giờ chốt) | Bắt buộc **Lý do hủy** → Hủy |

- **Lệch suất lớn (BIZ-2.1):** trước giờ chốt sửa số **ghi ngay, không đi duyệt**. Tổng suất lệch > 30% so với **trung bình 4 tuần cùng thứ của điểm giao** → chỉ **cảnh báo** + ghi vào ghi chú đơn `[LECH_TB_4_TUAN]`. Sau giờ chốt vẫn phải gửi *Yêu cầu sửa sau chốt* (người duyệt: Trưởng KD – nhóm SAL-CON – hoặc COO).
- Tab **Hành trình**: các chặng đơn → kế hoạch → sản xuất → đóng gói → giao → đối soát → phản ánh, kèm chứng từ từng chặng.

- **Chốt đơn theo ngày** (đầu màn `crm-order`, cần quyền Sửa): chọn ngày ăn → **Chốt đơn** → xác nhận → khóa mọi đơn **Đã xác nhận** của ngày (chỉ khi đã tới giờ chốt 20:00 hôm trước; bình thường job 20:00 tự chốt). Màn báo số đơn đã chốt và số đơn còn chưa xác nhận / đang chờ duyệt.

### 4.5b Đăng ký suất tuần – `crm-order-week` (UI-6.6, D-85)
Ghi số suất khách đăng ký cho từng điểm giao, từng buổi / lựa chọn trong tuần. Số đã xác nhận đi thẳng vào đơn hàng (mỗi ngày 1 đơn); **20:00 hôm trước hệ thống tự chốt** từng ngày.

**Chọn tuần:** ◀ ▶ đổi tuần, **Tuần sau** về tuần mặc định; cạnh tên tuần ghi thực đơn chung đã công bố áp dụng.

**4 bước:**
1. **Gửi phiếu cho khách** – nút *Tải phiếu cho N điểm chưa đăng ký*: tải phiếu Excel (thực đơn + ô số suất) cho mọi điểm giao chưa có số, gửi khách điền.
2. **Nhận số** – nút **Tải phiếu lên (nhiều file)**: chọn nhiều phiếu khách gửi lại một lần. Hệ thống đọc mã ẩn trong phiếu để **tự nhận khách / điểm giao / tuần**, lưu thành đơn **Nháp**. Hộp kết quả liệt kê từng tệp: *Đã lưu Nháp* · *Phiếu trống* · *Lỗi* (phiếu sai mẫu, phiếu tuần khác, ô lựa chọn không còn, ngày đã khóa). Cần quyền **Nhập**. Có thể nhập tay ở tab *Nhập số theo điểm giao*.
3. **Kiểm tra & xác nhận** – nút **Xác nhận N điểm đã nhập**: mọi đơn Nháp / khách gửi (còn trước giờ chốt) → **Đã xác nhận** một lần. Đơn lỗi báo riêng, không chặn đơn khác.
4. **Hệ thống tự chốt** – 20:00 hôm trước, từng ngày. **18:00** bot nhắc NV KD phụ trách các điểm chưa có số ngày mai; **20:05** nếu vẫn chưa có → bot 🔴 KD phụ trách + Trưởng KD + COO kèm danh sách. Hệ thống **không tự tạo đơn thay khách** (tiền tính theo số khách ký nhận) – gọi khách lấy số rồi nhập qua *Yêu cầu thay đổi sau giờ chốt* (mục 4.6).

**4 thẻ:** Điểm đã đăng ký / tổng điểm · Tổng suất tuần · Thành tiền (chưa VAT) · Lệch so dự báo (chỉ tính ô có cả số đăng ký và dự báo).

**Tab Tổng quan tuần** (mặc định): mọi khách có hợp đồng hiệu lực trong tuần tự hiện → điểm giao đang hoạt động × 7 ngày; ô = tổng suất ngày, màu:

| Màu | Ý nghĩa |
|---|---|
| Xám nét đứt, "—" | Chưa đăng ký – chữ nhỏ "DB …" = dự báo đã công bố |
| Cam | Nháp – KD đang nhập |
| Tím | Khách gửi (qua bot) – chờ KD xác nhận |
| Xanh lá | Đã xác nhận |
| Xám đậm | Đã chốt (khóa) |
| Gạch chéo | Ngày ngoài hiệu lực hợp đồng |

Cột **Cả tuần** + nút **Nhập** (mở điểm đó ở tab sau); dòng **Tổng**. Bấm dòng khách để mở / thu gọn, **Mở tất cả / Thu gọn tất cả**, ô **Lọc khách hàng / điểm giao** (trên 10 khách mặc định thu gọn). Điện thoại: mỗi khách một thẻ.

**Tab Nhập số theo điểm giao:** ô chọn điểm + trạng thái + **◀ Điểm trước / Điểm sau ▶** (theo thứ tự tab Tổng quan). Nhóm **buổi** (bấm để mở / gập; dòng buổi có tổng từng ngày, giá, số lựa chọn) → dòng **lựa chọn** × ngày: ô số suất, **món trong set là chữ nhỏ dưới ô** (rê chuột xem đủ + TB 4 tuần). **⚠** = lệch > 30 % so TB 4 tuần cùng thứ – chỉ cảnh báo. Gõ số rồi **rời ô / Enter = lưu Nháp** (ngày đó). Dòng **Tổng suất**, **Thành tiền**. Nút:

| Nút | Kết quả |
|---|---|
| **Chép số tuần trước** | Điền ô trống bằng số tuần trước (cùng thứ, buổi, lựa chọn) – chưa lưu, bấm **Lưu nháp các ô đã điền** |
| **Lấy số dự báo** | Điền buổi còn trống bằng dự báo đã công bố; chia các lựa chọn theo tỉ lệ suất đặt 4 tuần gần nhất (chưa có lịch sử → dồn vào lựa chọn 1) – chưa lưu |
| **In phiếu** | Tải phiếu Excel của điểm này |
| **Xác nhận điểm này** | Lưu cả tuần của điểm → mỗi ngày có số thành đơn **Đã xác nhận** (cảnh báo lệch dự báo nếu có) |

**Tab Lịch sử:** theo điểm giao – từng đơn trong tuần: tạo đơn, thêm / sửa dòng (số trước → sau), gửi, xác nhận, chốt; ai làm, lúc nào.

- **Ngày bị khóa** (nhãn *Khóa*): đã qua 20:00 hôm trước, đơn đã chốt / đang sản xuất, hoặc đang chờ duyệt → sửa qua mục 4.6.
- Sửa số của đơn **Đã xác nhận** lệch > 30 % → ngày đó **Chờ duyệt** (như sửa trên đơn).
- Ngày khách đã xác nhận → ô tương ứng ở màn **Dự báo suất ăn** chuyển "khách đã đăng ký" (dùng số đăng ký).
- **Kho / bếp nhận đúng số**: sau chốt, kế hoạch sản xuất và nhu cầu nguyên liệu tính **số suất lựa chọn × món trong set** (vd 2.000 suất LC1 → 2.000 phần mỗi món của LC1; món 2 cái / suất như bánh mì → 4.000 cái) – không còn chia đều + 15%.

### 4.6 Thay đổi đơn sau giờ chốt
- **Sửa số suất** sau giờ chốt (20:00 D-1) – kể cả đơn **Đã chốt**: ngăn chi tiết đơn có nút **Yêu cầu sửa sau chốt** (chỉ hiện khi đã quá giờ chốt): nhập số suất mới từng dòng + **Lý do*** → **Gửi yêu cầu** → yêu cầu duyệt ORDER_CHANGE_AFTER_CUTOFF (số `XN-…`) chờ **Trưởng KD (nhóm SAL-CON) hoặc COO** duyệt (BIZ-2.1); duyệt xong số lượng mới được ghi, đơn → Đã chốt.
- **Thêm đơn mới** sau giờ chốt: dùng **Tạo đơn hàng** + lý do (mục 4.5 bước 4).
- **Đổi món**: bộ phận Kế hoạch tạo **Yêu cầu đổi món** (`03_KE_HOACH.md`).
- Trước giờ chốt không cần duyệt: sửa số trực tiếp (lệch > 30 % so trung bình 4 tuần chỉ cảnh báo – mục 4.5).

### 4.7 Điều chỉnh giá – phụ lục trong hợp đồng (UI-6.4, D-83)
Mục menu "Chính sách giá" **đã bỏ**: đổi giá giữa kỳ làm bằng **phụ lục điều chỉnh giá** ngay trong hợp đồng (link cũ tới Chính sách giá tự mở hợp đồng).
1. Mở hợp đồng **đang Hiệu lực** → tab **Loại suất & đơn giá**: bảng giá gốc theo loại suất + giá **đang áp hôm nay** (theo giá gốc hay theo phụ lục nào).
2. Bấm **+ Điều chỉnh giá** (quyền Sửa hợp đồng): tích loại suất + nhập **giá mới** (> 0, nhiều loại suất một lần được), **Áp dụng từ ngày** (từ ngày mai, trong hiệu lực hợp đồng), **Đến ngày** (trống = hết hợp đồng), **Lý do** → **Lập phụ lục** (Nháp).
3. Trên dòng phụ lục: **Đính kèm phụ lục** (bản đã ký hai bên – PDF / ảnh) → **Gửi duyệt**. Chưa đính kèm thì không gửi duyệt được.
4. Người duyệt (CEO) duyệt ở màn **Phê duyệt** (loại *Phụ lục điều chỉnh giá* – thấy giá cũ → giá mới, chênh %, ngày áp dụng, có phụ lục chưa). Duyệt → **Hiệu lực**; từ chối → *Từ chối* kèm lý do (xóa được bản Nháp / Từ chối).
- **Lịch sử điều chỉnh giá** trong tab: từng phụ lục (từ – đến ngày, giá cũ → mới, chênh %, trạng thái, người duyệt, số tệp phụ lục); phụ lục **đang áp hôm nay** được tô đậm.
- **Tra giá khi chốt đơn:** phụ lục đang hiệu lực của hợp đồng → giá gốc trong hợp đồng. Giá trên đơn **cố định lúc chốt** – phụ lục duyệt sau giờ chốt không đổi đơn đã chốt.
- Lỗi: *Chỉ điều chỉnh giá khi hợp đồng đang Hiệu lực*, *Áp dụng từ ngày phải từ ngày mai…*, *Thời gian áp dụng phải nằm trong hiệu lực hợp đồng…*, *Loại suất … đã có phụ lục … chồng thời gian áp dụng*, *Chưa đính kèm phụ lục đã ký…*.

### 4.7b Bảng kê thanh toán – `crm-billing` (UI-6.9, D-87)
Tổng hợp tiền phải thu từng khách trong tháng (suất tính tiền đã chốt đối soát × đơn giá cố định lúc chốt + phí thất thoát khay + VAT, trừ suất lỗi Delicare) để gửi khách xác nhận khối lượng → hóa đơn / công nợ ở phần mềm kế toán. **Mở màn là hệ thống tự tính cho mọi khách – không còn nút "Lập bảng kê".**

- **Tháng** ◀ ▶ (mặc định tháng trước).
- **3 bước:**
  - ① *Chốt hết đối soát của tháng* – nút *N đơn chờ đối soát (M khách)* mở tab Chờ đối soát.
  - ② *Kiểm tra* – bấm dòng xem chi tiết; ⚠ = tiền suất lệch > 20 % so tháng trước (`BillingMonthDeviationPct`).
  - ③ *Xuất / in hàng loạt* – nút **Xuất tất cả khách sẵn sàng (N)**.
- **4 thẻ:** Tổng phải thu (có VAT) · Khách sẵn sàng / khách có phát sinh · Đơn chờ đối soát · Phí thất thoát khay.
- **Tab Theo khách hàng:** mọi khách có phát sinh (khách hợp đồng hiệu lực không phát sinh xếp cuối).
  - Cột: suất tính tiền, tiền suất ăn (⚠ khi lệch), phí khay, VAT, tổng thanh toán.
  - **Trạng thái:** **Sẵn sàng** (đã chốt hết đối soát) · **Chờ đối soát · N đơn** (số tạm tính) · **Không phát sinh**.
  - Nút **XLS / PDF** trên từng dòng (xuất ngay); dòng **Tổng**.
  - Bấm dòng → **chi tiết ngay dưới**: theo buổi – suất tính tiền, suất lỗi đã trừ, đơn giá, thành tiền; phí khay; ghi chú đơn bù giá 0; cảnh báo "số tạm tính" khi còn đơn chờ.
- **Chọn nhiều** (ô chọn đầu dòng / chọn tất cả) → **thanh dưới đáy**: "Đã chọn N khách · tổng tiền" (cảnh báo nếu có khách chờ đối soát).
  - **Excel** = 1 tệp, sheet *Tổng hợp* + mỗi khách 1 sheet.
  - **PDF** = 1 tệp nén, mỗi khách 1 tệp `BangKe_<MaKH>_<YYYYMM>.pdf`.
- **Gửi khách xác nhận** (trên dòng và thanh chọn): **chưa nâng cấp** – bấm hiện "Tính năng chưa được nâng cấp. Vui lòng liên hệ quản trị viên." (D-87 c – đợt sau: email / Zalo OA / cổng khách).
- **Tab Chờ đối soát:** đơn chưa chốt đối soát của tháng, nhóm theo khách; nút mở màn **Đối soát suất ăn**.
- **Tab Lịch sử xuất:** ai xuất / in, lúc nào, khách nào, kỳ nào (gồm cả xuất lô).
- **Tự động:** 08:00 **ngày 1** hằng tháng bot Điều hành báo Kế toán + Trưởng KD: "Bảng kê tháng MM/YYYY: X/Y khách sẵn sàng, Z khách còn N đơn chờ đối soát".
- Cách tính giữ nguyên BIZ-3.2:
  - chỉ đơn có **đối soát đã chốt**;
  - **suất tính tiền** = khách nhận − lỗi Delicare;
  - **đơn giá cố định lúc chốt**;
  - **phí thất thoát khay** = khay mất vượt ngưỡng × phí / khay (`ContainerLossFee`);
  - **VAT 8 %** (`SalesVatPct`);
  - đơn bù do lỗi Delicare (giá 0) không tính.
- Mẫu Excel / PDF có ô ký: khách hàng, kế toán, đại diện Delicare.
- **Quyền:** Kinh doanh + Kế toán xem / xuất / in.

### 4.8 Đối soát suất – `crm-reconciliation`
Màn mới (UI-6.8, D-86): kỳ 7 ngày / 30 ngày / Tháng, 3 bước (Xác nhận số → Xử lý chênh lệch → Chốt đối soát + Bảng kê tháng), 2 biểu đồ cạnh nhau, **bảng xác nhận ngay trên trang** (phục vụ mặc định = khách nhận; **Xác nhận tất cả dòng khớp** 1 nút; chỉ sửa dòng có sự cố). Thao tác chi tiết: `10_DIEM_PHAT.md` mục 4.2. Đơn đặt **theo lựa chọn**: bấm ▸ trên dòng để ghi phục vụ từng lựa chọn; tiền = Σ suất × giá buổi.

## 5. Trạng thái chứng từ

| Chứng từ | Luồng | Ai chuyển |
|---|---|---|
| Hợp đồng | Nháp → Chờ duyệt → Hiệu lực → Hết hạn (job 00:30 / Quét) / Chấm dứt (Thanh lý) | KD gửi duyệt, Ban điều hành duyệt |
| Dự báo | Nháp → Đã công bố → Bị thay thế | KD |
| Đơn hàng | Nháp → (Đã gửi – đơn khách tự đăng ký) → Đã xác nhận → Đã chốt → Đang sản xuất → Đã giao → Đã đối soát; Hủy | KD tới Đã chốt (hoặc job 20:00); Đang sản xuất khi phát hành kế hoạch SX; Đã giao khi bàn giao xong; Đã đối soát khi đối soát |
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
| Lựa chọn N không có trong thực đơn áp dụng của … ngày … (lựa chọn đang có: …) | Thực đơn đã đổi → chọn lựa chọn đang có / báo bếp |
| Chưa có thực đơn áp dụng cho … ngày … – không chọn được lựa chọn | Gán thực đơn cho khách hoặc nhờ Kế hoạch công bố thực đơn chung |
| Trùng dòng suất (cùng suất hợp đồng, ca và lựa chọn) | Mỗi lựa chọn chỉ 1 dòng |
| Không phải phiếu đăng ký suất của hệ thống / Phiếu không đúng khách / điểm giao / tuần | Tải lại phiếu từ màn Đăng ký suất tuần đúng khách, điểm giao, tuần; không sửa hàng / cột ẩn |
| Phiếu của tuần …, không phải tuần đang mở | Chuyển màn sang đúng tuần của phiếu rồi tải lên lại |
| Phiếu chưa điền số suất – không có gì để lưu | Khách gửi lại phiếu trống – liên hệ khách |
| Không có điểm giao nào có đơn Nháp / khách gửi chờ xác nhận | Mọi điểm đã xác nhận hoặc đã quá giờ chốt |
| Không có dự báo đã công bố cho buổi còn trống | Công bố dự báo ở màn Dự báo suất ăn trước |
| Chỉ sửa hợp đồng / loại suất khi hợp đồng ở trạng thái Nháp | Hợp đồng đã duyệt chỉ Thanh lý; muốn đổi giá → **phụ lục điều chỉnh giá** trong hợp đồng (4.7) hoặc hợp đồng mới |
| Số hợp đồng … đã tồn tại | Để trống ô số để tự sinh |
| Hợp đồng đã hết hạn (…) – không duyệt được | Sửa ngày hiệu lực rồi gửi lại |
| Khoảng ngày không hợp lệ (tối đa 62 ngày) | Thu hẹp khoảng ngày (Nhập dự báo nhiều ngày) |
| Khách đã đăng ký số suất ngày này – dùng số đăng ký, không sửa dự báo | Ô xanh dương chỉ đọc; muốn đổi số → sửa đơn / đăng ký suất tuần |
| Không sửa dự báo của ngày đã qua | Chỉ sửa từ hôm nay trở đi |
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
