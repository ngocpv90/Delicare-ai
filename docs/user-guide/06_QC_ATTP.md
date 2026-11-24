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
| Điều hành › Chỉ số theo ngày | `kpi-daily` | X |
| Điều hành › Chỉ số theo tháng | `kpi-monthly` | X |
| Kinh doanh › Phản ánh khách hàng | `log-complaint` | S (IX) |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | X |
| Thực đơn & Món ăn › Công thức chế biến | `menu-recipe` | X |
| Thực đơn & Món ăn › Món ăn | `menu-dish` | X |
| Mua hàng › Nhà cung cấp | `scm-supplier` | X |
| Mua hàng › Nguyên liệu | `scm-ingredient` | X |
| Kho › Phiếu nhập kho | `scm-gr` | X |
| Kho › Tồn kho | `scm-inv-balance` | X |
| Kho › Lô hàng & hạn dùng | `scm-lot` | X |
| Sản xuất › Kế hoạch sản xuất | `prod-plan` | X |
| Sản xuất › Mẻ nấu | `prod-batch` | X |
| Sản xuất › Sản lượng | `prod-output` | X |
| Chất lượng & ATTP › Kiểm tra hàng nhập | `qc-inspection` | S (IX) |
| Chất lượng & ATTP › Kiểm tra theo ca / mẻ | `qc-checklist-result` | S (IX) |
| Chất lượng & ATTP › Nhật ký nhiệt độ | `qc-temperature` | S (IX) |
| Chất lượng & ATTP › Vệ sinh khay hộp | `qc-cleaning` | S (IX) |
| Chất lượng & ATTP › Kiểm tra vệ sinh | `qc-cleaning-result` | S (IX) |
| Chất lượng & ATTP › Sự cố nhà cung cấp | `qc-supplier-issue` | Q |
| Chất lượng & ATTP › Bộ kiểm tra ATTP | `qc-checklist` | Q |
| Đóng gói & Giao hàng › Đơn giao hàng | `log-delivery-order` | X |
| Đóng gói & Giao hàng › Theo dõi chuyến giao | `log-execution` | X |
| Đóng gói & Giao hàng › Kiểm tra khay hộp thu hồi | `log-return-inspection` | S (IX) |
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

### 4.1 QC đầu vào – `qc-inspection`
1. Dòng chưa kết luận có nút **Kết luận** (hoặc mở thẳng từ màn nhận hàng).
2. Hộp **Kiểm tra chất lượng đầu vào – …**: mỗi dòng nguyên liệu có Bảo quản, **Nhiệt độ °C** (dưới ô hiện *Tối đa …°C · vùng nhiệt · Gợi ý: Đạt / Tạm giữ / Không đạt*), KL, **Cảm quan**, **Bao bì**, HSD, **Kết quả** (PASS / HOLD / REJECT – điền sẵn theo gợi ý).
3. Nhập **Ghi chú / lý do** (bắt buộc khi có dòng HOLD / REJECT) → **Lưu kết luận**.
4. Kết quả: phiếu nhập → **Đạt / Đạt một phần / Từ chối**; lô được gắn kết quả QC; có dòng không đạt → tự lập **Sự cố NCC**. Kho bấm **Ghi sổ** để nhập phần đạt.

Ngưỡng nhiệt độ nhận hàng:

| Kiểu bảo quản | Nhận | Nhận có điều kiện (bắt buộc lý do) | Loại (chỉ được Không đạt) |
|---|---|---|---|
| Mát (CHILLED) | ≤ 5 °C | 5 – 8 °C | > 8 °C |
| Đông lạnh (FROZEN) | ≤ −18 °C | −18 – −15 °C (+ xác nhận "không rã đông") | > −15 °C |

**HOLD (nhận một phần):** chọn HOLD → hiện ô **Số lượng đạt** (> 0 và nhỏ hơn số nhận) – chỉ phần đạt được nhập kho. **Hàng đông lạnh** (FROZEN) có ô tick **Xác nhận không rã đông** – bắt buộc khi nhiệt độ nhận nằm trong vùng nhận có điều kiện (−18 … −15 °C).

### 4.2 Sự cố nhà cung cấp – `qc-supplier-issue`
- Danh sách: NCC, Phiếu nhập, Loại, Mức độ, Ghi nhận, **Hạn xử lý** (+ "Quá hạn"), Phụ trách, Trạng thái. Sự cố quá hạn xếp lên đầu.
- Nút theo trạng thái:

| Trạng thái | Nút | Ghi chú |
|---|---|---|
| Mới | **Nhận xử lý** / **Đã xử lý** | Nhận xử lý → bạn là người phụ trách |
| Đang xử lý | **Đã xử lý** | Bắt buộc **Cách xử lý** |
| Đã xử lý | **Đóng** / **Mở lại** | Đóng rồi không sửa, không mở lại |

### 4.3 Checklist ATTP – `qc-checklist-result`
1. Chọn bộ checklist (chip mã) và **ca** (chip mã ca). Ngày = ngày làm việc hiện tại.
2. Dòng trạng thái: *Đã ghi x/y tiêu chí cho ca này · Hạn làm: … · Quá hạn*.
3. Bảng tiêu chí: Tiêu chí (+ nhãn **Trọng yếu**), Chuẩn, **Giá trị đo**, **Kết quả** (PASS / FAIL), **Khắc phục** (bắt buộc nếu FAIL).
4. **Ghi kết quả checklist** → ghi cả lượt một lần.
   - Có tiêu chí **trọng yếu** FAIL → *"Có N tiêu chí TRỌNG YẾU không đạt – mẻ liên quan chuyển chờ QC"*: mẻ bị giữ, **chặn đạt QC / đóng gói / xuất chuyến** tới khi kiểm lại ĐẠT; bot gửi 🔴 tới QA, trưởng ca, bếp trưởng, COO.
5. Khối **Tiêu chí trọng yếu đang không đạt** + biểu đồ **Tỷ lệ đạt 7 ngày**.
- Lỗi: *Chọn kết quả cho tất cả tiêu chí*, *Tiêu chí FAIL bắt buộc nhập hành động khắc phục*, *Bộ checklist … không áp dụng ngày …*.

### 4.4 Nhật ký nhiệt độ – `qc-temperature`
1. Chip **Khu vực bảo quản** → biểu đồ 48 giờ (dải ngưỡng tối thiểu – tối đa, điểm đỏ = ngoài ngưỡng) + số lần vượt.
2. Khối **Chuỗi nhiệt độ khu vực kho** (chọn khu ở ô chọn) – gồm cả số đo cảm biến IoT.
3. **Ghi nhiệt độ** → chọn Khu vực bảo quản **hoặc** Mẻ sản xuất, **Loại đo*** (Kho / tủ bảo quản, Giữ nóng thành phẩm, Làm lạnh nhanh, Khu chia suất / đóng gói, Nhiệt độ phòng), **Nhiệt độ °C***, Đo lúc, Thiết bị đo, **Hành động khắc phục** (bắt buộc khi ngoài ngưỡng) → **Ghi nhiệt độ**.
4. Sổ chỉ ghi thêm – không sửa / xóa. Nhiệt độ lõi mẻ và nhiệt độ giao ghi ở màn mẻ / giao hàng (*Nhiệt độ … ghi qua …*).

### 4.5 QC thành phẩm (mẻ) – `prod-batch`
Bấm thẻ mẻ trên **bảng điều độ** (ngày × ca × dây chuyền) → ngăn chi tiết:
- Tab **Nguyên liệu & an toàn**: điều kiện còn thiếu (đỏ), ngưỡng nhiệt độ lõi ≥ 70 °C, lưu mẫu, đã xuất nguyên liệu chưa, sản lượng, công đoạn, nhiệt độ lõi, lưu mẫu, hao hụt.
- Mẻ **Chờ QC**: **Ghi lưu mẫu**, **QC: PASS**, **HOLD**, **REJECT** (HOLD / REJECT bắt buộc ghi chú).
  - PASS → **Đạt**; HOLD → giữ ở Chờ QC (Tạm giữ); REJECT → **Không đạt** (tự ghi hao hụt "QC loại").
- Mẻ **Đạt**: **Loại mẻ** (nếu phát hiện sau), **Tạo lệnh đóng gói**.
- *Mẻ … chưa đạt điều kiện ATTP: Chưa đo nhiệt độ lõi / Nhiệt độ lõi … dưới ngưỡng 70 °C / Chưa ghi sản lượng mẻ / Tiêu chí ATTP trọng yếu chưa đạt…*.

### 4.6 Vệ sinh khay / hộp – `qc-cleaning`, `qc-cleaning-result`
**Kiểm tra khay thu hồi** (nút đầu trang, cần quyền màn Kiểm tra hàng trả về):
1. Chọn **Phiếu thu hồi*** (đang Đã thu hồi), **Kết quả kiểm tra*** (Đủ / Thiếu / Hỏng / Bẩn / Không đạt), Người kiểm, Số hỏng, Số thiếu, Người phụ trách vệ sinh, Ghi chú (bắt buộc khi khác Đủ).
2. **Ghi kết quả kiểm tra** → xác nhận → khay dùng được **tự vào mẻ vệ sinh `VS-…`**.

**Lập mẻ vệ sinh** (dùng khi vệ sinh lại sau mẻ không đạt): chọn Phiếu kiểm tra thu hồi*, Số khay / hộp (trống = toàn bộ số dùng được còn lại), Ca, Người phụ trách → **Lập mẻ**.

**Ghi 6 bước:** trên dòng mẻ bấm **Ghi bước** (Đang vệ sinh) / **Các bước** (đã xong); hoặc **Ghi bước vệ sinh** ở `qc-cleaning-result`.
1. Bảng bước: Bước (* bắt buộc), Kết quả hiện tại (người, giờ, lần), chọn **Đạt / Không đạt**, Giá trị đo, **Hóa chất** (bắt buộc khi Đạt ở bước khử khuẩn), **Khắc phục** (bắt buộc khi Không đạt).
2. Làm **lần lượt**: bước sau chỉ ghi được khi các bước bắt buộc trước đã Đạt. **Lưu các bước**.
3. Kết luận: **Đạt – sẵn sàng dùng lại** (khi mọi bước đạt) → khay vào sổ bao bì "sẵn sàng dùng lại"; hoặc **Không đạt** (bắt buộc lý do) → lập mẻ mới để vệ sinh lại. Sau khi kết luận không sửa được.

## 5. Trạng thái

| Chứng từ | Luồng |
|---|---|
| QC đầu vào (dòng) | PASS (Đạt) / HOLD (Đạt một phần) / REJECT (Không đạt) |
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
