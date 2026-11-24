# 03 – Kế hoạch vận hành & Cost (món, công thức, thực đơn, kế hoạch sản xuất, giá thành)

## 1. Bạn là ai

- Nhóm quyền: **OPS-PLAN** (1.1 Kế hoạch vận hành và Cost).
- Bot dùng: **Vận hành** (phân hệ 📋 Kế hoạch).
- Việc chính: món + công thức có phiên bản, giá thành chuẩn, thực đơn tuần (một thực đơn chung cho mọi khách – D-90), đổi món, **lập kế hoạch sản xuất ngày mai**, theo dõi hao hụt và giá thành.

## 2. Màn hình được dùng

<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->
Quyền theo ma trận D-79 (đủ 19 nhóm: `docs/claude/MENU5_MATRIX.md`).

| Menu › Mục | screen-id | OPS-PLAN |
|---|---|---|
| Việc của tôi › Việc của tôi | `hr-my` | X |
| Điều hành › Bảng điều hành | `dash-exec` | X |
| Điều hành › Chỉ số theo ngày | `kpi-daily` | X |
| Điều hành › Chỉ số theo tháng | `kpi-monthly` | X |
| Kinh doanh › Khách hàng | `crm-customer` | X |
| Kinh doanh › Đăng ký suất tuần | `crm-order-week` | X |
| Kinh doanh › Đơn đặt suất | `crm-order` | X |
| Kinh doanh › Dự báo suất ăn | `crm-forecast` | X |
| Kinh doanh › Điểm giao hàng | `crm-location` | X |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | Q |
| Thực đơn & Món ăn › Yêu cầu đổi món | `menu-change-request` | Q |
| Thực đơn & Món ăn › Công thức chế biến | `menu-recipe` | Q |
| Thực đơn & Món ăn › Món ăn | `menu-dish` | Q |
| Thực đơn & Món ăn › Nhóm món ăn | `menu-dish-cat` | Q |
| Mua hàng › Tính nhu cầu nguyên liệu | `scm-requirement` | X |
| Mua hàng › Đề nghị mua hàng | `scm-pr` | X |
| Mua hàng › Nguyên liệu | `scm-ingredient` | S |
| Kho › Tồn kho | `scm-inv-balance` | X |
| Kho › Lô hàng & hạn dùng | `scm-lot` | X |
| Sản xuất › Kế hoạch sản xuất | `prod-plan` | S |
| Sản xuất › Lệnh sản xuất | `prod-order` | S |
| Sản xuất › Mẻ nấu | `prod-batch` | X |
| Sản xuất › Sản lượng | `prod-output` | X |
| Sản xuất › Hao hụt | `prod-waste` | X |
| Sản xuất › Dây chuyền bếp | `prod-line` | Q |
| Sản xuất › Trạm bếp | `prod-workstation` | Q |
| Giá thành › Giá thành chuẩn | `cost-standard` | S |
| Giá thành › Giá thành thực tế | `cost-actual` | X (IX) |
| Giá thành › Giá vốn suất ăn | `cost-meal` | X (IX) |
| Chất lượng & ATTP › Kiểm tra hàng nhập | `qc-inspection` | X |
| Chất lượng & ATTP › Kiểm tra theo ca / mẻ | `qc-checklist-result` | X |
| Chất lượng & ATTP › Nhật ký nhiệt độ | `qc-temperature` | X |
| Chất lượng & ATTP › Vệ sinh khay hộp | `qc-cleaning` | X |
| Chất lượng & ATTP › Kiểm tra vệ sinh | `qc-cleaning-result` | X |
| Chất lượng & ATTP › Sự cố nhà cung cấp | `qc-supplier-issue` | X |
| Chất lượng & ATTP › Bộ kiểm tra ATTP | `qc-checklist` | X |
<!-- MENU5:END -->

Mặc định **không có quyền duyệt**: duyệt công thức, **Khóa & áp dụng** công thức, **duyệt thực đơn**, **duyệt kế hoạch sản xuất** do COO / CEO (mục "Phê duyệt › …"). Bấm các nút này khi chưa được cấp → *"Bạn không có quyền …"*.

## 3. Việc theo thứ tự

| Khi nào | Việc | Màn |
|---|---|---|
| Khi có món mới / đổi định lượng | Tạo phiên bản công thức → gửi duyệt → (COO) khóa & áp dụng | `menu-recipe` |
| Khi giá NCC đổi | Tính lại giá thành chuẩn | `cost-standard` |
| Hằng tuần | Lập / nhân bản thực đơn tuần → duyệt → công bố → xuất gửi khách | `menu-menu` |
| Khi khách xin đổi | Tạo yêu cầu đổi món | `menu-change-request` |
| Sau 20:00 (đơn đã chốt) | Lập kế hoạch sản xuất ngày mai từ đơn → COO duyệt → **Ra lệnh SX** | `prod-plan` |
| Sau sản xuất | Theo dõi hao hụt, giá thành lệnh, giá vốn suất | `prod-waste`, `cost-actual`, `cost-meal` |

## 4. Thao tác từng bước

### 4.1 Món ăn – `menu-dish`
Danh sách: Mã, Món ăn, Nhóm món, Giá thành chuẩn, **Công thức đang dùng** (phiên bản + trạng thái, hoặc "Chưa có công thức hiệu lực"), CP NVL ước tính, Trạng thái. Thêm / sửa món bằng form chung.

**Trang chi tiết (UI-6):** bấm 1 món → trang `#/menu-dish-detail?id=…`: phần đầu (tên, nhóm, trạng thái, công thức đang dùng; nút **Sửa**, **Công thức**), 4 chỉ số – **Giá vốn NVL / suất** (cam khi thiếu giá NCC, đỏ khi cao hơn giá thành chuẩn), **Định mức** (số nguyên liệu), **Menu dùng món**, **Mẻ đạt QC** (%). Tab **Tổng quan** (thông tin món + bảng định mức có đơn giá, thành tiền), **Định mức** (các phiên bản công thức), **Menu** (thực đơn có món, bữa, số lần), **Lịch sử nấu** (50 mẻ gần nhất – bấm mẻ → trang mẻ). "← Danh sách" quay lại đúng bộ lọc / trang / vị trí cuộn; Ctrl + bấm hoặc chuột giữa → mở tab mới; tab chung **Chứng từ liên quan / Tài liệu / Lịch sử**. Quyền = quyền xem màn danh sách. Chế độ DEMO vẫn mở ngăn chi tiết.

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

### 4.4 Thực đơn tuần – `menu-menu` (UI-6.11, D-89)
Mỗi tuần **một thực đơn chung** cho mọi khách (D-90). **Vòng tuần:**
1. Thứ Hai 06:00 hệ thống tự lập bản **Nháp** tuần sau.
2. 08:00 bot báo bếp / kế hoạch số ô cần xem.
3. Bếp sửa các ô được đánh dấu → **Gửi duyệt** → COO **Duyệt** → **Công bố** trước thứ Năm.
4. Gửi phiếu đăng ký cho khách (màn Đăng ký suất tuần).
5. Hết Chủ nhật thực đơn tự hết hiệu lực. Đổi món sau công bố: **Yêu cầu đổi món** (mục 4.6).

**Đầu trang:**
- Tuần ◀ ▶ (mặc định tuần sau); trạng thái + mã thực đơn + **hạn công bố (thứ Năm)**. Không có ô chọn thực đơn.
- **4 bước**, bước xong có ✓:
  - ① **Lập thực đơn** – nút *Tạo thực đơn tuần* khi tuần chưa có.
  - ② **Hoàn thiện món** – số ô trống / thiếu công thức / suất FC vượt / trùng món + nút **Đến ô cần xử lý** (nhảy lần lượt).
  - ③ **Gửi duyệt** – khóa khi còn ô trống / món thiếu công thức / FC vượt; trùng món chỉ cảnh báo. Người có quyền duyệt thấy nút **Duyệt**.
  - ④ **Công bố & gửi phiếu** – nút **Công bố** (người duyệt) / **Gửi phiếu đăng ký** (mở màn Đăng ký suất tuần).
- **4 thẻ:** Ô còn trống · Món thiếu công thức · FC% TB tuần (mục tiêu ≤ `KpiIngredientToRevenueMaxPct` 55 %) · Khách áp dụng (khách có hợp đồng hiệu lực trong tuần).

**Tạo thực đơn tuần** (nút bước ①):
- **Nhân bản tuần cùng vị trí chu kỳ 4 tuần** (như hệ thống tự làm: tuần N − 4; thiếu thì lấy tuần gần nhất có món).
- **Nhân bản tuần chọn**.
- **Khung trống mẫu chuẩn**: Sáng 3 lựa chọn × Món chính – Ăn kèm – Đồ uống; Trưa / Tối Món chính 1 – Món chính 2 – Món phụ – Rau – Canh – Cơm – Tráng miệng; Đêm Món chính – Đồ uống.
- *Tải Excel theo mẫu sheet "1. Menu"* **chưa nâng cấp**.

**Tab Lịch tuần** – một màn đủ 4 buổi:
- **Buổi** (bấm để mở / gập; giá bán bình quân, số lựa chọn) → dòng **lựa chọn** (giá vốn / suất và **FC%** từng ngày: xanh đạt, đỏ vượt) → **nhóm món** × 7 ngày, mỗi ô một món.
- **Màu ô:** nét đứt = ô trống · cam = món chưa có công thức khóa · đỏ (dòng FC) = suất FC vượt · xanh dương = món chính trùng trong 7 ngày (gồm cuối tuần trước) · vạch cam = ô vừa sửa.
- **Sửa ô tại chỗ (không hộp thoại):**
  - bấm ô → hộp chọn có **5 món gợi ý**, xếp theo: cùng nhóm món → có công thức khóa → chưa dùng trong 14 ngày (`MenuNoRepeatDays`) → giữ FC% trong mục tiêu;
  - mỗi gợi ý hiện giá vốn + **FC% mới nếu chọn**;
  - gõ để tìm món khác; ↑ ↓ chọn; **Enter** lưu và sang ô ngày kế tiếp; **Delete** (ô tìm trống) xóa món; **Esc** đóng.
- **⋯** cuối hàng = **Điền cả hàng** (7 ngày cùng món); **⧉** ở đầu cột ngày = **Chép ngày** (mọi buổi sang ngày khác, ngày đích bị thay).
- Mỗi ô lưu ngay (Nháp), FC% tính lại tức thì.
- Đã duyệt / công bố → chỉ đọc.

**Tab Duyệt & lịch sử:** ai tạo / sửa / duyệt / công bố, lúc nào.

**Giá vốn món** = giá vốn chuẩn của món (`Dish.StandardCost`) × số phần; món chưa có giá vốn chuẩn không tính FC. **Giá bán** = đơn giá loại suất bình quân các hợp đồng hiệu lực trong tuần.

**Quyền:** Kế hoạch (OPS-PLAN) lập / sửa; bếp, KD xem; COO duyệt / công bố.

### 4.5 Thực đơn riêng cho khách – đã bỏ (UI-6.12, D-90)
- **Không còn thực đơn riêng theo khách**: mọi khách dùng **thực đơn chung Đã công bố** của tuần. Mục menu "Thực đơn riêng cho khách" đã ẩn; dữ liệu phân công cũ giữ lại nhưng không dùng.
- Khách cần đổi món cho một đơn / ngày / ca: dùng **Yêu cầu đổi món** (4.6).

### 4.6 Yêu cầu đổi món – `menu-change-request`
1. **Tạo yêu cầu đổi món** → hộp nhập:
   - **Đơn hàng** (trống = áp dụng cho mọi đơn của ngày / ca), **Món hiện tại***, **Món thay thế***, **Ngày ăn*** (tự lấy theo đơn nếu chọn đơn), Ca, **Số suất đổi*** (trống = toàn bộ suất của đơn khi có chọn đơn), Thực đơn, **Lý do*** (≥ 3 ký tự).
2. **Tạo & gửi duyệt** → yêu cầu **Chờ duyệt**; kết quả: Đã duyệt / Từ chối / Đã hủy.
- Lỗi: *Món thay thế phải khác món hiện tại*, *Yêu cầu không theo đơn cần có ngày ăn và số suất đổi*, *Đơn … đang … – chỉ đổi món cho đơn chưa giao / chưa hủy*, *Món cũ không có trong thực đơn của đơn…*.

### 4.7 Kế hoạch sản xuất – `prod-plan`
**Tự lập, tự phát hành (BIZ-2.4):** chốt đơn (job 20:00 hoặc nút **Chốt đơn theo ngày**) → hệ thống tự **lập kế hoạch → duyệt → phát hành** mọi ca có nhu cầu (lịch ngược, mẻ, giữ chỗ như phát hành tay) và tạo **phiếu xuất Nháp theo ca** cho kho (BIZ-2.5). **Không tự phát hành** (kế hoạch giữ Nháp, bot 🔴 báo Kế hoạch + COO kèm lý do) khi: kế hoạch đã **sửa tay**, món **chưa có công thức khóa**, **thiếu nguyên liệu** sau giữ chỗ, mẻ **không kịp giờ xuất bếp**. Kế hoạch đã duyệt / phát hành tay trước đó → hệ thống không lập lại. Ghi chú kế hoạch có dòng "Hệ thống tự …".
**Đơn chốt muộn (D-78):** đơn được duyệt sửa / thêm sau giờ chốt hoặc đơn bù khi kế hoạch đã phát hành → hệ thống tự sinh **lệnh + mẻ bổ sung** gắn kế hoạch đó (nhãn 🔴 *Bổ sung sau chốt* / *Đơn bù*, xếp đầu ô trên bảng điều độ, không kịp lịch → bắt đầu ngay), giữ chỗ nguyên liệu và tạo phiếu xuất Nháp; bot báo Kế hoạch, bếp, điều phối.

**Lập kế hoạch**
1. **Lập từ đơn hàng** → chọn **Ngày sản xuất***, Đơn vị, Ghi chú → **Lập kế hoạch**.
2. Hệ thống tổng hợp số phần từng món từ đơn **Đã xác nhận / Đã chốt** của ngày → kế hoạch **Nháp** `KS-…` (1 kế hoạch / ngày). Bấm lại sẽ tính lại kế hoạch Nháp; kế hoạch đã duyệt không đổi.
   - Kế hoạch cũng được tạo khi bộ phận mua hàng / job 20:30 **Tính nhu cầu** (MRP).

**Duyệt:** dòng Nháp có **Duyệt** → xác nhận. Cần quyền "Phê duyệt › Kế hoạch sản xuất" (mặc định COO / CEO).

**Ra lệnh sản xuất:** dòng **Đã duyệt** chưa có lệnh → **Ra lệnh SX** → xác nhận → hệ thống sinh **lệnh sản xuất** + **mẻ nấu** theo ca và dây chuyền (lịch ngược từ giờ xuất bếp), chụp định lượng công thức và **giữ chỗ nguyên liệu** → chuyển sang màn Lệnh sản xuất. Đơn hàng của ngày → **Đang sản xuất**.

**Xuất phiếu sản xuất:** nút xuất trên mỗi dòng → tải phiếu sản xuất.

Danh sách: Số kế hoạch, Ngày SX, Ca, Đơn vị, Số món, Tổng suất, Số lệnh SX, Trạng thái.

**Trang chi tiết (UI-6):** bấm 1 kế hoạch → trang `#/prod-plan-detail?id=…`: thanh bước Nháp → Đã duyệt → Đã phát hành → Hoàn thành; nút **Duyệt kế hoạch** (Nháp, người có quyền Duyệt), **Ra lệnh SX** (Đã duyệt), **Phiếu sản xuất**; 4 chỉ số Tổng suất, Ca đã phát hành, Lệnh SX (mẻ đạt), Giữ chỗ thiếu. Tổng quan có khối **Theo ca** và **Món & suất**; tab **Lệnh sản xuất**, **Mẻ** (bấm dòng → trang lệnh / mẻ), **Giữ chỗ nguyên liệu**. Kế hoạch hệ thống giữ Nháp *chờ COO duyệt* có tag **Chờ COO duyệt**, lý do, và tab **Duyệt**. "← Danh sách" quay lại đúng bộ lọc / trang / vị trí cuộn; Ctrl + bấm hoặc chuột giữa → mở tab mới; tab chung **Chứng từ liên quan / Tài liệu / Lịch sử**. Quyền = quyền xem màn danh sách. Chế độ DEMO vẫn mở ngăn chi tiết.

### 4.8 Lệnh sản xuất – `prod-order`
Bấm một lệnh → ngăn chi tiết:
- Tab **Mẻ & nguyên liệu**: mẻ (số, kế hoạch, thực tế, bắt đầu, trạng thái, QC), định lượng lúc phát hành, giữ chỗ nguyên liệu (cần / đã giữ / thiếu), phiếu xuất.
- Tab **Giá thành & chênh lệch**: NVL, nhân công, chi phí chung, hao hụt, khác, tổng, giá thành / phần so với chuẩn; chênh lệch tiêu hao từng nguyên liệu.
- Nút:
  - **Tính giá thành** (lệnh Đang SX / Hoàn thành / Đóng) – ghi đè lần tính trước.
  - **Đóng lệnh** (lệnh Hoàn thành) – tính chênh lệch tiêu hao, giá thành, giải phóng giữ chỗ dư; không mở lại được.
  - **Hủy lệnh** (Kế hoạch / Phát hành, chưa mẻ nào bắt đầu) – bắt buộc lý do ≥ 3 ký tự; mẻ + phiếu xuất nháp bị hủy, giữ chỗ được giải phóng.
- Lỗi: *Lệnh … đã có mẻ bắt đầu nấu – không hủy được (mẻ không đạt thì dùng Loại mẻ)*, *Lệnh … đã xuất kho – hủy phiếu xuất trước khi hủy lệnh*.

**Trang chi tiết (UI-6):** chế độ dữ liệu thật, bấm 1 lệnh → trang `#/prod-order-detail?id=…` thay ngăn chi tiết: thanh bước Kế hoạch → Phát hành → Đang SX → Hoàn thành → Đóng; cùng 3 nút trên; 4 chỉ số Mẻ đạt, Suất đạt, Giữ chỗ thiếu, Giá thành / phần (đỏ khi cao hơn chuẩn); Tổng quan có bảng mẻ (bấm → trang mẻ); 2 tab như trên. "← Danh sách" quay lại đúng bộ lọc / trang / vị trí cuộn; Ctrl + bấm hoặc chuột giữa → mở tab mới; tab chung **Chứng từ liên quan / Tài liệu / Lịch sử**. Quyền = quyền xem màn danh sách. Chế độ DEMO vẫn mở ngăn chi tiết.

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
