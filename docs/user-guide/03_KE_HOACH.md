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
| Kinh doanh › Đơn hàng theo ngày | `crm-order` | X |
| Kinh doanh › Dự báo suất ăn | `crm-forecast` | X |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | Q |
| Thực đơn & Món ăn › Công thức chế biến | `menu-recipe` | Q |
| Thực đơn & Món ăn › Món ăn | `menu-dish` | Q |
| Mua hàng › Tính nhu cầu nguyên liệu | `scm-requirement` | X |
| Mua hàng › Đề nghị mua hàng | `scm-pr` | X |
| Mua hàng › Nguyên liệu | `scm-ingredient` | S |
| Kho › Tồn kho | `scm-inv-balance` | X |
| Kho › Lô hàng & hạn dùng | `scm-lot` | X |
| Sản xuất › Kế hoạch sản xuất | `prod-plan` | S |
| Sản xuất › Mẻ nấu | `prod-batch` | X |
| Sản xuất › Hao hụt | `prod-waste` | X |
| Sản xuất › Dây chuyền & trạm bếp | `prod-line` | Q |
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
Danh sách: Mã, Món ăn, Nhóm món, Giá thành chuẩn, **Công thức đang dùng** (phiên bản + trạng thái, hoặc "Chưa có công thức hiệu lực"), CP NVL ước tính, Trạng thái.

**Màn Món ăn – nhóm món ở cột trái (UI-6.21, D-102):** mục menu "Nhóm món ăn" đã ẩn; cột trái nhóm món + số món, bấm để lọc, + Thêm nhóm, ⋯ Đổi tên / Ngừng dùng (khóa khi còn món đang dùng) / Dùng lại – như màn Nguyên liệu (xem 04_MUA_HANG.md 4.5). 4 thẻ: Món đang dùng · Chưa có công thức hiệu lực · Chưa phân nhóm · Số nhóm món.

**Form Món ăn (UI-6.17, D-98) – 5 ô:** **Tên món*** · **Nhóm món*** (chọn trong danh mục; link **+ Thêm nhóm món** cho người có quyền Thêm Nhóm món ăn – thêm xong tự chọn) · **Định lượng 1 suất** (số + đơn vị g / ml / cái…) · **Mô tả** · **Trạng thái** (Đang dùng / Ngừng dùng). Mã món hệ thống tự sinh (chỉ đọc). **Giá thành chuẩn chỉ đọc** – hệ thống tự tính khi công thức được duyệt. Phân loại và khẩu phần cũ không còn trên form (máy chủ tự ghi theo nhóm món / định lượng).

**Thêm món ăn – mục Công thức chế biến** (chọn 1): (1) **Chép từ món có sẵn** (mặc định) – chọn món nguồn (tìm theo tên / mã, cùng nhóm xếp đầu; mỗi món ghi "Vn đang dùng · N nguyên liệu"); (2) **Nhập mới** – bản nháp trống; (3) **Để sau** – món mang nhãn "Chưa có công thức hiệu lực" (Thực đơn tuần chưa chọn được món này). Nút **Lưu và mở công thức** (1, 2): tạo món → tạo phiên bản **V1 Nháp** (chép định lượng khi chọn 1) → mở trang món, tab **Công thức**. Lựa chọn 3: nút **Lưu**.

**Tab Công thức trên trang món (UI-6.17):** ô chọn phiên bản (Nháp / Chờ duyệt / Đang dùng / bản cũ); bản **Nháp** sửa ngay trên bảng: Nguyên liệu · **Định lượng tinh / 100 suất** · ĐVT · **Hao hụt sơ chế %** · Lượng cần mua (tự tính) · Giá NCC ưu tiên · Thành tiền / 100 suất · ✕ xóa dòng; dòng cuối **+ Gõ tên nguyên liệu để thêm** (không có → **+ Tạo nguyên liệu mới** nếu có quyền); Enter xuống dòng cùng cột. 4 thẻ: Giá vốn NL / suất · Số nguyên liệu · Thành phẩm / suất · So với bản đang dùng (± %); dòng tự kiểm tra (đủ giá NCC, đơn vị, hướng dẫn chế biến – không bắt buộc). Nút: **Lưu nháp · Gửi duyệt** (Nháp); **Duyệt / Từ chối** (người duyệt Công thức, không tự duyệt bản mình gửi); **Khóa** (bản Đã duyệt); **Tạo phiên bản mới** (chép bản đang xem → Nháp). Dùng chung dữ liệu với màn Công thức chế biến (sửa ở đâu nơi kia thấy ngay; màn Công thức có link **Mở trang món**). Nút **Công thức** ở phần đầu trang món chuyển sang tab này.

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
- **Tải Excel** (thực đơn Nháp): trên Lịch tuần bấm **Tải mẫu Excel** (sheet "1. Menu": Buổi · Lựa chọn · Nhóm món · T2…CN, điền sẵn món đang có) → ghi tên món → **Tải Excel lên** → hộp xem trước: ô khớp tên món (không phân biệt dấu / hoa thường) màu xanh, ô chưa khớp màu đỏ kèm 3 món gần giống để chọn hoặc bỏ qua → **Ghi vào thực đơn**. Ô trống trong tệp giữ món đang có. Cần quyền Nhập.

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

### 4.6 Đổi món – trên màn Thực đơn tuần (UI-6.13, D-91)
Mục menu "Yêu cầu đổi món" **đã ẩn**; link cũ mở màn **Thực đơn tuần**, tab **Đổi món**.

**Đổi cho mọi khách (3 thao tác):** thực đơn **Đã công bố / Đã duyệt** → bấm ô món → hộp **Đổi món ngày này** ngay dưới ô:
1. **Chọn món thay:** 5 gợi ý (cùng nhóm → có công thức khóa → chưa dùng 14 ngày → giữ FC; hiện giá vốn + FC% mới), gõ để tìm món khác.
2. **Lý do*:** Thiếu nguyên liệu / Giá tăng / NCC giao lỗi / Chất lượng nguyên liệu / Khác + ghi chú.
3. **Gửi.**

Hệ thống tự xác định ngày, buổi, lựa chọn và **mọi đơn của mọi khách** dùng thực đơn đó. Trước khi gửi hiện **"Ảnh hưởng N khách · M suất"**; không chọn đơn, không nhập số suất.

**Tự duyệt** khi đủ cả 4 điều kiện:
- món thay **cùng nhóm món**;
- **có công thức khóa**;
- **FC% suất không vượt mục tiêu**;
- **trước giờ chốt** (20:00 hôm trước).

Đủ điều kiện → ghi "Hệ thống tự duyệt", áp ngay. Thiếu điều kiện → hộp ghi rõ lý do, yêu cầu **Chờ duyệt** ở **Phê duyệt › Yêu cầu đổi món** (COO).

**Sau khi duyệt:**
- ô thực đơn hiện **món mới + nhãn "đã đổi"** (rê chuột: món cũ, số yêu cầu, lý do); ô đang chờ có nhãn "chờ duyệt đổi";
- kế hoạch sản xuất ngày đó còn Nháp → tự chuyển món trong kế hoạch, mọi lần tính lại nhu cầu dùng món mới;
- kế hoạch đã phát hành → bếp xử lý tay theo chi tiết yêu cầu.

**Khách xin đổi riêng (KD, trên trang chi tiết đơn):** nút **Đổi món** → chọn **món cần đổi** (món của đơn theo buổi / lựa chọn) → chọn **món thay** (5 gợi ý) → **số suất** (mặc định cả lựa chọn; sửa nếu đổi một phần) → **nguồn** (Điện thoại / Email / Zalo / Trực tiếp) + ghi chú → tùy chọn **Áp cho các đơn của khách từ … đến …** (hệ thống tìm mọi đơn còn mở của khách có món đó, mỗi đơn sửa được món thay) → **Gửi**. Mỗi đơn một yêu cầu; tự duyệt từng ô theo 4 điều kiện trên, còn lại COO duyệt. Quyền: Sửa màn Đơn đặt suất.

**Tab Đổi món:** mọi yêu cầu có ngày ăn trong tuần (kể cả yêu cầu cũ theo đơn): số, ngày ăn, món cũ → món mới, phạm vi (Mọi khách / Khách X), số suất, lý do, trạng thái, người duyệt ("Hệ thống tự duyệt").

**Quyền:** người có quyền Thêm ở màn Thực đơn tuần (Kế hoạch – OPS-PLAN); duyệt: COO.

### 4.7 Kế hoạch sản xuất – `prod-plan`
**Tự lập, tự phát hành (BIZ-2.4):** chốt đơn (job 20:00 hoặc nút **Chốt đơn theo ngày**) → hệ thống tự **lập kế hoạch → duyệt → phát hành** mọi ca có nhu cầu (lịch ngược, mẻ, giữ chỗ như phát hành tay) và tạo **phiếu xuất Nháp theo ca** cho kho (BIZ-2.5). **Không tự phát hành** (kế hoạch giữ Nháp, bot 🔴 báo Kế hoạch + COO kèm lý do) khi: kế hoạch đã **sửa tay**, món **chưa có công thức khóa**, **thiếu nguyên liệu** sau giữ chỗ, mẻ **không kịp giờ xuất bếp**. Kế hoạch đã duyệt / phát hành tay trước đó → hệ thống không lập lại. Ghi chú kế hoạch có dòng "Hệ thống tự …".
**Đơn chốt muộn (D-78):** đơn được duyệt sửa / thêm sau giờ chốt hoặc đơn bù khi kế hoạch đã phát hành → hệ thống tự sinh **lệnh + mẻ bổ sung** gắn kế hoạch đó (nhãn 🔴 *Bổ sung sau chốt* / *Đơn bù*, xếp đầu ô trên bảng điều độ, không kịp lịch → bắt đầu ngay), giữ chỗ nguyên liệu và tạo phiếu xuất Nháp; bot báo Kế hoạch, bếp, điều phối.

**Màn "7 ngày tới" (UI-6.23, D-104) – mở mặc định.** **Một kế hoạch mỗi ngày sản xuất** (không tách theo đơn vị – trước đây chọn Đơn vị sinh kế hoạch thứ 2 → nấu gấp đôi). Hệ thống tự lập, tự duyệt và ra lệnh sau chốt đơn; màn này chỉ cho thấy **ngày nào cần người bấm**.
- Đầu màn: kỳ 7 ngày từ hôm nay, ◀ ▶ đổi 7 ngày, nút **7 ngày tới** quay về. Dải 4 bước **Chốt đơn → Lập kế hoạch → Duyệt → Ra lệnh SX theo ca**, mỗi bước ghi số việc và có nút cho ngày sớm nhất cần xử lý. 4 chỉ số: Suất đã chốt (7 ngày) · Suất chưa chốt · Ngày cần xử lý · Ca đã ra lệnh SX (x / y).
- Bảng **ngày × dòng**: tiêu đề cột = ngày + nhãn (Đã ra lệnh · Đã duyệt · Nháp · Bị giữ lại · Chưa có kế hoạch · Chưa chốt đơn). Dòng **Việc cần làm** – mỗi ngày đúng 1 việc:

  | Ngày đang… | Nút | Khi nào phải bấm |
  |---|---|---|
  | Bị giữ lại (hệ thống không tự duyệt) | **Xem lý do** | Luôn – xem nguyên liệu thiếu / món thiếu công thức khóa, rồi xử lý hoặc **Vẫn duyệt** |
  | Nháp | **Tính lại** (chữ nhỏ "+N suất chốt sau lần lập") | Khi có đơn chốt thêm sau lần lập, hoặc kế hoạch lập trước giờ chốt |
  | Đã duyệt, còn ca chưa ra lệnh | **Ra lệnh n ca** | Khi sẵn sàng nấu (thường sau khi hàng về) |
  | Chưa có kế hoạch nhưng đã có đơn chốt | **Lập trước** (chữ nhỏ "Còn N suất chưa chốt") | Chỉ khi cần mua / chuẩn bị sớm hơn giờ chốt |
  | Đã ra lệnh đủ ca | **Xem** | Không cần làm gì |
  | Chưa có đơn chốt | chữ "Tự lập sau chốt 20:00" | Không cần làm gì |

  Nhóm dòng mở / gập: **Đơn hàng (suất)** – Đã chốt / Chưa chốt; **Kế hoạch theo ca (suất)** – mỗi ca một dòng, màu theo trạng thái ca (chú thích màu trên bảng); **Món · lệnh sản xuất** – số món, số lệnh SX. Cột cuối **Cả 7 ngày**. **Ô không sửa trực tiếp** (sửa tay làm hệ thống không tự ra lệnh – sửa ở trang chi tiết kế hoạch); bấm ô → **hộp chi tiết ngày** (số kế hoạch, suất, món, bảng ca; ngày bị giữ lại có lý do + bảng nguyên liệu thiếu: Cần · Giữ được · Thiếu · **Đang về** (đơn mua đang mở, ngày giao dự kiến) và nút **Mở tính nhu cầu nguyên liệu** · **Mở kế hoạch** · **Vẫn duyệt**).
- **Lập / Tính lại:** máy chủ tự quyết – mọi đơn của ngày đã chốt → chạy đúng luồng tự động sau chốt (đủ công thức và nguyên liệu thì tự duyệt + ra lệnh; không đủ thì giữ Nháp kèm lý do); ngày còn đơn chưa chốt → chỉ lập **Nháp** (kế hoạch tự tính lại khi chốt 20:00). Còn Nháp → mở trang chi tiết kế hoạch.
- **Vẫn duyệt** (quyền Duyệt "Phê duyệt › Kế hoạch sản xuất", mặc định COO / CEO): duyệt khi thiếu nguyên liệu – lệnh SX vẫn ra, phần thiếu kho giữ chỗ bổ sung khi hàng về.
- Quyền: Xem màn = xem 7 ngày tới + hộp chi tiết; **Lập / Tính lại**, **Ra lệnh** cần quyền Sửa `prod-plan` (OPS-PLAN); **Vẫn duyệt** cần quyền Duyệt (COO). Bếp (OPS-KIT) chỉ xem.
- Điện thoại: bảng cuộn ngang, cột tên dòng đứng yên.

**Tab Danh sách kế hoạch:** Số kế hoạch, Ngày SX, Số món, Tổng suất, Số lệnh SX, Trạng thái. **Không có nút Tạo mới / Lập từ đơn hàng** (kế hoạch do hệ thống lập, mỗi ngày một bản – lập sớm bằng **Lập trước** ở 7 ngày tới). Dòng Nháp có **Duyệt** (người có quyền Duyệt); dòng **Đã duyệt** (còn ca chưa ra lệnh) có **Ra lệnh SX** → hệ thống sinh **lệnh sản xuất** + **mẻ nấu** theo ca và dây chuyền (lịch ngược từ giờ xuất bếp), chụp định lượng công thức và **giữ chỗ nguyên liệu**. Đơn hàng của ngày → **Đang sản xuất**.

**Tab Nhật ký tự động:** các lần hệ thống tự lập / tự duyệt / ra lệnh / giữ lại trong 14 ngày (7 ngày trước + kỳ đang xem) – ngày, kế hoạch, trạng thái, việc hệ thống đã làm.

**Xuất phiếu sản xuất:** nút xuất trên mỗi dòng → tải phiếu sản xuất.

**Bảng sản xuất ngày (UI-6.24, D-105)** – trang chi tiết kế hoạch (mở từ màn 7 ngày tới / danh sách kế hoạch / nút **Bảng sản xuất ngày** ở Mẻ nấu): theo dõi một mạch **Kế hoạch → lệnh → mẻ** của cả ngày.
- Đầu trang: số kế hoạch, ngày, nhãn trạng thái; nút theo việc được phép: **Duyệt kế hoạch** · **Ra lệnh n ca** · **Phiếu sản xuất**. 4 chỉ số: Suất kế hoạch · Mẻ xong / tổng · Mẻ chờ QC / bị loại · Lệnh đã đóng / tổng.
- **Ma trận Món × Ca** (nhóm theo nhóm món, mở / gập, có dòng tổng; cột cuối Cả ngày): mỗi ô = số suất của món trong ca, chữ nhỏ "x/y mẻ xong"; màu: xám gạch = kế hoạch chưa ra lệnh · xanh dương = chờ nấu · cam = đang nấu · vàng = chờ QC · xanh lá = đạt / đã đóng gói · đỏ = có mẻ loại hoặc tạm giữ. Rê chuột: số lệnh, dây chuyền, giờ bắt đầu – xong dự kiến. **Ô không sửa trực tiếp.**
- Bấm ô → **hộp chi tiết lệnh** (món × ca): bảng mẻ (bấm số mẻ mở trang mẻ), giữ chỗ nguyên liệu (cần · đã giữ · thiếu), phiếu xuất, chênh lệch tiêu hao + giá thành khi lệnh đã đóng; nút **Giữ chỗ bổ sung** · **Tính giá thành** · **Hủy lệnh** (lý do) · **Mở trang lệnh**.
- Tab **Nguyên liệu & giữ chỗ** (gộp theo nguyên liệu cả ngày) · **Chênh lệch & giá thành** (theo lệnh) · Chứng từ liên quan · Tài liệu · Lịch sử.
- **Không tạo lệnh sản xuất tay:** lệnh + mẻ chỉ sinh khi **Ra lệnh** kế hoạch (mỗi món × ca = 1 lệnh); ngoại lệ có sẵn: đơn chốt muộn → lệnh bổ sung, **Nấu thêm** ở mẻ.
- **Lệnh tự đóng:** mẻ cuối của lệnh đóng gói / loại → lệnh Hoàn thành → hệ thống **tự Đóng** (chênh lệch tiêu hao, giá thành thực tế, trả nguyên liệu giữ chỗ dư). Lỗi khi tự đóng không làm hỏng thao tác mẻ – job **"Đóng lệnh sản xuất đã xong"** 22:00 đóng lại các lệnh còn sót và gửi **1 tin** chênh lệch vượt ngưỡng của ngày cho Bếp trưởng + COO. Tắt tự động (cấu hình `ProductionAutoCloseOrder`) thì dùng nút **Đóng lệnh** ở trang lệnh.

**Trang chi tiết (UI-6):** bấm 1 kế hoạch → trang `#/prod-plan-detail?id=…`: thanh bước Nháp → Đã duyệt → Đã phát hành → Hoàn thành; nút **Duyệt kế hoạch** (Nháp, người có quyền Duyệt), **Ra lệnh SX** (Đã duyệt), **Phiếu sản xuất**; 4 chỉ số Tổng suất, Ca đã phát hành, Lệnh SX (mẻ đạt), Giữ chỗ thiếu. Tổng quan có khối **Theo ca** và **Món & suất**; tab **Lệnh sản xuất**, **Mẻ** (bấm dòng → trang lệnh / mẻ), **Giữ chỗ nguyên liệu**. Kế hoạch hệ thống giữ Nháp *chờ COO duyệt* có tag **Chờ COO duyệt**, lý do, và tab **Duyệt**. "← Danh sách" quay lại đúng bộ lọc / trang / vị trí cuộn; Ctrl + bấm hoặc chuột giữa → mở tab mới; tab chung **Chứng từ liên quan / Tài liệu / Lịch sử**. Quyền = quyền xem màn danh sách. Chế độ DEMO vẫn mở ngăn chi tiết.

### 4.8 Lệnh sản xuất – trang lệnh (mục menu đã ẩn – UI-6.24)
Không còn mục menu riêng: mở lệnh từ **Bảng sản xuất ngày** (bấm ô → Mở trang lệnh), từ mẻ hoặc tìm kiếm; link cũ `#/prod-order?id=` mở trang lệnh, `#/prod-order` mở Kế hoạch sản xuất. Không có nút Tạo mới / Sửa chung – chỉ **Người phụ trách** và **Ghi chú** sửa tại chỗ (link **Sửa** cạnh 2 ô này, ô chọn nhân viên có tìm). Trang lệnh:
- Tab **Mẻ & nguyên liệu**: mẻ (số, kế hoạch, thực tế, bắt đầu, trạng thái, QC), định lượng lúc phát hành, giữ chỗ nguyên liệu (cần / đã giữ / thiếu), phiếu xuất.
- Tab **Giá thành & chênh lệch**: NVL, nhân công, chi phí chung, hao hụt, khác, tổng, giá thành / phần so với chuẩn; chênh lệch tiêu hao từng nguyên liệu.
- Nút:
  - **Tính giá thành** (lệnh Đang SX / Hoàn thành / Đóng) – ghi đè lần tính trước.
  - **Đóng lệnh** (lệnh Hoàn thành – chỉ khi tắt tự đóng hoặc tự đóng lỗi) – tính chênh lệch tiêu hao, giá thành, giải phóng giữ chỗ dư; không mở lại được. Bình thường hệ thống **tự đóng** khi mẻ cuối xong.
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
