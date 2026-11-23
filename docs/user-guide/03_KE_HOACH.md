# 03 – Kế hoạch vận hành & Cost (món, công thức, thực đơn, kế hoạch sản xuất, giá thành)

## 1. Bạn là ai

- Nhóm quyền: **OPS-PLAN** (1.1 Kế hoạch vận hành và Cost).
- Bot dùng: **Vận hành** (phân hệ 📋 Kế hoạch).
- Việc chính: món + công thức có phiên bản, giá thành chuẩn, thực đơn tuần, phân công thực đơn cho khách, đổi món, **lập kế hoạch sản xuất ngày mai**, theo dõi hao hụt và giá thành.

## 2. Màn hình được dùng

| screen-id | Menu › Mục | Quyền |
|---|---|---|
| `menu-dish-cat` | Thực đơn & Món ăn › Món ăn | Xem, sửa |
| `menu-dish` | … › Món ăn | Xem, sửa |
| `menu-recipe` | … › Công thức chế biến (version) | Xem, sửa |
| `menu-menu` | … › Thực đơn | Xem, sửa |
| `menu-assignment` | … › Phân công thực đơn | Xem, sửa |
| `menu-change-request` | … › Yêu cầu đổi món | Xem, sửa |
| `cost-standard` | Sản xuất & Giá thành › Giá thành tiêu chuẩn | Xem, sửa, xuất |
| `prod-line`, `prod-workstation` | … › Dây chuyền, Khu vực & trạm làm việc | Xem, sửa, xuất |
| `prod-plan` | … › Kế hoạch sản xuất | Xem, sửa, xuất |
| `prod-order` | … › Lệnh sản xuất | Xem, sửa, xuất |
| `prod-batch` | … › Mẻ sản xuất | Xem, sửa, xuất |
| `prod-waste` | … › Hao hụt thực phẩm | Xem, sửa, xuất |
| `cost-actual` | … › Giá thành thực tế | Xem, sửa, xuất |
| `cost-meal` | … › Giá thành / suất & biên lợi nhuận | Xem, sửa, xuất |
| `scm-requirement`, `scm-inv-balance`, `crm-order`… | Mua hàng & Kho, Kinh doanh | Chỉ xem |

Mặc định **không có quyền duyệt**: duyệt công thức, **Khóa & áp dụng** công thức, **duyệt thực đơn**, **duyệt kế hoạch sản xuất** do COO / CEO (mục "Phê duyệt › …"). Bấm các nút này khi chưa được cấp → *"Bạn không có quyền …"*.

## 3. Việc theo thứ tự

| Khi nào | Việc | Màn |
|---|---|---|
| Khi có món mới / đổi định lượng | Tạo phiên bản công thức → gửi duyệt → (COO) khóa & áp dụng | `menu-recipe` |
| Khi giá NCC đổi | Tính lại giá thành chuẩn | `cost-standard` |
| Hằng tuần | Lập / nhân bản thực đơn tuần → duyệt → công bố → xuất gửi khách | `menu-menu` |
| Đầu hợp đồng | Gán thực đơn cho khách / điểm giao / loại suất | `menu-assignment` |
| Khi khách xin đổi | Tạo yêu cầu đổi món | `menu-change-request` |
| Sau 20:00 (đơn đã chốt) | Lập kế hoạch sản xuất ngày mai từ đơn → COO duyệt → **Ra lệnh SX** | `prod-plan` |
| Sau sản xuất | Theo dõi hao hụt, giá thành lệnh, giá vốn suất | `prod-waste`, `cost-actual`, `cost-meal` |

## 4. Thao tác từng bước

### 4.1 Món ăn – `menu-dish`
Danh sách: Mã, Món ăn, Nhóm món, Giá thành chuẩn, **Công thức đang dùng** (phiên bản + trạng thái, hoặc "Chưa có công thức hiệu lực"), CP NVL ước tính, Trạng thái. Thêm / sửa món bằng form chung.

### 4.2 Công thức chế biến – `menu-recipe`
Màn 2 cột: trái là danh sách món (ô **Tìm món…**, số phiên bản, nhãn "chờ" nếu có bản Nháp / Chờ duyệt); phải là các phiên bản của món đang chọn.

**Tạo phiên bản mới**
1. Chọn món → **Tạo phiên bản mới** → bản **Nháp** mới, chép định lượng của phiên bản mới nhất.
   - *Món đang có bản công thức nháp / chờ duyệt – hoàn tất bản đó trước*.
2. Trên bản Nháp:
   - Sửa **Định lượng tinh / 100 suất** (lượng sau chế biến) trực tiếp trong bảng (lưu ngay). Bảng hiện thêm **Sau sơ chế** = tinh ÷ (1 − hao hụt chế biến %) và **Lượng mua (trước sơ chế)** = sau sơ chế ÷ (1 − hao hụt sơ chế %) – giống file định lượng (vd. 70 g, 35 %, 5 % → 107,7 g → 113,4 g). Nhu cầu nguyên liệu, phiếu xuất và giá vốn đều tính theo lượng mua.
   - **Thêm nguyên liệu**: chọn Nguyên liệu, Định lượng, ĐVT, **Hao hụt chế biến %**, **Hao hụt sơ chế %** (để trống = hao hụt chuẩn của nguyên liệu), Thứ tự, Ghi chú sơ chế → **Tạo mới**.
   - 🗑 trên dòng để bỏ nguyên liệu.
   - *Định lượng phải lớn hơn 0*, *Hao hụt phải trong khoảng [0, 100)*, *Chưa có quy đổi giữa 2 đơn vị này*.
3. **Gửi duyệt** → xác nhận → **Chờ duyệt** (không sửa được định lượng tới khi có kết quả). *Công thức phải có ít nhất 1 nguyên liệu*.
4. Người duyệt duyệt ở **Yêu cầu phê duyệt** → bản thành **Đã duyệt**; từ chối → về **Nháp**.
5. **Một lần duyệt (BIZ-2.3):** khi **Gửi duyệt** chọn **Áp dụng từ ngày*** (mặc định ngày mai). **Duyệt cấp cuối = khóa & áp dụng ngay** từ ngày đó (ngày đã qua → ngày duyệt): phiên bản đang dùng chuyển **Hết dùng** từ ngày trước đó, giá thành chuẩn của món tính lại; sau khi khóa không sửa được. Bản *Đã duyệt* chưa khóa còn sót từ trước có nút **Áp dụng** (dùng một lần để dọn).

**Tính giá vốn** (mọi phiên bản): nhập **Số suất cần tính** (mặc định 100) → **Tính** → giá vốn NVL / suất, tổng, bảng từng nguyên liệu (định lượng, đơn giá NCC ưu tiên, thành tiền). Cảnh báo *"Có nguyên liệu chưa tính được chi phí – giá vốn đang thấp hơn thực tế"* = thiếu giá NCC hoặc thiếu quy đổi đơn vị.

### 4.3 Giá thành tiêu chuẩn – `cost-standard`
**Tự tính lại khi giá NCC đổi (BIZ-3.3):** thêm / sửa / đổi ưu tiên / xóa dòng bảng giá NCC → hệ thống tự tính lại giá thành chuẩn các món có công thức khóa dùng nguyên liệu đó (thông báo khi lưu) và kiểm FC % các thực đơn Đã duyệt / Đã công bố còn hiệu lực có món này (giá bán = đơn giá suất thấp nhất trong hợp đồng hiệu lực). Vượt ngưỡng 55 % → bot 🔴 Kế hoạch + COO kèm thực đơn / lựa chọn / thứ vượt.
- **Tính lại giá thành** (đầu trang): tính lại mọi món có công thức đang áp dụng theo giá NCC hiện hành → bản đang dùng ngừng hiệu lực, thay bằng bản mới.
- **Tính lại** trên từng dòng Đang dùng: chỉ món đó.
- Kết quả: "Đã tính lại giá thành chuẩn cho N món" / "Không có món nào có công thức đang áp dụng để tính lại".

### 4.4 Thực đơn tuần – `menu-menu`
**Tự hết hiệu lực + nhắc (BIZ-3.4):** thực đơn Đã công bố qua ngày hiệu lực cuối (trống ngày đến = hiệu lực từ + 6) → tự **Hết hiệu lực** lúc 00:30. **Thứ Tư 09:00** chưa có thực đơn chung Đã công bố cho tuần sau → bot 🔴 Kế hoạch + COO. **Thứ Sáu 16:00** khách chưa gửi đăng ký suất tuần sau → bot nhắc NV kinh doanh phụ trách.
**Xem:** chip chọn thực đơn (mã + trạng thái); thẻ Thực đơn (hiệu lực, loại), Số dòng món, Khách hàng được gán, Suất đã đặt; chip **Loại suất**; bảng **Nhóm món × T2…CN** – mỗi ô là món + giá vốn / suất; hàng **Giá vốn / suất** từng ngày + % so với giá bán (đỏ nếu vượt mục tiêu). Nhiều lựa chọn cho một loại suất hiện "Lựa chọn 1, 2…".

**Lập thực đơn**
1. **Tạo mới** (bảng chung) hoặc **Nhân bản** thực đơn đang xem: nhập Mã, Tên, **Hiệu lực từ*** → **Nhân bản** → thực đơn Nháp mới, chép toàn bộ món.
2. **Thêm món**: chọn Món, Loại suất, **Thứ** (1 = T2 … 7 = CN), Lựa chọn (1–9), Số phần, Bắt buộc, Ghi chú → **Tạo mới**. Bấm vào món trong lịch để xem / sửa / xóa dòng.
3. Nút **Chuyển sang …** theo trạng thái:
   - Nháp → **Đã duyệt** (cần quyền "Phê duyệt › Thực đơn"),
   - Đã duyệt → **Đã công bố** (kiểm đủ món mọi ngày, công thức hiệu lực),
   - Đã công bố → **Hết hiệu lực**.
4. **Xuất gửi khách**: tải tệp thực đơn gửi khách. **In phiếu đăng ký**: chọn khách + điểm giao → tải *Phiếu đăng ký suất* (Excel, ô Số suất dưới mỗi lựa chọn, tuần bắt đầu hiệu lực của thực đơn) – khách điền rồi gửi lại cho Kinh doanh tải lên (`02_KINH_DOANH.md` mục 4.5b).
5. **Thực đơn chung** (ORD-OPT-1, D-74 f): ô **Loại thực đơn** = *Thực đơn chung* → mọi khách **không có phân công riêng** tự dùng thực đơn này khi đã **Đã công bố** và hiệu lực tại ngày ăn (nhãn cam *Thực đơn chung* ở đầu lịch). Mỗi tuần một thực đơn chung (thường **Nhân bản** từ tuần trước). Không công bố được 2 thực đơn chung chồng ngày hiệu lực.
6. **Lựa chọn** (1–9) = set món: Sáng LC1 = bánh mì + trứng + sữa… Khách đặt số suất theo từng lựa chọn; **Số phần** = số phần của món trong 1 suất (vd bánh mì 2 cái / suất; trống = 1) – dùng để tính số phần nấu / mua.

Lỗi: *Đã có thực đơn chung … Đã công bố chồng thời gian hiệu lực…* (cho thực đơn chung cũ hết hiệu lực hoặc đổi ngày), *Thực đơn phải có ít nhất 1 món*, *Chưa đủ món để công bố – thiếu: …*, *Cảnh báo: món … chưa có công thức đang khóa – chưa tính được giá vốn*, *Thứ phải trong khoảng 1 (Thứ 2) – 7 (Chủ nhật)*, *Lựa chọn (SequenceNo) phải trong khoảng 1 – 9*, *Dòng thực đơn phải có loại suất*.

### 4.5 Phân công thực đơn – `menu-assignment`
- Danh sách phân công (khách / điểm giao / loại suất / thực đơn / hiệu lực) – thêm / sửa bằng form chung. **Chỉ cần cho khách ngoại lệ** (trường học, bệnh viện… có thực đơn riêng); khách khác dùng **thực đơn chung** của tuần.
- **Tra thực đơn áp dụng**: chọn **Khách hàng***, Điểm giao, **Loại suất***, **Ngày ăn*** → **Tra cứu** → thực đơn + cấp phân công (*Gán theo điểm giao* / *Gán theo khách hàng* / *Thực đơn chung* khi khách không có phân công); bấm mã thực đơn để mở lịch.
- Lỗi: *Chồng thời gian hiệu lực với phân công thực đơn khác…*, *Loại suất … không có trong suất của hợp đồng*, *Điểm giao không thuộc khách hàng này*, *Không có thực đơn được gán…*.

### 4.6 Yêu cầu đổi món – `menu-change-request`
1. **Tạo yêu cầu đổi món** → hộp nhập:
   - **Đơn hàng** (trống = áp dụng cho mọi đơn của ngày / ca), **Món hiện tại***, **Món thay thế***, **Ngày ăn*** (tự lấy theo đơn nếu chọn đơn), Ca, **Số suất đổi*** (trống = toàn bộ suất của đơn khi có chọn đơn), Thực đơn, **Lý do*** (≥ 3 ký tự).
2. **Tạo & gửi duyệt** → yêu cầu **Chờ duyệt**; kết quả: Đã duyệt / Từ chối / Đã hủy.
- Lỗi: *Món thay thế phải khác món hiện tại*, *Yêu cầu không theo đơn cần có ngày ăn và số suất đổi*, *Đơn … đang … – chỉ đổi món cho đơn chưa giao / chưa hủy*, *Món cũ không có trong thực đơn của đơn…*.

### 4.7 Kế hoạch sản xuất – `prod-plan`
**Lập kế hoạch**
1. **Lập từ đơn hàng** → chọn **Ngày sản xuất***, Đơn vị, Ghi chú → **Lập kế hoạch**.
2. Hệ thống tổng hợp số phần từng món từ đơn **Đã xác nhận / Đã chốt** của ngày → kế hoạch **Nháp** `KS-…` (1 kế hoạch / ngày). Bấm lại sẽ tính lại kế hoạch Nháp; kế hoạch đã duyệt không đổi.
   - Kế hoạch cũng được tạo khi bộ phận mua hàng / job 20:30 **Tính nhu cầu** (MRP).

**Duyệt:** dòng Nháp có **Duyệt** → xác nhận. Cần quyền "Phê duyệt › Kế hoạch sản xuất" (mặc định COO / CEO).

**Ra lệnh sản xuất:** dòng **Đã duyệt** chưa có lệnh → **Ra lệnh SX** → xác nhận → hệ thống sinh **lệnh sản xuất** + **mẻ nấu** theo ca và dây chuyền (lịch ngược từ giờ xuất bếp), chụp định lượng công thức và **giữ chỗ nguyên liệu** → chuyển sang màn Lệnh sản xuất. Đơn hàng của ngày → **Đang sản xuất**.

**Xuất phiếu sản xuất:** nút xuất trên mỗi dòng → tải phiếu sản xuất.

Danh sách: Số kế hoạch, Ngày SX, Ca, Đơn vị, Số món, Tổng suất, Số lệnh SX, Trạng thái.

### 4.8 Lệnh sản xuất – `prod-order`
Bấm một lệnh → ngăn chi tiết:
- Tab **Mẻ & nguyên liệu**: mẻ (số, kế hoạch, thực tế, bắt đầu, trạng thái, QC), định lượng lúc phát hành, giữ chỗ nguyên liệu (cần / đã giữ / thiếu), phiếu xuất.
- Tab **Giá thành & chênh lệch**: NVL, nhân công, chi phí chung, hao hụt, khác, tổng, giá thành / phần so với chuẩn; chênh lệch tiêu hao từng nguyên liệu.
- Nút:
  - **Tính giá thành** (lệnh Đang SX / Hoàn thành / Đóng) – ghi đè lần tính trước.
  - **Đóng lệnh** (lệnh Hoàn thành) – tính chênh lệch tiêu hao, giá thành, giải phóng giữ chỗ dư; không mở lại được.
  - **Hủy lệnh** (Kế hoạch / Phát hành, chưa mẻ nào bắt đầu) – bắt buộc lý do ≥ 3 ký tự; mẻ + phiếu xuất nháp bị hủy, giữ chỗ được giải phóng.
- Lỗi: *Lệnh … đã có mẻ bắt đầu nấu – không hủy được (mẻ không đạt thì dùng Loại mẻ)*, *Lệnh … đã xuất kho – hủy phiếu xuất trước khi hủy lệnh*.

### 4.9 Hao hụt – `prod-waste`
- Lọc **Kỳ** từ – đến (mặc định 30 ngày), **Loại hao hụt**; tổng suất / kg + top loại.
- Khối **Tổng hợp hao hụt 30 ngày**: chip **Theo loại / Theo món / Theo ca / Theo ngày** + biểu đồ.
- **Tạo mới** → hộp **Ghi hao hụt**: Mẻ / Lệnh / Món (chọn ít nhất 1), Ca, **Loại hao hụt*** (Nấu dư, QC loại, Hư hỏng, Hao hụt sơ chế, Khách trả lại, Hỏng / loại khi đóng gói), Số suất và / hoặc Khối lượng (kg), Lý do, Cách xử lý → **Ghi hao hụt**.
- *Nhập số suất hoặc khối lượng hao hụt (lớn hơn 0)*, *Chọn mẻ, lệnh sản xuất hoặc món bị hao hụt*.

### 4.10 Giá thành thực tế – `cost-actual`
- Lọc ngày sản xuất, món. Biểu đồ cơ cấu (NVL / nhân công / chung / hao hụt / khác), so với giá thành chuẩn.
- **Tính giá thành lệnh**: chọn lệnh (Đang SX / Hoàn thành / Đóng) → **Tính giá thành**. **Tính lại** trên từng dòng.
- Giá thực tế / phần đỏ = cao hơn chuẩn.

### 4.11 Giá vốn suất & lãi gộp – `cost-meal`
- Lọc ngày ăn (≤ 92 ngày). Thẻ: số đơn đã tính, giá vốn / suất, doanh thu, lãi gộp.
- **Tính giá vốn cho đơn**: chọn đơn **Đã giao / Đã đối soát** → **Tính giá vốn**. **Tính lại** / 🔍 xem chi tiết trên dòng (từng bữa, từng món, nguồn giá: thực tế / chuẩn / chưa có).
- Cảnh báo: *Đơn chưa đối soát – suất nhận lấy theo POD*, *Món … dùng giá thành chuẩn*, *không có giá thành – giá vốn tính 0*.

## 5. Trạng thái

| Chứng từ | Luồng |
|---|---|
| Công thức | Nháp → Chờ duyệt → Đã duyệt → Đã khóa (đang áp dụng) → Hết dùng |
| Thực đơn | Nháp → Đã duyệt → Đã công bố → Hết hiệu lực |
| Yêu cầu đổi món | Chờ duyệt → Đã duyệt / Từ chối / Đã hủy |
| Kế hoạch SX | Nháp → Đã duyệt → Đã phát hành → Hoàn thành |
| Lệnh SX | Kế hoạch → Phát hành → Đang SX → Hoàn thành → Đóng; Hủy |
| Giá thành chuẩn | Đang dùng → (tính lại) ngừng hiệu lực |

## 6. Lỗi / cảnh báo thường gặp

| Thông báo | Cách xử lý |
|---|---|
| Không có đơn Đã xác nhận / Đã chốt nào của ngày … có thực đơn cho ngày đó | KD chưa xác nhận đơn hoặc khách chưa được gán thực đơn |
| Kế hoạch … đang … – không sinh lại được | Kế hoạch đã duyệt – không lập lại |
| Kế hoạch … đã tính nhu cầu nguyên liệu – tính lại bằng MaterialRequirement/Calculate | Nhờ Mua hàng bấm **Tính nhu cầu** lại |
| Kế hoạch chưa có dòng món có số phần lớn hơn 0 | Không có suất – không duyệt được |
| Món chưa có công thức đã khóa / đã duyệt – không phát hành được: … | Hoàn tất công thức cho món |
| Cảnh báo: món … dùng công thức Đã duyệt chưa khóa | Nên khóa công thức trước khi ra lệnh |
| Ca … của kế hoạch đã phát hành / Mọi ca có nhu cầu của kế hoạch đã phát hành | Đã ra lệnh rồi |
| Không xác định được giờ xuất bếp… | Điểm giao chưa có giờ giao, ca chưa có giờ bắt đầu – bổ sung danh mục |
| Bạn không có quyền … (duyệt kế hoạch / thực đơn / khóa công thức) | Việc của COO / CEO |

## 7. Trên bot Telegram

Phân hệ **📋 Kế hoạch** (bot Vận hành):
- 📋 **Kế hoạch ngày mai** (`/kehoach`) – mẻ sản xuất ngày mai.
- 📅 **Thực đơn đang áp dụng** (`/thucdon`).
- ♻️ **Hao hụt 7 ngày** (`/haohut`).
- 💰 **Giá thành suất** (`/giathanh`) – 7 ngày.
- Thông báo: thiếu nguyên liệu sau MRP, thực đơn công bố, kết quả duyệt.
