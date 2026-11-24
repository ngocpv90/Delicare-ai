# 09 – Giao vận (điều phối, tài xế, thu hồi khay)

## 1. Bạn là ai

- Nhóm quyền: **OPS-LOG** (1.4 Điều phối giao nhận – điều phối viên, tài xế).
- Bot dùng: **Vận hành** (phân hệ 🚚 Giao vận) – **kênh chính của tài xế**; QR dán trên xe. Trưởng điều phối được cấp thêm bot **Điều hành**.
- Việc chính: lập đơn giao, lập chuyến nhiều xe theo khu vực, sắp điểm dừng, xếp hàng lên xe, xuất bến (đo nhiệt độ ≥ 60 °C), đến điểm, bàn giao + POD / giao thất bại, thu hồi khay hộp, sổ bao bì, phản ánh về giao hàng.

## 2. Màn hình được dùng

<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->
Quyền theo ma trận D-79 (đủ 19 nhóm: `docs/claude/MENU5_MATRIX.md`).

| Menu › Mục | screen-id | OPS-LOG |
|---|---|---|
| Việc của tôi › Việc của tôi | `hr-my` | X |
| Kinh doanh › Đơn hàng theo ngày | `crm-order` | X |
| Kinh doanh › Điểm giao hàng | `crm-location` | X |
| Sản xuất › Kế hoạch sản xuất | `prod-plan` | X |
| Chất lượng & ATTP › Nhật ký nhiệt độ | `qc-temperature` | X |
| Đóng gói & Giao hàng › Lệnh đóng gói | `log-packing-order` | X |
| Đóng gói & Giao hàng › Kết quả đóng gói | `log-packing-result` | X |
| Đóng gói & Giao hàng › Đơn giao hàng | `log-delivery-order` | S |
| Đóng gói & Giao hàng › Chuyến giao hàng | `log-route` | S |
| Đóng gói & Giao hàng › Xếp hàng lên xe | `log-loading` | S |
| Đóng gói & Giao hàng › Theo dõi chuyến giao | `log-execution` | S |
| Đóng gói & Giao hàng › Vị trí xe | `log-gps` | X |
| Đóng gói & Giao hàng › Xác nhận đã giao | `log-pod` | S |
| Đóng gói & Giao hàng › Thu hồi khay hộp | `log-container-return` | S |
| Đóng gói & Giao hàng › Khay hộp theo khách | `log-packaging-ledger` | X |
| Đóng gói & Giao hàng › Xe giao hàng | `log-vehicle` | Q |
<!-- MENU5:END -->

## 3. Việc trong ngày

| Thời điểm | Việc | Màn |
|---|---|---|
| Chiều D-1 / sáng D | Lập chuyến theo khu vực cho đơn Đang sản xuất, kiểm tải xe | `log-route` |
| Trước giờ chạy | Sắp điểm dừng, giờ đến dự kiến; in phiếu giao | `log-route` |
| Mẻ đóng gói xong | **Xếp hàng lên xe** | `log-route` |
| Giờ xuất bếp | **Xuất bến** – đo nhiệt độ thức ăn ≥ 60 °C | `log-route` / bot |
| Trên đường | Tài xế: 📍 Đã đến → ✅ Giao (số suất, người nhận, nhiệt độ) hoặc ❌ Không giao được | bot / `log-execution` |
| Về bếp | 📥 Nhận hộp về / Ghi thu hồi | bot / `log-container-return` |

## 4. Thao tác từng bước

### 4.1 Đơn giao – `log-delivery-order` (hoặc khối "Đơn giao trong ngày" ở `log-route`)
**Lập đơn giao**
1. **Lập đơn giao** → chọn **Đơn hàng*** (đơn Đã chốt / Đang sản xuất của ngày), Điểm giao (trống = điểm giao của đơn), **Số suất** (trống = toàn bộ suất còn lại), Giờ giao dự kiến, Ghi chú.
2. **Lập đơn giao** → đơn giao `GH-…` ở **Kế hoạch**, chưa gán chuyến. (Lập chuyến theo khu vực tự tạo đơn giao cho đơn hàng chưa có.)

**Trên dòng đơn giao Kế hoạch, chưa xếp lên xe:**

| Nút | Thao tác |
|---|---|
| **Gán chuyến** / **Chuyển chuyến** | Chọn **Chuyến xe*** (chuyến Kế hoạch cùng ngày), Thứ tự điểm dừng (trống = cuối chuyến) |
| **Tách** | Nhập ≥ 2 phần (số suất + giờ giao từng phần, **Thêm phần** để thêm); tổng = số suất đơn giao. Phần 1 giữ số phiếu cũ, phần sau thành đơn giao mới chưa gán chuyến. Không gộp lại được |
| **Hủy** | Bắt buộc lý do; gỡ khỏi chuyến, số suất trả lại đơn hàng |

### 4.2 Lập chuyến – `log-route`
Chọn **Ngày giao** (chip hoặc ô ngày).

**Lập chuyến theo khu vực** (cách nhanh):
1. Tick các **Xe** sẵn sàng (biển số · loại · sức chở) và chọn **Tài xế** cho từng xe.
2. **Lập chuyến** → hệ thống gom đơn Đang sản xuất + đơn giao chưa gán của ngày theo **khu vực**, chia cho xe theo tải còn trống; mỗi xe có hàng = 1 chuyến `CX-…`.
- *{đơn} (… suất) không vừa tải còn trống của xe nào – xếp vào xe … (vượt tải)*.

**Lập chuyến** (tay):
1. Chọn **Xe***, Tài xế, Giờ xuất phát / kết thúc dự kiến, Tên chuyến.
2. Tick **Đơn hàng chờ xếp chuyến** và / hoặc **Đơn giao chưa gán chuyến** (không tick = lấy mọi đơn hàng chờ của ngày).
3. **Lập chuyến**.

**Thẻ chuyến** (mỗi chuyến): số chuyến, tên, xe, tài xế, **Tải trọng N / sức chở suất** (thanh đỏ khi vượt), dòng thời gian *Bếp trung tâm (điểm đầu) → các điểm dừng (suất, giờ đến KH, trạng thái)*; dòng đỏ **"Chưa đủ điều kiện xuất bến: …"** nếu còn mẻ chưa đóng gói / chưa lưu mẫu / chưa xếp hàng.

| Trạng thái chuyến | Nút |
|---|---|
| Kế hoạch | **Xếp hàng lên xe**, **Sắp điểm dừng**, Chi tiết, In phiếu giao (PDF), Biên bản giao hàng (Excel) |
| Đang xếp | **Xuất bến**, Chi tiết, In… |

- **Sắp điểm dừng**: sửa **Thứ tự** (số nguyên từ 1, không trùng) và **Giờ đến dự kiến** từng điểm → **Lưu thứ tự**.
- **Xếp hàng lên xe**: ghi xếp hàng cho mọi đơn giao của chuyến (số suất = kế hoạch) → chuyến **Đang xếp**.
- **Xuất bến**: nhập **Nhiệt độ thùng giữ nhiệt °C** và **Hành động khắc phục** (bắt buộc nếu dưới 60 °C) → **Xác nhận** → chuyến **Đang giao**.
- **In phiếu giao (PDF)**: một tệp gồm mọi phiếu giao của chuyến (mỗi phiếu một trang, có QR). **Biên bản giao hàng (Excel)**: biên bản cả chuyến.

**Trang chi tiết chuyến (UI-6):** nút **Chi tiết** trên thẻ chuyến → trang `#/log-route-detail?id=…`: thanh bước Kế hoạch → Đang xếp → Đang giao → Hoàn thành; cùng các nút của thẻ chuyến (Xếp hàng lên xe, Xuất bến, Sắp điểm dừng, Ghi vị trí hiện tại, In phiếu giao, Biên bản giao hàng); 4 chỉ số Điểm đã giao, Suất đã giao, Tải xe %, Phản ánh (đang mở). Tổng quan: xe, tài xế, giờ, nhiệt độ xuất bến so ngưỡng, điều kiện xuất bến còn thiếu, dòng thời gian điểm giao. Tab **Điểm giao & POD**, **Sự kiện & nhiệt độ**, **Mẻ phục vụ** (bấm → trang mẻ), **Phản ánh**. "← Danh sách" quay lại đúng bộ lọc / trang / vị trí cuộn; Ctrl + bấm hoặc chuột giữa → mở tab mới; tab chung **Chứng từ liên quan / Tài liệu / Lịch sử**. Quyền = quyền xem màn danh sách. Chế độ DEMO vẫn mở ngăn chi tiết.

### 4.3 Thực hiện giao – `log-execution`
1. Chọn ngày. Thẻ đếm: Chờ xếp hàng / Đã xếp / Đã xuất phát / Đã đến / Đã giao / Thất bại.
2. Mỗi đơn giao: số phiếu · điểm giao, khung giờ nhận, chuyến, khách · số suất · xe · tài xế; thanh tiến trình.

| Trạng thái | Nút | Thao tác |
|---|---|---|
| Đã xuất phát | **Đã đến điểm giao** | Ghi đến (kèm tọa độ điểm giao) |
| Đã đến | **Bàn giao & ghi POD** | SL giao, **SL khách nhận** (≤ SL giao), **Người nhận***, Ghi chú, **Nhiệt độ khi giao °C**, Khắc phục (nếu dưới ngưỡng) → **Xác nhận POD** |
| Đã đến | **Giao thất bại** | Bắt buộc lý do → cần điều phối lại |
| Mọi lúc | **Sự kiện**, **In phiếu**, **In POD** (khi đã có POD) | |
| Chuyến Đang xếp / Đang giao | **Ghi vị trí hiện tại** | Lấy vị trí thiết bị (trình duyệt phải cho phép định vị) |

- Bàn giao xong mọi đơn giao của đơn hàng → đơn hàng **Đã giao**. Khách từ chối bớt suất → hệ thống **tự tạo phản ánh** `PA-…` (thiếu suất) và ghi sổ bao bì OUT.

### 4.4 Vị trí xe – `log-gps`
Bảng **Vị trí xe theo sự kiện**: chuyến, xe, tài xế, sự kiện gần nhất (đến điểm, giao không thành công, tài xế gửi vị trí), lúc, tọa độ.

### 4.5 Thu hồi khay / hộp – `log-container-return`
1. **Ghi thu hồi** (hoặc **Ghi thu hồi** trên dòng Chờ thu hồi): chọn **Đơn giao đã giao***, Số phải thu (trống = số còn ở khách), **Số thu về***, Trong đó bị hỏng, Ngày thu hồi, Loại khay / hộp (trống = loại đã giao), Ghi chú → **Ghi thu hồi**.
2. **Kiểm tra** (dòng Đã thu hồi): **Kết quả kiểm tra*** (Đủ / Thiếu / Hỏng / Bẩn / Không đạt), Số hỏng, Số thiếu, Ghi chú (bắt buộc khi khác Đủ) → **Ghi kết quả kiểm tra**; khay dùng được tự vào mẻ vệ sinh.
- Cột: Đơn giao, Khách hàng, Loại khay / hộp, Ngày thu hồi, Phải thu, Thu về, Hỏng, Kết quả kiểm tra, Trạng thái.

### 4.6 Sổ bao bì – `log-packaging-ledger`
- Khối **Khay hộp còn ở khách**: theo khách × loại – Đã giao, Đã thu, Trong đó hỏng, Thất thoát, **Còn ở khách** (= giao − thu − thất thoát), **Tỷ lệ thất thoát** (đỏ khi vượt ngưỡng 3%).
- **Ghi sổ** (tay): Khách hàng*, **Loại ghi sổ*** (Giao cho khách / Khách trả lại / Thất thoát), Loại khay / hộp*, Số lượng*, Tình trạng (khi khách trả: Tốt / Hỏng), **Lý do*** → xác nhận. Sổ chỉ ghi thêm – ghi sai thì ghi dòng ngược lại.

### 4.7 Phản ánh – `log-complaint`
**Đơn bù (BIZ-3.1):** phản ánh loại **Thiếu suất / Hỏng – đổ vỡ / Không đạt nhiệt độ** có gắn đơn (kể cả phản ánh tự tạo khi khách từ chối suất lúc bàn giao) → nút **Tạo đơn bù** trên dòng: số suất (trống = số khách từ chối ở POD), tích **Khách đặt thêm – tính tiền** nếu không phải lỗi Delicare. Hệ thống tạo đơn **Đã chốt** cùng ngày ăn / điểm giao / loại suất (lựa chọn chia theo đơn gốc), **giá 0** khi lỗi Delicare, ghi chú `[DON_BU:<đơn gốc>|<PA>]`; phản ánh → Đang xử lý; bot 🔴 báo Kế hoạch + điều phối **nấu ưu tiên, giao bù**. Mỗi phản ánh 1 đơn bù. Giao bù xong → bấm **Đã xử lý** phản ánh. Kế hoạch ngày đó đã phát hành → hệ thống **tự sinh lệnh + mẻ bổ sung** nhãn 🔴 *Đơn bù* (nấu ưu tiên) và phiếu xuất Nháp cho kho (D-78).

**Trang chi tiết phản ánh (UI-6):** bấm 1 dòng phản ánh (ở danh sách, hồ sơ khách 360 hoặc trang đơn hàng) → **trang riêng** `#/log-complaint-detail?id=…` (Ctrl / chuột giữa = tab mới; **← Phản ánh** quay lại đúng bộ lọc / trang / vị trí cuộn). Phần đầu: số phản ánh, trạng thái, mức độ, "Quá hạn xử lý"; khách · phân loại · giờ tiếp nhận · kênh · người xử lý; thanh bước **Mới → Đang xử lý → Đã xử lý → Đóng** (Từ chối tô đỏ). Nút (cần quyền Sửa): **→ bước tiếp theo** (nhập nội dung xử lý), **Tạo đơn bù** (như nút trên dòng; cần quyền Thêm phản ánh và Thêm đơn đặt suất, chỉ khi chưa có đơn bù), **Thêm ghi chú**, **Sửa**, ⋯ **Từ chối phản ánh**, **Hồ sơ khách hàng**. 4 thẻ: **Còn lại / Quá hạn / Đã xử lý trong** (giờ so với hạn SLA), **Diễn biến xử lý**, **Đơn bù**, **Phản ánh khác đang mở** của khách. Tab **Tổng quan** (thông tin – bấm khách / đơn / đơn giao để mở; nội dung; kết quả xử lý; 5 diễn biến gần nhất; đơn bù) · **Diễn biến xử lý** (đủ, kèm tên người ghi) · **Tài liệu** · **Lịch sử**.

Xem `10_DIEM_PHAT.md` mục 4.3 (cùng màn).

## 5. Trạng thái

| Chứng từ | Luồng |
|---|---|
| Chuyến xe | Kế hoạch → Đang xếp → Đang giao → Hoàn thành |
| Đơn giao | Kế hoạch → Đang giao → Đã giao / Thất bại; Hủy |
| Điểm dừng | Kế hoạch → Đã đến → Đã giao / Thất bại |
| Thực hiện giao | Chờ xếp hàng → Đã xếp → Đã xuất phát → Đã đến → Đã giao / Thất bại |
| Thu hồi khay | Chờ thu hồi → Đã thu hồi → Đã kiểm tra |

## 6. Lỗi / cảnh báo thường gặp

| Thông báo | Cách xử lý |
|---|---|
| Chuyến … chưa được xuất phát: … | Xem danh sách lý do: mẻ chưa đóng gói, mẻ chưa lưu mẫu, chưa xếp hàng, chưa đo nhiệt độ |
| Mẻ chưa đóng gói xong: … / Mẻ chưa lưu mẫu thức ăn (bắt buộc trước khi giao): … | Báo bếp / tổ đóng gói |
| Chưa xếp hàng lên xe (DeliveryRoute/Load): … | Bấm **Xếp hàng lên xe** |
| Nhiệt độ … °C dưới ngưỡng giao 60 °C – bắt buộc ghi hành động khắc phục | Hâm lại / thay suất, đo lại; ghi khắc phục |
| Nhiệt độ bàn giao … dưới ngưỡng … – không bàn giao được; báo điều phối giao bù | Ghi **Giao thất bại**, điều phối giao bù |
| Tổng … suất vượt tải xe … | Đổi xe / tách đơn giao |
| Xe … đã có chuyến khác chưa hoàn thành trong ngày / Tài xế đã có chuyến trùng khung giờ | Chọn xe / tài xế khác |
| Xe … đang ở trạng thái … – không xếp chuyến được | Xe bảo trì / ngừng |
| Không có chuyến nào còn ở trạng thái Kế hoạch trong ngày … | Lập chuyến mới |
| Đơn giao … đã xếp lên xe – không … được | Không tách / hủy sau khi xếp |
| Tổng các phần phải bằng … suất | Sửa số khi tách |
| Bắt buộc nhập người nhận / SL nhận không được lớn hơn SL giao | Sửa POD |
| Đơn giao chưa có điểm dừng (DeliveryStop) trên tuyến | Gán đơn giao vào chuyến trước |
| Chỉ ghi vị trí khi chuyến đang xếp hàng / đang giao | |
| Chưa cho phép trình duyệt dùng vị trí của thiết bị | Cho phép định vị trong trình duyệt |
| Khách … chỉ còn … – không thu hồi … | Số thu về vượt số còn ở khách |

## 7. Trên bot Telegram

Phân hệ **🚚 Giao vận** (bot Vận hành):
- 🚚 **Chuyến của tôi** (`/chuyen`) – tài xế: chuyến hôm nay (chỉ 1 chuyến thì mở thẳng thẻ chuyến; chuyến trễ lên đầu). Trong chuyến:
  - **🚀 Xuất phát** – chọn nhiệt độ thức ăn (nút 60 / 62 / 65 / 68 / 70 / 75 °C bấm 1 chạm; **Khác…** = màn số); dưới ngưỡng phải xử lý trên web.
  - Mỗi điểm **📍 #n**: **📍 Đã đến**, **✅ Giao đủ N · t°** – **1 chạm** (số suất đã xếp, nhiệt độ bàn giao = lần đo tại điểm, chưa đo thì lấy lần đo lúc xuất phát; người nhận điền sẵn người nhận gần nhất tại điểm hoặc "Đại diện <khách>" – **👤 Người nhận khác…** để gõ tên); **✏️ Giao số khác** (thiếu / thừa – màn số); **❌ Không giao được** (chọn lý do hay gặp, ✏️ Khác… mới gõ); **🔄 Làm mới**. Quay lại thẻ chuyến bằng **◀ Quay lại** (không còn nút ⬅️ Chuyến riêng). Giao đủ nhanh: **⭐ Chuyến của tôi → điểm → ✅ Giao đủ N** (3 chạm).
  - **Vị trí trực tiếp** (chia sẻ Live Location trong Telegram): bot tự ghi **xuất phát** khi xe rời bếp và **đã đến** khi vào bán kính 150 m của điểm giao.
- 🗺 **Giao vận hôm nay** (`/giaovan`) – điều phối: mọi chuyến hôm nay.
- 📥 **Nhận hộp về** (`/hop`) – chọn điểm đã giao → số hộp / khay nhận về trên **màn số** (điền sẵn số phải thu, −10 / −1 / +1 / +10, ✅ Xác nhận).
- **QR trên xe** (`?start=VEH_<mã xe>`): chuyến hôm nay của xe + **🚀 Xuất phát**.
- **QR trên phiếu giao / POD**: mở đúng phiếu.
- Thông báo: 🔴 xe chưa rời bếp quá giờ, 🔴 điểm trễ, 🔴 nhiệt độ giao < 60 °C, 🔴 báo thiếu suất; tài xế 🟡 được phân / đổi chuyến.
