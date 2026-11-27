# 04 – Mua hàng (nhu cầu nguyên liệu, đề nghị mua, đơn mua, nhà cung cấp)

## 1. Bạn là ai
> **BIZ-2.2 – PO tự duyệt:** lập PO từ đề nghị mua Đã duyệt → PO **tự Đã duyệt** khi đúng NCC gợi ý, đơn giá = bảng giá, số ≤ đề nghị, tổng < 50 triệu (ghi chú `[TU_DUYET]`). Thiếu đơn giá / ≥ 50 triệu → Nháp, gửi duyệt như cũ. Sửa dòng PO đã tự duyệt (chưa nhập hàng) → về **Nháp**, gửi duyệt lại.
> **BIZ-2.6 – QC xong tự nhập kho:** kết luận QC **Đạt / Đạt một phần** → hệ thống **tự Ghi sổ** phần đạt (lô, tồn, PO) ngay trong thao tác kết luận. **Không đạt** cả phiếu → không nhập, Ghi sổ tay để tạo sự cố NCC. Nút **Ghi sổ** chỉ còn dùng khi tự ghi sổ lỗi.


- Nhóm quyền: **OPS-SCM** (1.2 Mua hàng và Kho) – dùng chung với thủ kho (`05_KHO.md`). Trưởng bộ phận mua hàng được cấp thêm **"Phê duyệt › Đề nghị mua hàng (cấp 1)"**.
- Bot dùng: **Vận hành** (phân hệ 📦 Kho & mua hàng); trưởng bộ phận được cấp thêm bot **Điều hành**.
- Việc chính: tính nhu cầu nguyên liệu (MRP) cho ngày sản xuất, lập và gửi duyệt đề nghị mua, chuyển thành đơn mua (PO) theo NCC, gửi NCC, theo dõi hàng về, quản lý bảng giá NCC.

## 2. Màn hình được dùng

<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->
Quyền theo ma trận D-79 (đủ 19 nhóm: `docs/claude/MENU5_MATRIX.md`).

| Menu › Mục | screen-id | OPS-SCM | OPS-SCM-HEAD |
|---|---|---|---|
| Việc của tôi › Việc của tôi | `hr-my` | X | X |
| Phê duyệt › Đề nghị mua – cấp 1 | `wf-inbox?type=scm-pr-l1` | — | Duyệt |
| Phê duyệt › Phiếu kiểm kê – cấp 1 | `wf-inbox?type=scm-stockcount` | — | Duyệt |
| Phê duyệt › Xếp xe chưa đủ hàng | `wf-inbox?type=log-load-short` | — | Duyệt |
| Điều hành › Bảng điều hành | `dash-exec` | — | X |
| Điều hành › Chỉ số kinh doanh | `kpi-daily` | — | X |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | X | X |
| Thực đơn & Món ăn › Công thức chế biến | `menu-recipe` | X | X |
| Mua hàng › Tính nhu cầu nguyên liệu | `scm-requirement` | S (IX) | S (IX) |
| Mua hàng › Đề nghị mua hàng | `scm-pr` | S (IX) | S (IX) |
| Mua hàng › Đơn mua hàng | `scm-po` | S (IX) | S (IX) |
| Mua hàng › Nhà cung cấp | `scm-supplier` | Q | Q |
| Mua hàng › Bảng giá nhà cung cấp | `scm-supplier-material` | Q | Q |
| Mua hàng › Nguyên liệu | `scm-ingredient` | Q | Q |
| Kho › Nhận hàng | `scm-gr` | X | S (IX) |
| Kho › Trả hàng NCC | `scm-supplier-return` | Sửa | S (IX) |
| Kho › Xuất kho sản xuất | `scm-issue` | X | S (IX) |
| Kho › Tồn kho | `scm-inv-balance` | X | Q (IX) |
| Kho › Kiểm kê | `scm-stockcount` | — | S (IX) |
| Sản xuất › Kế hoạch sản xuất | `prod-plan` | X | X |
| Chất lượng & ATTP › Sổ kiểm tra hàng nhập | `qc-inspection` | X | X |
| Chất lượng & ATTP › Sự cố nhà cung cấp | `qc-supplier-issue` | X | X |
| Đóng gói & Giao hàng › Định mức bao bì | `log-packaging-norm` | X | X |
| Danh mục hệ thống › Đơn vị tính | `scm-uom` | Q | Q |
| Danh mục hệ thống › Kho & vị trí | `scm-warehouse` | X | Q |
| Tri thức › Kho tri thức | `knowledge` | X | X |
| Tri thức › 30 Thực đơn & Công thức | `knowledge?folder=30` | X | X |
| Tri thức › 40 Kho & Truy xuất | `knowledge?folder=40` | X | S |
| Tri thức › 60 Kế toán | `knowledge?folder=60` | — | X |
| Tri thức › 80 Đào tạo | `knowledge?folder=80` | X | X |
| Tri thức › 99 Mẫu | `knowledge?folder=99` | X | X |
<!-- MENU5:END -->

## 3. Việc trong ngày

| Giờ | Việc | Màn |
|---|---|---|
| 20:00 | Đơn ngày mai được chốt (tự động) | – |
| 20:30 | Hệ thống tự **tính nhu cầu** ngày mai + lập **đề nghị mua Nháp** phần thiếu | tự động |
| Sau 20:30 | Kiểm tra nhu cầu, chọn dòng thiếu, lập / gửi duyệt đề nghị mua | `scm-requirement`, `scm-pr` |
| Khi đề nghị được duyệt | **Lập PO** → gửi duyệt PO → **Gửi NCC** → in đơn đặt hàng | `scm-pr`, `scm-po` |
| Khi hàng về | Thủ kho **Nhận hàng** theo PO (xem `05_KHO.md`) | `scm-po` |
| Khi có thêm đơn sau MRP | **Tính bổ sung** | `scm-requirement` |

## 4. Thao tác từng bước

### 4.1 Nhu cầu nguyên liệu (MRP) – `scm-requirement`
**Kế hoạch mua theo ngày cần hàng (khối đầu trang – BIZ-1.3, D-77 c):** hệ thống tự tính nhu cầu **7 ngày tới** (ngày mai → +7). Mỗi ngày, mỗi khách / điểm giao / loại suất lấy **đơn đã chốt**; chưa chốt thì lấy **đăng ký / đơn đã xác nhận**; chưa có thì lấy **dự báo đã công bố** (đầu cột ngày ghi nguồn). Tồn kho, hàng PO sắp về và đề nghị mua đang mở được **trừ dồn** qua các ngày (không dùng một tồn cho nhiều ngày). Mỗi nguyên liệu có **Ngày phải đặt** = ngày cần − thời gian giao của NCC ưu tiên − 1 ngày: *Phải đặt hôm nay* / *Đặt trước ngày …* / *Trễ*. Nút **Lập đề nghị mua cho phần phải đặt** → đề nghị mua Nháp, mỗi dòng ghi ngày cần hàng; gửi duyệt như thường. Bot `/nlmai` cũng báo nguyên liệu phải đặt hôm nay. Mốc 20:00 sau chốt đơn chỉ còn **Tính bổ sung**.

1. Chọn **ngày sản xuất** (mặc định ngày mai) ở đầu trang.
2. **Tính nhu cầu**: hệ thống lấy các đơn **Đã chốt** của ngày, nhân định lượng công thức, so với tồn **xuất được** và **hàng sắp về** (PO đã duyệt có ngày giao ≤ ngày SX), cộng tồn an toàn, rồi tự lập **đề nghị mua Nháp** cho phần thiếu.
3. Kết quả trên màn:
   - Thẻ: Đơn đã chốt (kế hoạch `KS-…`), Tổng suất / số món, **Nguyên liệu cần mua**, Giá trị mua ước tính (tính lúc …).
   - Khối **Lưu ý**: cảnh báo (đơn chưa chốt, đơn chưa gắn thực đơn, món chưa có công thức khóa, thiếu quy đổi đơn vị, NCC giao không kịp…).
   - Khối **Đề nghị mua của kế hoạch**: số đề nghị, số dòng, giá trị, trạng thái + **Mở Đề nghị mua →**.
   - Bảng **Nguyên liệu**: Nhu cầu thuần, **Tồn an toàn**, Tồn xuất được (+ "bị chặn" nếu có lô giữ / hết hạn), Hàng sắp về, **Cần mua**, ĐVT, Độ phủ (thanh màu), NCC gợi ý, Trạng thái (Đủ / Thiếu / Đã đề nghị mua).
   - Bảng **Món và số suất**: món, số suất (+ dự phòng khi thực đơn nhiều lựa chọn), phiên bản công thức.
4. **Lập đề nghị mua theo dòng chọn** (khi đề nghị chưa gửi duyệt): tick các nguyên liệu, nhập **Dự phòng thêm (%)** (0–100, **mặc định 0** – BIZ-1.2: số cần mua đã gồm tồn an toàn, chỉ nhập khi thật cần), Ghi chú → **Lập đề nghị mua**. Số đề nghị = cần mua × (1 + % dự phòng); đề nghị Nháp của kế hoạch được cập nhật lại.
5. **Tính bổ sung** (chỉ hiện khi đề nghị mua của kế hoạch đã gửi duyệt / đã duyệt / đã lập PO): tính lại phần thiếu thêm và lập **đề nghị mua bổ sung**, không đụng đề nghị cũ.
6. **In phiếu trước sản xuất**: tải phiếu nguyên liệu cần cho ngày.

### 4.2 Đề nghị mua – `scm-pr`
**Form đề nghị mua (UI-6.18, D-99):** số `MH-YYMMDD-NNNN`, ngày lập, người đề nghị **hệ thống tự điền** (chỉ đọc), luôn tạo **Nháp**, không có ô trạng thái. Nhập **Ngày cần hàng***, Ghi chú / lý do, **bảng nguyên liệu** (gõ tên để thêm dòng; số lượng; ĐVT – chỉ đơn vị quy đổi được; ngày cần từng dòng – trống = ngày cần chung). Mỗi dòng hiện **tồn hiện tại**, **NCC gợi ý · giá** (theo quy tắc gợi ý NCC), cảnh báo dưới tồn tối thiểu / "Cần chọn NCC: lý do"; cuối bảng **Tổng giá trị ước tính**. Nút **Lưu nháp** / **Gửi duyệt luôn**; chặn 0 dòng, số ≤ 0; nguyên liệu trùng tự gộp. Trang chi tiết đề nghị Nháp có nút **Sửa dòng**. **Duyệt cấp cuối → hệ thống tự lập đơn mua** (thông báo "MH-… đã duyệt → hệ thống lập PO-… (NCC, tiền, tự duyệt)"; dòng không có NCC đủ điều kiện chờ ở Đơn mua hàng › Chọn nhà cung cấp).

Danh sách: Số đề nghị, Ngày, Ngày cần, Người đề nghị, Số dòng, Giá trị ước tính, Trạng thái. Bấm dòng → **trang chi tiết đề nghị mua** (mục 4.6).

| Nút trên dòng | Khi nào | Kết quả |
|---|---|---|
| **Gửi duyệt** | Nháp | Gửi duyệt **2 cấp**: cấp 1 Trưởng mua hàng → cấp 2 Giám đốc vận hành |
| **Lập PO** | Đã duyệt | Mỗi NCC gợi ý một **PO Nháp**; báo danh sách PO + cảnh báo; chuyển sang màn Đơn mua |
| **Hủy** | Nháp / Đã duyệt | Bắt buộc lý do |

Đề nghị mua thường được **sinh từ MRP**. Tạo tay bằng **Tạo mới** tạo phần đầu phiếu, rồi mở phiếu (Nháp) → **Sửa dòng nguyên liệu**: thêm / sửa / xóa dòng (nguyên liệu, số lượng, đơn vị, ngày cần, ghi chú; mỗi nguyên liệu một dòng).

### 4.3 Đơn mua (PO) – `scm-po`
**Khối việc mua hàng trên đầu màn (UI-6.18):** 4 thẻ (Dòng cần chọn NCC · Đơn chờ gửi · Đơn chờ duyệt · Giá trị đang chọn) + 2 tab: **Chọn nhà cung cấp** – dòng đề nghị đã duyệt chưa có đơn mua (nhóm theo nhóm nguyên liệu, nhãn lý do), mỗi dòng ≤ 3 thẻ NCC (giá / đơn vị, số ngày giao, % đúng hẹn, số sự cố 90 ngày, ghi chú tối thiểu / tăng giá, thành tiền; nhãn Gợi ý / Ưu tiên; NCC không đủ điều kiện mờ kèm lý do) – **bấm thẻ = chọn**, **Theo gợi ý cho tất cả**; cột phải **Đơn mua sẽ lập** gom theo NCC và báo trước kết quả (Tự duyệt → chờ gửi / Gửi Trưởng Mua duyệt / Chờ Giám đốc duyệt ≥ 50 tr) → **Lập đơn mua**. **Chờ gửi NCC** – đơn Đã duyệt chưa gửi (nguồn, "Hệ thống tự duyệt"), nút **Gửi** từng đơn / **Gửi tất cả**, **Phiếu đặt hàng** (Excel). Danh sách đơn mua như cũ ở dưới.

Danh sách: Số PO, NCC, Đề nghị mua, Ngày đặt, Giao dự kiến, Giá trị, Trạng thái. Bấm dòng → **trang chi tiết đơn mua** (mục 4.6).

| Nút trên dòng | Khi nào | Kết quả |
|---|---|---|
| **In** | Mọi lúc | Tải Đơn đặt hàng NCC (có VAT theo cấu hình) |
| **Gửi duyệt** | Nháp | PO < 50 triệu: duyệt mục "Phê duyệt › Đơn mua hàng"; PO ≥ 50 triệu: "Phê duyệt › Đơn mua hàng (Giám đốc)". PO dưới ngưỡng tự duyệt (nếu cấu hình) được duyệt ngay |
| **Gửi NCC** | Đã duyệt | Chỉ **đánh dấu** đã gửi (hệ thống chưa tự gửi email / Zalo) → Đã gửi NCC. Gửi tệp in cho NCC bằng kênh thường dùng |
| **Nhận hàng** | Đã duyệt / Đã gửi NCC / Nhận một phần | Thủ kho lập phiếu nhập (xem `05_KHO.md` mục 4.1) |
| **Đóng** | Nhận một phần / Đã nhận đủ | → Đóng (không nhận thêm) |
| **Hủy** | Nháp / Đã duyệt / Đã gửi NCC | Bắt buộc lý do; PO đã có phiếu nhập không hủy được |

Sửa dòng PO Nháp: mở PO → **Sửa dòng hàng** (nguyên liệu, số lượng, đơn giá – trống = theo bảng giá NCC, ngày giao dự kiến).

### 4.4 Nhà cung cấp – `scm-supplier`, bảng giá – `scm-supplier-material`
- **Bấm vào dòng NCC** → mở **hồ sơ nhà cung cấp** cả trang (← về danh sách). Nút **Tổng quan** trên dòng vẫn mở hộp xem nhanh (tỷ lệ giao đúng hạn, tỷ lệ trả hàng 90 ngày, bảng giá, PO đang mở, sự cố gần đây); hộp có nút **Mở hồ sơ đầy đủ**.
- **Phần đầu hồ sơ:** tên, mã `NC-…`, loại NCC, trạng thái, MST, người liên hệ, điện thoại, địa chỉ; nút **Sửa**, **Thêm giá NL**, **Ghi sự cố**, ⋯ **In hồ sơ** / **Ngừng giao dịch** (nút hiện theo quyền). Không có "Tạo PO" – PO chỉ lập từ đề nghị mua.
- **4 thẻ chỉ số:** PO đang mở (số phụ: tổng giá trị) · Giao đúng hạn 90 ngày (số lần nhận) · Hàng bị loại 90 ngày (ngày nhận gần nhất) · Sự cố đang mở (/ tổng · số quá hạn).
- **Tab Tổng quan (UI-2R, D-69):** hai cột – **trái 1/3 "Thông tin nhà cung cấp"**: người liên hệ · điện thoại liên hệ + công ty · email · địa chỉ · loại NCC + số nguyên liệu / số ưu tiên · lần nhận hàng gần nhất (số phiếu); nhóm **Pháp lý** (tên đầy đủ, MST + sao chép, địa chỉ đăng ký), **Phân loại** (loại, trạng thái, bắt đầu giao dịch = PO / phiếu nhập sớm nhất), **Ghi chú**, **Hệ thống**. **Phải 2/3**: **Cần chú ý** (PO trễ hạn giao, sự cố quá hạn, lô tạm giữ / cận date, giá hết hiệu lực trong 15 ngày) → **Nguyên liệu cung cấp** (giá, hạn hiệu lực – tô cam khi còn ≤ 15 ngày, "thấp nhất trong N NCC", nhãn **Ưu tiên**; + Thêm giá, Xem bảng giá) cạnh **Đánh giá NCC** 90 ngày (giao đúng hạn, đạt QC, sự cố đúng hạn + số lần nhận / sự cố) → **Giá trị mua 6 tháng** (PO đặt và đã nhận – ghi sổ, triệu đồng; dòng tháng này) → **Đơn mua đang mở** (hẹn giao, giá trị, trạng thái hoặc "Trễ N ngày").
  - **Khối thông tin bên trái (UI-2R, dùng chung 3 màn chi tiết):** 6 dòng chính luôn hiện (điện thoại / email bấm để gọi / gửi thư); bên dưới là các **nhóm gập** – bấm tiêu đề nhóm (hoặc Tab + Enter) để mở / gập, dòng xám dưới tiêu đề là tóm tắt; trình duyệt **nhớ nhóm đang mở** cho lần sau. Ô trống không hiện; nhóm chưa có gì ghi "Chưa có thông tin · Bổ sung" (bấm Bổ sung = Sửa, chỉ khi có quyền sửa). Nút sao chép cạnh MST / CCCD. Màn rộng: khối này đứng yên khi cuộn; màn < 1200px: xếp trên cùng.
- **Các tab khác** (chỉ hiện khi có quyền xem màn tương ứng): **Bảng giá nguyên liệu** (lọc *Đang hiệu lực / Tất cả*, cột **Thấp nhất NCC khác** cùng nguyên liệu – tô đỏ khi NCC khác rẻ hơn, nút **Thêm giá NL**, **Ưu tiên**, **Sửa**) · **Đơn mua** (lọc ngày đặt, trạng thái) · **Nhận hàng và QC** (lọc ngày nhận) · **Lô nguyên liệu** (hạn dùng, QC, "cận date") · **Sự cố** (nút **Nhận xử lý / Đã xử lý / Đóng / Mở lại** như màn `qc-supplier-issue`) · **Tài liệu** · **Lịch sử** (+ Liên quan). Bấm dòng để mở chứng từ.
- **Bảng giá nhà cung cấp (`scm-supplier-material`) – bảng so sánh (UI-6.19, D-100):** chọn ngày xem giá (◀ ▶, Hôm nay); 4 thẻ: Nguyên liệu có giá / tổng · Chưa có NCC ưu tiên · Sắp hết hạn 7 ngày · Tăng giá ≥ 10%. Bảng **nguyên liệu (dòng, nhóm theo nhóm nguyên liệu – gập / mở) × nhà cung cấp (cột)**, mỗi ô **1 giá quy về ĐVT kho**; ô viền xanh = NCC ưu tiên, chấm xanh = rẻ nhất, rê chuột xem giá gốc + đơn vị, tối thiểu, số ngày giao, ngày giao, hiệu lực, % đổi lần trước. Chip **việc cần làm**: Chưa có giá · Chưa có NCC ưu tiên · Sắp hết hạn · Tăng giá ≥ 10% · NCC ưu tiên không còn rẻ nhất · Chưa quy đổi đơn vị. Bấm ô → form giá; ô trống **+ Thêm giá** (điền sẵn NCC + nguyên liệu); ★ trong ô = **Đặt ưu tiên**; chọn nhiều dòng → **Gia hạn hàng loạt**; **Nhập báo giá Excel**: chọn NCC → Tải mẫu (nguyên liệu đang có + giá hiện hành + cột Giá mới / Đơn vị / Áp dụng từ) → Tải lên → xem trước Tăng / Giảm / Mới / Lỗi → **Áp dụng** (mỗi dòng = giá mới); **Xuất Excel** bảng đang xem.
- **Form giá nguyên liệu của NCC (UI-6.19):** NCC* / Nguyên liệu* (gõ để tìm, mở từ trang NCC / nguyên liệu thì điền sẵn) · **Đơn giá*** (> 0) + **Giá tính theo*** (chỉ đơn vị quy đổi được về ĐVT kho; + tạo đơn vị đóng gói) – dưới ô hiện "= X đ / ĐVT kho" và "so với giá trước ±%" (tăng ≥ 10% → đỏ "đơn mua sẽ phải duyệt") · **Áp dụng từ ngày*** / Đến ngày (trống = chưa hết hạn) · Đặt tối thiểu · Số ngày giao · **Ngày giao trong tuần** (T2…CN / Hằng ngày) · Ưu tiên · Mã hàng của NCC · Yêu cầu chất lượng. Cột phải: **Lịch sử giá** (quy về ĐVT kho, %) + **So với NCC khác** (giá, ưu tiên, rẻ nhất, % đúng hẹn). **Luật lưu:** thêm giá mới → giá đang áp dụng của cùng NCC × nguyên liệu **tự kết thúc hôm trước** (không báo trùng); cùng ngày bắt đầu → "sửa giá ngày đó thay vì thêm"; **giá đã áp dụng không sửa đè** đơn giá / đơn vị – nút **Lưu giá mới từ ngày** (mặc định hôm nay); nút **Lưu thông tin phụ** chỉ đổi tối thiểu / ngày giao / mã hàng / yêu cầu / ngày kết thúc; bật ưu tiên → báo tên NCC bị bỏ ưu tiên.

### 4.5 Nguyên liệu, đơn vị – `scm-ingredient`, `scm-uom`, `scm-uom-conversion`
**Màn Đơn vị tính – gộp quy đổi (UI-6.22, D-103):** mục menu "Quy đổi đơn vị" đã ẩn. Danh sách: Mã · Đơn vị · Loại (**Gốc** / **Đóng gói**) · **= bao nhiêu đơn vị gốc** · **Đang dùng ở** (số nguyên liệu, giá NCC, dòng công thức, dòng chứng từ) · Trạng thái; lọc Tất cả / Gốc / Đóng gói. **+ Đơn vị gốc** (mã, tên, ký hiệu – kg, g, lít, ml là đơn vị chuẩn, khóa sửa). **+ Đơn vị đóng gói**: chọn loại bao bì (thùng / bao / khay / hộp / chai / can / túi / kiện / bình / lốc) + "1 đơn vị = [số] [đơn vị gốc]" → tên tự sinh (vd "thùng 10 kg"); đã có cùng kích cỡ → hệ thống chọn lại đơn vị đó. Đơn vị đã dùng không đổi được kích cỡ (tạo đơn vị mới). Bấm dòng để sửa; đơn vị gốc có link **Quy đổi khác** (giữa 2 đơn vị gốc khác loại – hiếm). Trong form Nguyên liệu, nút **+ Tạo đơn vị đóng gói** tạo ngay tại chỗ.

**Màn Nguyên liệu – nhóm ở cột trái (UI-6.21, D-102):** mục menu "Nhóm nguyên liệu" đã ẩn. Cột trái: **Tất cả nhóm** + từng nhóm (số nguyên liệu đang dùng) + **Chưa phân nhóm**; bấm nhóm = lọc bảng (giữ khi quay lại), tiêu đề bảng ghi "Tên nhóm · N nguyên liệu · thường bảo quản X, đơn vị Y"; nhóm trống có nút **+ Thêm nguyên liệu vào nhóm này**. **+ Thêm nhóm** (gõ tên, Enter lưu); rê chuột nhóm → **⋯ Đổi tên** (Enter lưu, Esc hủy) · **Ngừng dùng** (khóa khi nhóm còn nguyên liệu đang dùng – ghi "còn N nguyên liệu") · **Dùng lại**; nhóm ngừng dùng gạch ngang, xếp cuối, không có trong ô chọn của form. Tên nhóm không trùng (không phân biệt hoa thường); nhóm chỉ 1 cấp. Quyền theo màn Nguyên liệu (Thêm / Sửa). Bảng: Mã · Nguyên liệu · Nhóm · ĐVT kho · Bảo quản (Mát / Đông lạnh / Khô) · Hạn dùng · NCC ưu tiên. 4 thẻ: Nguyên liệu đang dùng · Chưa có giá NCC · Chưa phân nhóm · Số nhóm. Điện thoại: nhóm thành dải chip cuộn ngang. Trong form nguyên liệu, chọn nhóm → **gợi ý loại bảo quản + đơn vị kho** theo đa số nguyên liệu cùng nhóm ("Gợi ý theo N nguyên liệu cùng nhóm …", chỉ điền ô còn trống).

**Form Nguyên liệu (UI-6.20, D-101) – 5 nhóm** (dùng chung cho Thêm / Sửa ở danh sách, nút Sửa trang chi tiết và "+ Tạo nguyên liệu mới" ở tab Công thức):
1. **Thông tin chung:** Tên* · **Nhóm nguyên liệu*** (chọn danh mục, + Thêm nhóm theo quyền) · Quy cách · Mã (tự sinh, chỉ đọc).
2. **Đơn vị:** **Đơn vị kho*** · Đơn vị mua · Đơn vị xuất – 2 ô sau **chỉ liệt kê đơn vị quy đổi được về đơn vị kho** (ghi "= N kg"), để trống = đơn vị kho.
3. **Bảo quản & ATTP:** **Loại bảo quản*** chọn 1 trong 3 – **Khô / thường**, **Mát 0–5 °C**, **Đông lạnh ≤ −18 °C** (mỗi loại ghi ngưỡng nhận hàng); **Quản lý hạn dùng** (1 công tắc) → **Hạn dùng (ngày)***; **Quản lý theo lô** tự bật khi có hạn dùng (tắt thì báo "xuất theo hạn dùng cần quản lý lô"); **Kiểm tra khi nhận** tự bật với Mát / Đông lạnh (chỉ QA tắt được, có cảnh báo ATTP); Điều kiện bảo quản (mô tả).
4. **Định mức:** **Hao hụt sơ chế %** (dùng khi công thức không ghi hao hụt riêng) · **Tồn tối thiểu**, **Tồn an toàn** (theo đơn vị kho) – kho có định mức riêng (màn Định mức tồn kho) thì dùng định mức kho; form liệt kê các kho đang có định mức riêng.
5. **Ghi chú · Trạng thái** (Đang dùng / Ngừng dùng). Ngừng dùng nguyên liệu còn trong công thức / đề nghị / đơn mua chưa xong → vẫn lưu, báo *"Lưu ý: nguyên liệu còn trong …"* kèm số chứng từ.

Lỗi thường gặp: *Chọn nhóm nguyên liệu trong danh mục*; *Chọn đơn vị kho của nguyên liệu*; *Loại bảo quản phải là Khô / thường, Mát 0–5 °C hoặc Đông lạnh ≤ −18 °C*; *Đơn vị mua / xuất chưa quy đổi được về đơn vị kho*.

Danh mục chung. Lưu ý khi nhập nguyên liệu: kiểu bảo quản (Mát / Đông lạnh / thường – dùng để kiểm nhiệt độ nhận hàng), số ngày hạn dùng, tỷ lệ thu hồi, tồn an toàn. Quy đổi đơn vị phải có cho mọi cặp đơn vị công thức ↔ đơn vị tồn kho, thiếu thì MRP bỏ qua phần đó.
- *Nguyên liệu quản lý hạn dùng phải có số ngày hạn dùng lớn hơn 0*, *Tỷ lệ thu hồi phải trong (0, 100]*, *Hệ số quy đổi phải lớn hơn 0*, *Cặp quy đổi đơn vị này đã tồn tại*.

### 4.6 Trang chi tiết – đề nghị mua, đơn mua, nguyên liệu (UI-6)
Bấm một dòng ở **Đề nghị mua**, **Đơn mua** hoặc **Nguyên liệu** → mở **trang chi tiết toàn màn** (đường dẫn riêng, gửi được cho người khác; Ctrl + bấm hoặc bấm chuột giữa → mở tab mới). **← Danh sách** quay lại đúng bộ lọc, trang và vị trí cuộn đang xem. Quyền xem trang = quyền xem màn danh sách. Nút trên phần đầu là **đúng các nút trên dòng** của danh sách và chỉ hiện khi trạng thái / quyền cho phép (nút phụ nằm trong ⋯).
- **Đơn mua:** phần đầu số PO + trạng thái + nhãn "Giám đốc duyệt" (PO vượt ngưỡng), NCC, ngày đặt / giao dự kiến, đề nghị mua gốc (bấm để mở); **thanh tiến trình** Nháp → Chờ duyệt → Đã duyệt → Đã gửi NCC → Nhận một phần → Đã nhận đủ → Đóng (Hủy = đỏ). Nút **Nhận hàng**, **Gửi duyệt**, **Gửi NCC**, **Đóng PO**, **In đơn**; ⋯ **Hủy đơn mua**, **Mở màn Phê duyệt**. 4 thẻ: Giá trị gồm VAT (chưa VAT) · Đã nhận % (số dòng còn chờ) · Phiếu nhập · Bị loại QC. Tab: Tổng quan · Dòng hàng (đặt / đơn giá / VAT / đã nhận / chờ QC / còn lại) · **Phiếu nhập & QC** (mỗi phiếu: kho, phiếu QC, kết quả, nhận / đạt / loại – bấm số phiếu mở trang phiếu nhập) · Duyệt (khi đã gửi duyệt) · Chứng từ liên quan · Tài liệu · Lịch sử.
- **Đề nghị mua:** tiến trình Nháp → Chờ duyệt → Đã duyệt → Đã lập PO (Từ chối / Hủy = đỏ); nút **Lập PO**, **Gửi duyệt**; ⋯ **Hủy**, **Mở màn Phê duyệt**. 4 thẻ: Số dòng · Giá trị ước tính · NCC gợi ý (số dòng chưa có giá) · PO đã lập. Tab: Tổng quan · Dòng đề nghị (NCC gợi ý, đơn giá, ước tính) · Đơn mua đã lập · Duyệt · Tài liệu · Lịch sử.
- **Nguyên liệu (hồ sơ):** phần đầu tên, mã, nhóm, ĐVT, kiểu bảo quản, NCC ưu tiên; nhãn **Dưới tồn tối thiểu** / **N lô sắp hết hạn**. Nút **Sửa** (mở hộp sửa như cũ), **Danh sách lô**. 4 thẻ: Tồn khả dụng (tối thiểu) · Lô còn tồn (số lô hết hạn trong số ngày cảnh báo của chính sách tồn, mặc định 3) · **Giá mua gần nhất** (= đơn giá dòng PO đã duyệt mới nhất, không tính PO Nháp / Hủy) · Nhà cung cấp có bảng giá đang hiệu lực. Tab: Tổng quan (thông tin + tồn theo kho + lô còn tồn theo thứ tự nên xuất trước) · Lô · Giao dịch kho (100 dòng mới nhất) · **Giá & nhà cung cấp** (bảng giá NCC + lịch sử giá 30 dòng PO gần nhất) · Chứng từ liên quan · Lịch sử.

## 5. Trạng thái

| Chứng từ | Luồng |
|---|---|
| Nhu cầu nguyên liệu (từng dòng) | Đã tính → Thiếu / Đủ → Đã đề nghị mua |
| Đề nghị mua | Nháp → Chờ duyệt → Đã duyệt / Từ chối → Đã lập PO; Hủy |
| Đơn mua (PO) | Nháp → Chờ duyệt → Đã duyệt → Đã gửi NCC → Nhận một phần → Đã nhận đủ → Đóng; Hủy |

## 6. Lỗi / cảnh báo thường gặp

| Thông báo | Cách xử lý |
|---|---|
| Không có đơn Đã chốt cho ngày … – chốt đơn trước khi tính nhu cầu | Chờ 20:00 hoặc nhờ KD chốt đơn |
| Kế hoạch sản xuất … đã ở trạng thái … – không tính lại nhu cầu | Kế hoạch đã duyệt – dùng **Tính bổ sung** |
| Đề nghị mua … của lần tính trước đang ở trạng thái … – không tính lại được… | Dùng **Tính bổ sung**, hoặc từ chối / hủy đề nghị cũ |
| Không thiếu thêm so với các đề nghị mua đã lập – không lập đề nghị mua bổ sung | Đủ hàng |
| Cảnh báo: nguyên liệu … thiếu quy đổi đơn vị … | Thêm quy đổi đơn vị rồi tính lại |
| Cảnh báo: NCC … cần … ngày giao – đặt hôm nay không kịp ngày … | Đổi NCC ưu tiên / mua ngoài |
| Chưa có NCC đang hiệu lực cho: … – thêm bảng giá NCC rồi lập PO | Thêm dòng ở `scm-supplier-material` |
| PO tổng … đ ≥ 50.000.000 đ – cần Giám đốc duyệt | Bình thường – chờ Giám đốc |
| PO đã có phiếu nhập … – không hủy được (đóng PO bằng Close) | Dùng **Đóng** |
| PO còn phiếu nhập chưa ghi sổ: … | Thủ kho ghi sổ / hủy phiếu trước khi đóng |
| Nhà cung cấp đang ngừng giao dịch | Chọn NCC khác |
| Tỷ lệ dự phòng từ 0 tới 100 (%) / Chọn ít nhất một nguyên liệu | Sửa ô dự phòng / tick dòng |

## 7. Trên bot Telegram

Phân hệ **📦 Kho & mua hàng** (bot Vận hành):
- 🥬 **NL ngày mai** (`/nlmai`) – nhu cầu + thiếu hụt ngày mai.
- 🧾 **PO sắp về / trễ** (`/po`).
- 🚛 **Hàng về hôm nay** (`/hangve`).
- 📦 **Tồn dưới mức** (`/ton`), ⌛ **Cận date** (`/candate`).
- Thông báo: thiếu nguyên liệu sau MRP, PO trễ hẹn, 🔴 QC đầu vào không đạt.
- Duyệt đề nghị mua (cấp 1) trên bot: **⏳ Chờ tôi duyệt** – mức rủi ro **cao**, xác nhận 2 bước (xem `20_BOT_TELEGRAM.md`).
