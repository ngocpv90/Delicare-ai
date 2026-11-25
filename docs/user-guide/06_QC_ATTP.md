# 06 – QC / An toàn thực phẩm

## 1. Bạn là ai

- Nhóm quyền: **QA** (Khối QA/QC và ATTP 4.1–4.5) và QA/QC bếp (**OPS-KIT-QA**, dùng nhóm **OPS-KIT**). QA được cấp thêm **"Phê duyệt › Kiểm tra hàng nhập"** (BIZ-3.7: mục này **đã ẩn** – không loại duyệt nào dùng; QC đầu vào kết luận ngay trên `qc-inspection`, Đạt / Đạt một phần tự ghi sổ).
- Bot dùng: **Vận hành** (phân hệ 🧽 ATTP / QC); trưởng QA được cấp thêm bot **Điều hành**.
- Việc chính: QC nguyên liệu đầu vào, sự cố NCC, checklist ATTP theo ca / mẻ, nhật ký nhiệt độ, QC thành phẩm (mẻ), vệ sinh khay / hộp 6 bước.

## 2. Màn hình được dùng

<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->
Quyền theo ma trận D-79 (đủ 19 nhóm: `docs/claude/MENU5_MATRIX.md`).

| Menu › Mục | screen-id | QA |
|---|---|---|
| Việc của tôi › Việc của tôi | `hr-my` | X |
| Phê duyệt › Kiểm tra hàng nhập | `wf-inbox?type=qc-inspection` | Duyệt |
| Phê duyệt › Mẻ nấu | `wf-inbox?type=prod-batch` | Duyệt |
| Phê duyệt › Giữ / thả lô | `wf-inbox?type=scm-lot` | Duyệt |
| Điều hành › Bảng điều hành | `dash-exec` | X |
| Điều hành › Chỉ số kinh doanh | `kpi-daily` | X |
| Kinh doanh › Phản ánh khách hàng | `log-complaint` | S (IX) |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | X |
| Thực đơn & Món ăn › Công thức chế biến | `menu-recipe` | X |
| Thực đơn & Món ăn › Món ăn | `menu-dish` | X |
| Mua hàng › Nhà cung cấp | `scm-supplier` | X |
| Mua hàng › Nguyên liệu | `scm-ingredient` | X |
| Kho › Nhận hàng | `scm-gr` | X |
| Kho › Tồn kho | `scm-inv-balance` | X |
| Sản xuất › Kế hoạch sản xuất | `prod-plan` | X |
| Sản xuất › Mẻ nấu | `prod-batch` | X |
| Chất lượng & ATTP › Sổ kiểm tra hàng nhập | `qc-inspection` | S (IX) |
| Chất lượng & ATTP › Kiểm tra theo ca / mẻ | `qc-checklist-result` | S (IX) |
| Chất lượng & ATTP › Nhật ký nhiệt độ | `qc-temperature` | S (IX) |
| Chất lượng & ATTP › Vệ sinh khay hộp | `qc-cleaning` | S (IX) |
| Chất lượng & ATTP › Sự cố nhà cung cấp | `qc-supplier-issue` | Q |
| Chất lượng & ATTP › Bộ kiểm tra ATTP | `qc-checklist` | Q |
| Đóng gói & Giao hàng › Đơn giao hàng | `log-delivery-order` | X |
| Đóng gói & Giao hàng › Theo dõi chuyến giao | `log-execution` | X |
<!-- MENU5:END -->

## 3. Việc trong ngày

| Khi nào | Việc | Màn |
|---|---|---|
| Hàng về | Kết luận QC đầu vào ngay sau khi kho tạo phiếu nhập | `qc-inspection` |
| Đầu / trong ca | Checklist ATTP theo ca (hạn làm hiện trên màn) | `qc-checklist-result` |
| Theo tần suất | Ghi nhiệt độ kho, tủ, khu chia suất | `qc-temperature` |
| Sau nấu | QC thành phẩm từng mẻ (sau khi bếp đo nhiệt độ lõi, ghi sản lượng) | `prod-batch` |
| Có sự cố NCC | Xử lý trong hạn (mặc định 2 ngày) | `qc-supplier-issue` |
| Khay thu hồi về | Kiểm tra → vệ sinh 6 bước → kết luận | `qc-cleaning` |

## 4. Thao tác từng bước

### 4.1 Kiểm tra hàng nhập – hàng chờ ở **Phê duyệt › Kiểm tra hàng nhập** + **Sổ kiểm tra hàng nhập** (`qc-inspection`, UI-6.29)
**Kết luận (QA):**
1. Mở **Phê duyệt › Kiểm tra hàng nhập** (`#/wf-inbox?type=qc-inspection`; sổ hiện khung vàng *N phiếu nhận hàng chờ kết luận → Mở hàng chờ*). Mỗi phiếu: số phiếu nhập, NCC, giờ nhận, nguyên liệu + vùng nhiệt độ.
2. **Kiểm tra & kết luận** → **cùng form Nhận hàng** đã điền sẵn số nhận, lô, hạn dùng, nhiệt độ do kho nhập. Chỉ sửa dòng có vấn đề: Nhiệt độ, Cảm quan, Bao bì, **Kết quả** (Đạt / Đạt một phần / Không đạt – điền sẵn theo gợi ý), Số đạt, Nhận xét (bắt buộc khi không Đạt).
3. **Kết luận & nhập kho** → hệ thống ghi sổ phần đạt, lập lô; dòng không đạt → tự lập **Sự cố NCC**.
- QA đứng ở cửa kho: mở **Nhận hàng** → form có nút **Kết luận & nhập kho** (tạo phiếu + kết luận một lần).

**Sổ kiểm tra hàng nhập (chỉ xem):** mỗi dòng = một nguyên liệu trong phiếu nhận, **gom theo ngày** (mở / gập; "N phiếu · M dòng · K không đạt"; nút **In ngày**). Cột Giờ · Nguyên liệu (bảo quản) · NCC (+ số phiếu) · Lô kho · Hạn dùng · Nhận / đạt · Nhiệt độ (màu theo vùng + ngưỡng) · Cảm quan / bao bì · Kết quả · Người kiểm (hoặc **Tự đạt**) · In.
- Lọc: ô tìm (nguyên liệu, NCC, số lô, số phiếu); kỳ **Hôm nay · 7 ngày · Tháng này · Chọn khoảng**; Kết quả (gồm *Nhận có điều kiện*, *Chờ kết luận*); Bảo quản; NCC; **Chỉ nhiệt độ ngoài ngưỡng**. 4 thẻ: Lượt kiểm · Tỷ lệ đạt · Có điều kiện / Không đạt · Nhiệt độ ngoài ngưỡng.
- Bấm dòng → ngăn chi tiết (đủ tiêu chí, nhận xét, lô kho) + tab **Lô này đã đi đâu**; nút **Mở phiếu nhập**, **Truy xuất lô**, **In phiếu kiểm tra**.
- Tab **Theo nhà cung cấp**: phiếu, dòng kiểm, đạt, có điều kiện, không đạt, nhiệt độ ngoài ngưỡng, **tỷ lệ đạt** (thấp lên đầu), lần kiểm gần nhất; bấm NCC → lọc sổ.
- Đầu màn: **In sổ kiểm thực bước 1** (theo khoảng ngày đang xem) + **Xuất Excel**.
- **Không có nút Sửa / Xóa / Kết luận trên sổ.** Hồ sơ kiểm tra không sửa được (*Hồ sơ kiểm tra hàng nhập không sửa được sau khi kết luận – ghi bổ sung ở lô hoặc sự cố nhà cung cấp.*). Sai sau khi nhập kho → Tạm giữ lô, sự cố NCC hoặc Điều chỉnh tồn (có lý do).

Ngưỡng nhiệt độ nhận hàng:

| Kiểu bảo quản | Nhận | Nhận có điều kiện (bắt buộc lý do) | Loại (chỉ được Không đạt) |
|---|---|---|---|
| Mát (CHILLED) | ≤ 5 °C | 5 – 8 °C | > 8 °C |
| Đông lạnh (FROZEN) | ≤ −18 °C | −18 – −15 °C (+ xác nhận "không rã đông") | > −15 °C |

**Đạt một phần:** chọn Đạt một phần → hiện ô **Số đạt** (> 0 và nhỏ hơn số nhận) – chỉ phần đạt được nhập kho. **Hàng đông lạnh** nhận có điều kiện (−18 … −15 °C) → tick **Không có dấu hiệu rã đông** (bắt buộc).

### 4.2 Sự cố nhà cung cấp – `qc-supplier-issue` (UI-6.33)
- **Danh sách:** chip trạng thái có số đếm **Chưa đóng** (mặc định) · Mới · Đang xử lý · Đã xử lý · Đóng · Tất cả; quá hạn lên đầu + nhãn đỏ; cột NCC · Mô tả ngắn (rê chuột xem đủ) · Loại · Mức độ · Phiếu nhập / đơn mua · Hạn xử lý · Phụ trách · Trạng thái (+ hướng xử lý). Lọc NCC · Loại · Mức độ · kỳ (30 / 90 ngày), ô tìm. 4 thẻ: Chưa đóng · Quá hạn · Mới trong 30 ngày · NCC nhiều sự cố nhất (30 ngày).
- **Nút bước tiếp trên dòng:** **Nhận xử lý** (bạn là người phụ trách) → **Đã xử lý** (chọn **hướng xử lý**: Đổi hàng · Trả hàng · Trừ tiền thanh toán · Cảnh cáo nhà cung cấp · Khác + ghi chú bắt buộc) → **Đóng**; **Mở lại** (ghi lý do).
- Bấm dòng → ngăn chi tiết (mô tả đủ, chứng từ, hướng xử lý, tab **Lịch sử**). **Sửa** chỉ khi Mới / Đang xử lý (Loại, Mức độ, Mô tả, chứng từ).
- **Lập sự cố** (nút đầu màn): **Nhà cung cấp\*** · **Loại\*** (Chất lượng · Nhiệt độ · Giao trễ · Giao thiếu · Sai quy cách · Giấy tờ / hóa đơn · Khác) · **Mức độ** (Cao · Trung bình · Thấp) · **Mô tả\*** · Phiếu nhập / đơn mua của NCC đã chọn. Trạng thái Mới, hạn xử lý tự tính.
- Sự cố tự sinh khi ghi sổ phiếu nhập có dòng Không đạt / Đạt một phần (loại Chất lượng / Nhiệt độ).
- **Không xóa được** (*Sự cố nhà cung cấp là hồ sơ chất lượng… ghi nhầm thì đóng với hướng xử lý Khác, ghi chú 'Ghi nhầm'*). Sự cố quá hạn: bot gửi 1 tin gộp / ngày cho QA, mua hàng.

### 4.3 Kiểm tra theo ca / mẻ – `qc-checklist-result` (UI-6.30 – kiểm thực bước 2)
Màn có 3 tab: **Hôm nay** · **Sổ kiểm tra** · **Tỷ lệ đạt**.

**Hôm nay (chỗ làm việc):**
1. Chọn ngày bằng ◀ ▶ (mặc định hôm nay). 4 thẻ: Lượt đến hạn · Đã làm · **Quá hạn** · **Lỗi trọng yếu đang mở**.
2. **Ma trận Bộ kiểm tra × Ca**: mỗi ô "đã ghi / số tiêu chí" + giờ hạn; màu: Chưa đến hạn · Đến hạn (xanh dương) · **Quá hạn (đỏ)** · Đạt hết (xanh lá) · Có lỗi (cam) · **Có lỗi trọng yếu (đỏ)** + "Giữ mẻ: …". Cột ca đang chạy tô nổi. Hạn = giờ xuất bếp của ca + độ lệch của bộ (Sơ chế −150′, Vệ sinh khu sơ chế −90′, Làm lạnh nhanh −20′, Vệ sinh bếp +90′).
3. Chỉ hiện bộ ghi ở màn này; bộ **công đoạn / lưu mẫu** ghi ở **Mẻ nấu**, **vệ sinh khay** ở **Vệ sinh khay hộp**.
4. Bấm ô → **form kiểm tra**: tiêu chí theo thứ tự, kết quả **mặc định Đạt**, nút **Đạt tất cả**; ô **Giá trị đo** chỉ ở tiêu chí có chuẩn đo; **Không đạt** → bắt buộc **hành động khắc phục**.
5. Tiêu chí **trọng yếu** Không đạt → khối **Mẻ bị ảnh hưởng**: tick mẻ của ca (mẻ Đang nấu / Chờ QC được tick sẵn) **hoặc** tick **Không ảnh hưởng mẻ nào** + lý do (bắt buộc). **Ghi kết quả** → mẻ được chọn **bị giữ** (Đạt → Chờ QC + tạm giữ; Đã đóng gói → chặn xuất chuyến) – thông báo ghi rõ "Mẻ bị giữ: …"; bot gửi 🔴 tới QA, trưởng ca, bếp trưởng, COO.
6. Kiểm lại sau khắc phục: mở lại ô, đổi tiêu chí sang **Đạt** → các mẻ đang bị giữ vì tiêu chí đó được mở chặn. Chỉ ghi tiêu chí thay đổi (kiểm lại = ghi lần mới, không sửa lần cũ).
- Lượt quá hạn: bot gửi **1 tin gộp theo ca** "⏰ Kiểm tra ATTP quá hạn" cho trưởng ca + QA.
- Lỗi: *Có tiêu chí trọng yếu Không đạt – chọn mẻ bị ảnh hưởng (mẻ sẽ bị giữ) hoặc ghi lý do không ảnh hưởng mẻ nào*, *Tiêu chí … KHÔNG ĐẠT – bắt buộc ghi hành động khắc phục*, *Bộ kiểm tra … ghi ở màn Mẻ nấu …*.

**Sổ kiểm tra (chỉ xem – hồ sơ kiểm thực bước 2):** mỗi dòng = một tiêu chí đã ghi, **gom theo ngày** (mở / gập, "N lượt · M tiêu chí · K không đạt", nút **In ngày**). Cột Giờ ghi · Ca · Bộ kiểm tra · Tiêu chí (nhãn **Trọng yếu**, **Kiểm lại**) · Giá trị đo (+ chuẩn) · Kết quả · Khắc phục · Mẻ bị giữ · Người kiểm. Ô tìm + kỳ **Hôm nay · 7 ngày · Tháng này · Chọn khoảng** + lọc Bộ · Ca · Kết quả · **Chỉ lỗi trọng yếu**. Đầu màn **In sổ kiểm thực bước 2** + **Xuất Excel**. Không có Sửa / Xóa. Điện thoại: thẻ gọn.

**Tỷ lệ đạt:** biểu đồ tỷ lệ đạt 30 ngày theo bộ (kết quả mới nhất mỗi tiêu chí trong ngày / ca) + bảng **tiêu chí hay Không đạt nhất** (số lần, lần gần nhất).

### 4.3b Bộ kiểm tra – tab "Bộ kiểm tra" trong Kiểm tra theo ca / mẻ (UI-6.33)
Mục "Bộ kiểm tra ATTP" không còn trên menu – mở tab **Bộ kiểm tra** (người có quyền bộ kiểm tra; link cũ `#/qc-checklist` mở tab này).
- Danh sách: Mã (+ nhãn **Bộ hệ thống**), Tên, Khu vực, Tần suất, Số tiêu chí, Trạng thái; bấm 👁 → tab **Tiêu chí** (thêm / sửa / xếp thứ tự; tiêu chí đã có kết quả chỉ đổi thứ tự / mô tả).
- **Tạo mới:** Tên\*, Khu vực (Bếp · Khu sơ chế · Đóng gói / khay hộp · Kho · Giao hàng), Tần suất (Mỗi ca · Mỗi mẻ · Mỗi ngày · Mỗi tuần), hiệu lực, ghi chú – **mã tự sinh** từ tên (BKT_…), phiên bản 1.
- **Bộ hệ thống** (sơ chế, vệ sinh khu sơ chế, làm lạnh nhanh, vệ sinh bếp, vệ sinh khay, công đoạn, kiểm soát mẻ / thành phẩm): không đổi mã, không ngừng dùng, không xóa – sửa nội dung bằng **Tạo phiên bản mới**.
- **Tạo phiên bản mới:** chọn ngày áp dụng (từ ngày mai) → chép toàn bộ tiêu chí sang phiên bản mới; bản cũ hết hiệu lực ngày trước đó. Kiểm tra theo ca tự dùng bản có hiệu lực theo ngày.
- Hạn làm: bộ hệ thống theo giờ xuất bếp (Sơ chế −150′, Vệ sinh khu sơ chế −90′, Làm lạnh nhanh −20′, Vệ sinh bếp +90′); **bộ tự tạo – trong ca (hạn = hết ca)**.

### 4.4 Nhật ký nhiệt độ – `qc-temperature` (UI-6.31)
3 tab: **Hôm nay** · **Sổ nhiệt độ** · **Biểu đồ**.

**Hôm nay:** ngày ◀ ▶ + nhãn **ca đang chạy** + nút to **Ghi một lượt**. 4 thẻ: Khu đang ngoài ngưỡng · **Đến giờ chưa đo (ca này)** · **Cảm biến mất tín hiệu** · Lần ngoài ngưỡng 7 ngày (đã khắc phục / còn mở).
- Khối **Cần xử lý ngay**: khu ngoài ngưỡng chưa khắc phục → **Đo lại & ghi khắc phục**; khu đến giờ chưa đo / cảm biến mất tín hiệu → **Đo tay ngay**.
- Mỗi khu một thẻ: nhiệt độ mới nhất, ngưỡng, giờ đo, nhãn **Cảm biến** / **Ghi tay**, đường 24 giờ (dải xanh = ngưỡng), trạng thái (Đạt · Ngoài ngưỡng chưa khắc phục · Đến giờ chưa đo · Cảm biến mất tín hiệu). Bấm thẻ → tab Biểu đồ của khu.
- **Lịch đo:** khu không có cảm biến (hoặc cảm biến mất tín hiệu) phải đo **ít nhất 1 lần mỗi ca**; quá **60 phút đầu ca** chưa đo → "Đến giờ chưa đo" + bot báo trưởng ca, QA (1 tin / ca). Cảm biến quá **30 phút** không gửi số → "Cảm biến mất tín hiệu" + bot báo (1 lần / khoảng mất) – đo tay khu đó tới khi có lại.

**Ghi một lượt:** mỗi khu ghi tay (và khu cảm biến đang mất tín hiệu) một dòng – ngưỡng, lần trước, ô nhiệt độ to; nhập là tô xanh / đỏ ngay; ngoài ngưỡng → **hành động khắc phục bắt buộc**; để trống = chưa đo khu đó; **Lưu cả lượt** (một lần). Loại đo tự theo loại khu, giờ đo = lúc lưu. Ngoài ngưỡng → bot báo trưởng ca + QA.
- **Đo lại & ghi khắc phục:** ghi **lần đo mới** kèm khắc phục (không sửa lần cũ). Lần ngoài ngưỡng được coi **đã khắc phục** khi có khắc phục hoặc có lần đo sau trong ngưỡng của cùng khu.

**Sổ nhiệt độ (chỉ xem):** gom theo ngày (số lần đo / ngoài ngưỡng, nút **In ngày**); cột Giờ · Khu vực / đối tượng · Loại đo · Nhiệt độ · Ngưỡng · Kết quả · Người đo / nguồn ("Cảm biến <mã>") · Khắc phục. Ô tìm + kỳ (Hôm nay · 7 ngày · Tháng này · Chọn khoảng) + lọc khu, nguồn, **Chỉ ngoài ngưỡng**, **Gồm lõi mẻ / giao hàng / nhận hàng** (mặc định tắt; bật thì có link mở mẻ / lô). **In sổ nhiệt độ** + **Xuất Excel**. Không Sửa / Xóa.

**Biểu đồ:** một biểu đồ cho khu đã chọn – **48 giờ / 7 ngày / 30 ngày**, dải ngưỡng, điểm ngoài ngưỡng tô đỏ.
- Nhiệt độ lõi mẻ, giao hàng, nhận hàng ghi ở màn Mẻ nấu / Chuyến giao hàng / Nhận hàng (*Nhiệt độ «…» ghi ở màn …*).

### 4.5 QC thành phẩm (mẻ) – `prod-batch`
Bấm thẻ mẻ trên **bảng điều độ** (ngày × ca × dây chuyền) → ngăn chi tiết:
- Tab **Nguyên liệu & an toàn**: điều kiện còn thiếu (đỏ), ngưỡng nhiệt độ lõi ≥ 70 °C, lưu mẫu, đã xuất nguyên liệu chưa, sản lượng, công đoạn, nhiệt độ lõi, lưu mẫu, hao hụt.
- Mẻ **Chờ QC**: **Ghi lưu mẫu**, **QC: PASS**, **HOLD**, **REJECT** (HOLD / REJECT bắt buộc ghi chú).
  - PASS → **Đạt**; HOLD → giữ ở Chờ QC (Tạm giữ); REJECT → **Không đạt** (tự ghi hao hụt "QC loại").
- Mẻ **Đạt**: **Loại mẻ** (nếu phát hiện sau), **Tạo lệnh đóng gói**.
- *Mẻ … chưa đạt điều kiện ATTP: Chưa đo nhiệt độ lõi / Nhiệt độ lõi … dưới ngưỡng 70 °C / Chưa ghi sản lượng mẻ / Tiêu chí ATTP trọng yếu chưa đạt…*.

### 4.6 Vệ sinh khay hộp – `qc-cleaning` (UI-6.32)
Một màn cho cả việc kiểm tra khay thu hồi và vệ sinh 6 bước (mục "Kiểm tra vệ sinh" và "Kiểm tra khay hộp thu hồi" đã ẩn – link cũ mở màn này). 2 tab: **Hôm nay** · **Sổ vệ sinh**.

**Đầu màn:** ngày ◀ ▶; thanh **Khay sạch cho ngày mai** theo loại bao bì: *sẵn sàng / cần (suất đã chốt ngày mai × khay mỗi suất) – thiếu N* (đỏ) · đang vệ sinh · chờ kiểm tra. Tồn khay sạch đầu kỳ ghi ở **Sổ bao bì** (loại "Sẵn sàng dùng lại"). 4 thẻ: Chờ kiểm tra · Đang vệ sinh · Hoàn tất hôm nay (đạt / tổng) · Tỷ lệ đạt 7 ngày.

**Hôm nay – 3 cột:**
1. **Chờ kiểm tra** (phiếu thu hồi Đã thu hồi: khách, loại, đơn giao, giờ thu, phải thu / thu về / hỏng / thiếu) → **Kiểm tra**: chọn kết quả một chạm (Đủ / Thiếu / Hỏng / Bẩn / Không đạt – gợi ý sẵn Thiếu khi thu về ít hơn, Hỏng khi có hỏng), số hỏng / thiếu điền sẵn, **người kiểm = tài khoản đang đăng nhập**, ghi chú bắt buộc khi khác Đủ → **Ghi kiểm tra & lập mẻ** (khay dùng được tự vào mẻ vệ sinh mới).
2. **Đang vệ sinh** (mẻ: số, khách, loại, số lượng, thanh 6 bước, "Bước x/6 · người phụ trách") → **Ghi bước**: bước đã ghi hiện Đạt + người / giờ; bước còn lại **mặc định Đạt**; bước khử khuẩn **hóa chất điền sẵn** theo lần ghi gần nhất; Không đạt → khắc phục bắt buộc (khóa kết luận).
   - **Lưu bước đã làm** – ghi lần lượt tới bước Không đạt đầu tiên.
   - **Đạt tất cả & kết luận** – một chạm: ghi Đạt mọi bước còn lại + kết luận Đạt → khay vào sổ bao bì "sẵn sàng dùng lại". Bị chặn khi mẻ có bước Không đạt.
3. **Hoàn tất hôm nay**: Đạt – sẵn sàng / Không đạt + lý do + **Vệ sinh lại** (lập mẻ mới đúng số khay của mẻ không đạt).

**Sổ vệ sinh (chỉ xem):** gom theo ngày (số mẻ, số không đạt, **In ngày**); cột Giờ kết luận · Mẻ · Khách · Loại · Số lượng · Kết quả · Hóa chất khử khuẩn · Người làm · Khắc phục; tìm + kỳ (Hôm nay · 7 ngày · Tháng này · Chọn khoảng) + lọc kết quả; bấm số mẻ → đủ 6 bước (người, giờ, hóa chất, số lần ghi). **In sổ vệ sinh** + **Xuất Excel**. Không Sửa / Xóa.

## 5. Trạng thái

| Chứng từ | Luồng |
|---|---|
| Kiểm tra hàng nhập (dòng) | Đạt / Đạt một phần / Không đạt / Chờ kết luận (+ nhãn "Có điều kiện") |
| Sự cố NCC | Mới → Đang xử lý → Đã xử lý → Đóng (Đã xử lý ↔ Mở lại) |
| Checklist (tiêu chí) | PASS / FAIL |
| Mẻ | Kế hoạch → Đang nấu → Chờ QC → Đạt / Không đạt → Đã đóng gói; QC: Đạt / Tạm giữ / Từ chối |
| Mẻ vệ sinh | Đang vệ sinh → Hoàn tất (Đạt – sẵn sàng dùng lại / Không đạt – vệ sinh lại) |
| Thu hồi khay | Chờ thu hồi → Đã thu hồi → Đã kiểm tra |

## 6. Lỗi / cảnh báo thường gặp

| Thông báo | Cách xử lý |
|---|---|
| Chưa kết luận đủ các dòng / Nhập lý do cho dòng HOLD/REJECT | Chọn kết quả mọi dòng, ghi lý do |
| {NL}: nhiệt độ nhận … > … °C (vùng loại) – chỉ được kết luận KHONG_DAT | Chọn REJECT |
| {NL}: nhiệt độ nhận … vượt ngưỡng … – kết luận KHONG_DAT hoặc ghi lý do chấp nhận | Ghi lý do / hành động khắc phục |
| {NL}: chưa có nhiệt độ nhận – hàng … bắt buộc đo nhiệt độ… | Nhập nhiệt độ |
| {NL}: hạn dùng … trước ngày nhận – chỉ kết luận KHONG_DAT | Chọn REJECT |
| {NL}: DAT_MOT_PHAN cần số lượng đạt > 0 và < số nhận | Dòng HOLD: nhập Số lượng đạt nhỏ hơn số nhận |
| Không chuyển sự cố từ … sang … | Bước chuyển không hợp lệ |
| Chuyển Đã xử lý phải ghi cách xử lý | Nhập cách xử lý |
| Tiêu chí bắt buộc chưa có kết quả: … | Ghi đủ tiêu chí |
| Nhiệt độ ngoài ngưỡng của khu vực – nhập hành động khắc phục | Nhập khắc phục |
| Bước … chưa làm được: bước trước … chưa ĐẠT | Làm bước trước |
| Bước …: phải ghi hóa chất đã dùng | Nhập hóa chất |
| Mẻ vệ sinh … chưa ĐẠT đủ các bước | Làm đủ trước khi kết luận Đạt |

## 7. Trên bot Telegram

Phân hệ **🧽 ATTP / QC** (bot Vận hành):
- 🧽 **Checklist ATTP** (`/checklist`) – chọn checklist → bấm từng mục ✅ / ❌ trong một tin → **✔️ Hoàn tất**.
- ⛔ **Mẻ đang giữ** (`/giu`) – mẻ tạm giữ / không đạt hôm nay.
- 🧪 **Lô chờ QC đầu vào** (`/chokiem`).
- 🌡 **Cảnh báo ATTP** (`/canhbao`) – nhiệt độ ngoài ngưỡng, lô giữ / cận date.
- QR khu vệ sinh (`?start=CLEAN`) – xem lô vệ sinh hôm nay (ghi kết quả trên web).
- Thông báo 🔴: lỗi trọng yếu checklist, nhiệt độ lõi < 70 °C, nhiệt độ kho (IoT) ngoài ngưỡng, QC đầu vào không đạt – thẻ có **🛠 Nhận xử lý / ✅ Đã xử lý**, quá 10′ chưa ai nhận thì leo thang COO.
