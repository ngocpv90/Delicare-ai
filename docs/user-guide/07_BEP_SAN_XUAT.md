# 07 – Bếp trung tâm / sản xuất

## 1. Bạn là ai
> **BIZ-2.7 / 2.8 – Mẻ tự Đạt, tự lệnh đóng gói:** sau Kết thúc nấu, mẻ đủ điều kiện – lõi gần nhất ≥ 70 °C, **đã ghi sản lượng**, **đã lưu mẫu**, không tiêu chí trọng yếu FAIL đang mở – **tự Đạt QC** (không cần bấm Đạt). Thiếu điều kiện → giữ Chờ QC, màn báo còn thiếu gì. QA vẫn **Tạm giữ** (cả mẻ đã tự Đạt, chưa đóng gói → về Chờ QC) / **Loại** được. Mẻ Đạt (tay / tự) → **tự sinh lệnh đóng gói** theo đơn; đóng gói hoàn tất bằng lệnh đóng gói (Bắt đầu → Ghi kết quả).


- Nhóm quyền: **OPS-KIT** (1.3 Bếp trung tâm – bếp trưởng, trưởng ca, tổ trưởng các tổ Sơ chế, Nấu, Cơm / Canh, Chia suất / Đóng gói, Vệ sinh, QA bếp).
- Bot dùng: **Vận hành** (phân hệ 🍳 Bếp) – kênh chính của công nhân và tổ trưởng; QR dán ở dây chuyền / bàn nấu. Bếp trưởng được cấp thêm bot **Điều hành**.
- Việc chính: chạy mẻ theo bảng điều độ (bắt đầu → đo nhiệt độ lõi → nấu xong + sản lượng → lưu mẫu), checklist ATTP theo ca, ghi hao hụt, theo dõi lệnh sản xuất.

## 2. Màn hình được dùng

<!-- MENU5:BEGIN – bảng sinh từ ma trận D-79 (tools/AppDelicare.CodeGen --layers security), không sửa tay -->
Quyền theo ma trận D-79 (đủ 19 nhóm: `docs/claude/MENU5_MATRIX.md`).

| Menu › Mục | screen-id | OPS-KIT | OPS-MNT |
|---|---|---|---|
| Việc của tôi › Việc của tôi | `hr-my` | X | X |
| Phê duyệt › Lệnh của agent | `wf-inbox?type=ai-agent-cmd` | Duyệt | — |
| Phê duyệt › Duyệt SOP | `wf-inbox?type=kb-sop` | Duyệt | — |
| Bán lẻ › Đơn khách lẻ | `sal-retail-order` | X (IX) | — |
| Thực đơn & Món ăn › Thực đơn tuần | `menu-menu` | X | — |
| Thực đơn & Món ăn › Công thức chế biến | `menu-recipe` | X | — |
| Thực đơn & Món ăn › Món ăn | `menu-dish` | X | — |
| Kho › Xuất kho sản xuất | `scm-issue` | X | — |
| Kho › Tồn kho | `scm-inv-balance` | X | — |
| Sản xuất › Kế hoạch sản xuất | `prod-plan` | X | — |
| Sản xuất › Mẻ nấu | `prod-batch` | S | X |
| Sản xuất › Hao hụt | `prod-waste` | S | — |
| Chất lượng & ATTP › Kiểm tra theo ca / mẻ | `qc-checklist-result` | S | — |
| Chất lượng & ATTP › Nhật ký nhiệt độ | `qc-temperature` | S | X |
| Chất lượng & ATTP › Vệ sinh khay hộp | `qc-cleaning` | S | — |
| Chất lượng & ATTP › Bộ kiểm tra ATTP | `qc-checklist` | X | — |
| Đóng gói & Giao hàng › Chia suất & đóng gói | `log-packing-order` | S | — |
| Đóng gói & Giao hàng › Loại hộp / khay | `log-packaging-type` | X | — |
| Đóng gói & Giao hàng › Định mức bao bì | `log-packaging-norm` | X | — |
| Nhân sự › Phân ca | `hr-shift-assign` | X | — |
| Nhân sự › Chấm công | `hr-attendance` | X | — |
| Nhân sự › Nghỉ & đổi ca | `hr-leave` | X | — |
| Danh mục hệ thống › Ca làm việc | `hr-shift` | X | — |
| Danh mục hệ thống › Dây chuyền & trạm bếp | `prod-line` | X | S |
| Tri thức › Kho tri thức | `knowledge` | X | X |
| Tri thức › 30 Thực đơn & Công thức | `knowledge?folder=30` | S | — |
| Tri thức › 45 ATTP | `knowledge?folder=45` | X | — |
| Tri thức › 50 Sản xuất | `knowledge?folder=50` | S | — |
| Tri thức › 80 Đào tạo | `knowledge?folder=80` | X | X |
| Tri thức › 99 Mẫu | `knowledge?folder=99` | X | X |
| Trợ lý AI › Việc agent theo dõi | `ai-tasks` | S | — |
<!-- MENU5:END -->

## 3. Việc trong ca

| Thời điểm | Việc | Ở đâu |
|---|---|---|
| 15′ trước ca | Nhận **Thẻ ca** trên bot (ca, việc chính, checklist, việc tồn) | bot |
| Vào ca | **📍 Chấm công** (bot, gửi vị trí) | bot |
| Đầu ca | Kiểm kho đã xuất nguyên liệu cho mẻ (tab Nguyên liệu & an toàn) | `prod-batch` |
| Từng mẻ | **Bắt đầu nấu** → **Kết thúc nấu** (đo nhiệt độ lõi ≥ 70 °C + sản lượng) → **Ghi lưu mẫu** | `prod-batch` / bot |
| Theo hạn | Checklist ATTP của ca | `qc-checklist-result` / bot |
| Sau QC đạt | **Tạo lệnh đóng gói** (hoặc tổ đóng gói tự lập) | `prod-batch` |
| Có dư / hỏng | **Ghi hao hụt** | `prod-batch` / `prod-waste` |
| Ra ca | **📍 Chấm công** ra ca | bot |

## 4. Thao tác từng bước

### 4.0 Mẻ nấu – 1 chạm mỗi mẻ (UI-6.25, D-106)
Màn **Mẻ nấu** xem **một ngày** (◀ ngày ▶ hoặc chọn ngày) và **một ca** (4 thẻ ca: giờ xuất bếp, x/y mẻ xong, số mẻ trễ, thanh tiến độ – mặc định ca đang chạy). Công tắc **Dây chuyền / Bếp trưởng** (máy nhớ lựa chọn).
- **Chế độ Dây chuyền** (bếp đứng máy): chọn dây chuyền (máy nhớ) → khối **ĐANG NẤU** (món, suất, giờ, đã lưu mẫu món trong ca chưa) với nút to **Xong mẻ** và **Ghi hao hụt**; chưa có mẻ đang nấu → nút to **Bắt đầu nấu** mẻ kế tiếp. Khối **Mẻ chờ QC – cần bổ sung** (lý do chưa tự Đạt + nút **Đã lưu mẫu**; QA có Đạt / Tạm giữ / Loại). Cột **Tiếp theo** (trễ tô đỏ) và **Đã xong trong ca**.
- **Hộp Xong mẻ**: Nhiệt độ lõi* (bàn phím số to; báo Đạt / Dưới ngưỡng ngay – dưới ngưỡng phải nhập hành động khắc phục) · Số suất nấu được (mặc định kế hoạch, nút −/+) · **Lưu mẫu món này trong ca** (đã lưu ở mẻ trước → hiện giờ đã lưu; chưa → ô xác nhận **không tích sẵn**) · Hao hụt trong mẻ (Hư hỏng / Hao hụt sơ chế / Hỏng khi đóng gói + số suất). Nút **Xong mẻ** hoặc **Xong & bắt đầu <món kế tiếp> (giờ)** (cùng dây chuyền). Đủ điều kiện → "hệ thống tự Đạt QC"; thiếu → "Chờ QC: <thiếu gì>". Tất cả ghi trong 1 lần – lỗi thì không ghi gì.
- **Lưu mẫu theo món × ca**: mẻ đầu của món trong ca lưu mẫu, các mẻ sau dùng lại (tự Đạt và xuất chuyến đều tính theo món × ca). Ghi lưu mẫu lần nữa → báo "Món này trong ca đã lưu mẫu lúc …".
- **Chế độ Bếp trưởng**: 4 chỉ số (Mẻ của ca · Đang nấu · Tự Đạt QC / cần QA xem · Trễ / cần xử lý); khối **Cần làm ngay** (đến giờ chưa bắt đầu – nút Bắt đầu / **Bắt đầu cả n mẻ**; quá giờ xong – Xong mẻ; chờ QC quá 20 phút – mở mẻ); tab **Theo giờ** (mỗi dây chuyền một dòng, mẻ là thanh theo giờ, màu theo trạng thái, viền đỏ = trễ, viền đứt = nấu thêm, vạch Bây giờ và Hạn xong nấu; bấm thanh mở trang mẻ); tab **Sản lượng & hao hụt ca** theo món (mẻ xong, kế hoạch, nấu được, đạt, loại QC, hao hụt khác, %).
- Ngưỡng trễ trong cấu hình: `BatchLateStartMinutes` (10 phút), `BatchQcWaitWarnMinutes` (20 phút). Nút **Bảng sản xuất ngày** mở trang kế hoạch của ngày.

### 4.1 Bảng điều độ mẻ – `prod-batch` (cách cũ – chế độ DEMO)
1. Chọn **Ngày sản xuất** (chip 5 ngày gần nhất, hoặc ô chọn ngày).
2. Dòng trạng thái đếm mẻ: Kế hoạch · Đang nấu · Chờ QC · Đạt · Đã đóng gói · Không đạt.
3. Thẻ số liệu: Số mẻ (số lệnh), Phần món kế hoạch, Phần món đạt QC, Tạm giữ / Loại.
4. Lưới **Ca × Dây chuyền** (mỗi dây chuyền ghi suất / mẻ, phút / mẻ; cột "Chưa gán dây chuyền" nếu có). Mỗi thẻ mẻ: món, số mẻ `ME-…`, số suất, **Lõi …°C ✓/✗** hoặc "Chưa đo nhiệt độ lõi", "đã / chưa lưu mẫu", trạng thái, QC.
5. Bấm thẻ mẻ → ngăn chi tiết (thanh tiến trình, lệnh SX + ca, công thức phiên bản khóa, nhiệt độ lõi, checklist theo mẻ; tab **Nguyên liệu & an toàn**).

**Trang chi tiết mẻ (UI-6):** chế độ dữ liệu thật, bấm thẻ mẻ → trang `#/prod-batch-detail?id=…` thay ngăn chi tiết: thanh bước Kế hoạch → Đang nấu → Chờ QC → Đạt → Đã đóng gói (Không đạt: đỏ); nút theo trạng thái như bảng 4.2 (**Bắt đầu nấu**, **Kết thúc nấu → Chờ QC**, **Ghi sản lượng**, **Ghi lưu mẫu**, **QC: Đạt / Tạm giữ / Loại mẻ**, **Tạo lệnh đóng gói**; ⋯ → Ghi hao hụt, In phiếu sản xuất, Mở lệnh sản xuất); 4 chỉ số Sản lượng, Nhiệt độ lõi (so ngưỡng), Công đoạn (xong / tổng), Hao hụt. Tổng quan: thông tin mẻ (lệnh, kế hoạch, ngày, ca, dây chuyền, công thức khóa, giờ), điều kiện còn thiếu, an toàn thực phẩm, nguyên liệu theo định lượng. Tab **QC & an toàn** (nhiệt độ lõi từng lần đo + khắc phục, lưu mẫu), **Công đoạn** (Bắt đầu / Xong), **Nguyên liệu & xuất kho**. "← Danh sách" quay lại đúng bộ lọc / trang / vị trí cuộn; Ctrl + bấm hoặc chuột giữa → mở tab mới; tab chung **Chứng từ liên quan / Tài liệu / Lịch sử**. Quyền = quyền xem màn danh sách. Chế độ DEMO vẫn mở ngăn chi tiết.

### 4.2 Chạy một mẻ
| Trạng thái mẻ | Nút | Thao tác |
|---|---|---|
| Kế hoạch | **Bắt đầu nấu** | → Đang nấu (lệnh SX → Đang SX) |
| Đang nấu | **Kết thúc nấu → Chờ QC** | Hộp **Ghi nhận sản lượng & nhiệt độ lõi**: nhập **Sản lượng thực tế** (số phần) và **Nhiệt độ lõi °C** → **Lưu**. Hệ thống ghi nhiệt độ lõi → nấu xong → ghi sản lượng; dừng ở bước đầu tiên bị từ chối |
| Đang nấu / Chờ QC / Đạt / Đã đóng gói / Không đạt | **Ghi hao hụt** | Loại hao hụt*, Số suất / Kg, Lý do, Cách xử lý |
| Chờ QC | **Ghi lưu mẫu** | Ghi "Đã lưu mẫu" (bắt buộc trước khi xe xuất phát) |
| Chờ QC | **QC: PASS / HOLD / REJECT** | Việc của QA (`06_QC_ATTP.md` 4.5) |
| Đạt | **Tạo lệnh đóng gói** | Sinh lệnh đóng gói từ mẻ (chia số phần đạt cho các đơn Đã chốt / Đang SX của ngày) |
| Đạt | **Loại mẻ** | Ghi chú bắt buộc |

**Nhiệt độ lõi:** ngưỡng đạt hiện trên hộp nhập (cấu hình, mặc định **≥ 70 °C**); ô nhiệt độ để trống, nhập số đo thật. Đo dưới ngưỡng: hiện ô **Hành động khắc phục** (bắt buộc, vd. *gia nhiệt lại 10 phút*) – trên bot, đo dưới ngưỡng thì bot hiện nút **hành động khắc phục** hay gặp (✏️ Khác… mới phải gõ). Mẻ chưa cho Đạt QC được tới khi đo lại đạt.

### 4.3 Bảng sản xuất ngày & lệnh sản xuất (UI-6.24 – menu Sản xuất còn 4 mục: Kế hoạch sản xuất · Mẻ nấu · Hao hụt · Dây chuyền & trạm bếp)
- Màn **Mẻ nấu** giữ bảng điều độ ca × dây chuyền; nút **Bảng sản xuất ngày** mở trang kế hoạch của ngày đang xem (ma trận món × ca, tiến độ mẻ) – xem `03_KE_HOACH.md` mục 4.7.
- Tab **Sản lượng** trong Mẻ nấu: danh sách chỉ xem (sản lượng ghi ở mẻ – Kết thúc nấu / Loại mẻ). Link cũ `#/prod-output` mở tab này.
- **Dây chuyền & trạm bếp** (UI-6.26, D-107) – 2 tab:
  - **Dây chuyền**: mỗi dây chuyền có Tên, **Công suất (suất / mẻ)**, **Thời gian 1 mẻ (phút)**, **Nhóm món đang gán**, Trạng thái. Khi ra lệnh sản xuất, mỗi món nấu ở **dây chuyền của nhóm món** (nhóm con trống → nhóm cha): số mẻ = suất ÷ công suất (làm tròn lên), mỗi mẻ đúng số phút của dây chuyền. Nhóm chưa gán (hoặc dây chuyền đã ngừng) → món nấu **1 mẻ** + cảnh báo "Món X (nhóm Y) chưa gán dây chuyền – nấu 1 mẻ".
  - Đầu màn cảnh báo "N nhóm món chưa gán dây chuyền" + nút **Gán nhanh** (bảng nhóm món × ô chọn dây chuyền, lưu một lần). **Thêm dây chuyền / Sửa**: Tên*, Công suất*, Thời gian 1 mẻ*, Trạng thái, **Nhóm món** (tick nhiều – tick nhóm đang ở dây chuyền khác = chuyển sang), Ghi chú; mã tự sinh DC-NNN. Dây chuyền Ngừng hoạt động không gán được cho nhóm.
  - **Bàn nấu** (trước là Trạm bếp): Mã, Tên bàn nấu, Khu vực, Ghi chú, Trạng thái + **In QR** – không còn tọa độ / bán kính. Bắt đầu mẻ không hỏi bàn nấu (bot / QR vẫn ghi kèm). Link cũ `#/prod-workstation` mở tab này.
  - Ở màn **Món ăn**, cột nhóm bên trái hiện dây chuyền của từng nhóm (cam = chưa gán); ⋯ → **Gán dây chuyền**.
- Lệnh sản xuất không tạo tay; **lệnh tự đóng** khi mẻ cuối đóng gói / loại (chênh lệch, giá thành, trả nguyên liệu giữ chỗ dư). Chi tiết trang lệnh: `03_KE_HOACH.md` mục 4.8.

### 4.4 Kế hoạch sản xuất – `prod-plan`
Bếp xem kế hoạch ngày, **Xuất phiếu sản xuất** (nút xuất trên dòng). Lập / ra lệnh xem `03_KE_HOACH.md` mục 4.7.

### 4.5 Hao hụt – `prod-waste`
Xem `03_KE_HOACH.md` mục 4.9. Hao hụt gắn mẻ nên ghi ngay trên mẻ (nút **Ghi hao hụt**) để số hao hụt của mẻ được cập nhật.

### 4.6 Checklist, nhiệt độ, vệ sinh
Xem `06_QC_ATTP.md` mục 4.3, 4.4, 4.6.

## 5. Trạng thái

| Chứng từ | Luồng |
|---|---|
| Mẻ | Kế hoạch → Đang nấu → Chờ QC → Đạt / Không đạt → Đã đóng gói; Hủy |
| QC mẻ | Đạt / Tạm giữ / Từ chối |
| Lệnh SX | Kế hoạch → Phát hành → Đang SX → Hoàn thành → Đóng; Hủy |
| Công đoạn | Chưa làm → Đang làm → Đã xong (nút **Bắt đầu / Xong** ở tab Công đoạn trên web, hoặc trên thẻ mẻ ở bot) |

## 6. Lỗi / cảnh báo thường gặp

| Thông báo | Cách xử lý |
|---|---|
| Nhiệt độ lõi … °C dưới ngưỡng 70 °C – phải ghi hành động khắc phục… | Nấu tiếp / hâm lại, đo lại ≥ 70 °C |
| Cảnh báo: nhiệt độ lõi … dưới ngưỡng – mẻ bị chặn đạt QC / đóng gói tới khi đo lại đạt | Đo lại |
| Chưa đo nhiệt độ lõi / Chưa ghi sản lượng mẻ | Dùng **Kết thúc nấu** đủ 2 ô |
| Số phần nấu được phải lớn hơn 0… | Nhập sản lượng |
| Mẻ đã ghi lưu mẫu lúc … | Đã lưu – không cần ghi lại |
| Chỉ … khi mẻ ở trạng thái … | Làm đúng thứ tự Bắt đầu → Kết thúc → QC |
| Mẻ …: phải xong công đoạn Nấu trước khi nấu xong mẻ | Mở mẻ → tab **Công đoạn** → **Bắt đầu / Xong** từng công đoạn theo thứ tự (trên bot: nút **▶️ Bắt đầu …** / **⏹ Xong …** trên thẻ mẻ) |
| Mẻ có tiêu chí ATTP trọng yếu chưa đạt | QA kiểm lại tiêu chí sau khắc phục |
| Cảnh báo: mẻ … chưa có phiếu xuất nguyên liệu đã ghi sổ | Báo kho xuất nguyên liệu |
| Đang tải dữ liệu mẻ – thử lại sau giây lát | Bấm lại sau vài giây |

## 7. Trên bot Telegram

Phân hệ **🍳 Bếp** (bot Vận hành):
- 🍳 **Mẻ cần làm** (`/me`) – mặc định mẻ **ca hiện tại** của tổ tôi (mẻ trễ / nhiệt độ chưa đạt lên đầu), 6 mẻ / trang, **📋 Xem tất cả** để xem cả ngày. Mỗi mẻ: **▶️ Bắt đầu**; mẻ Đang nấu có **hàng nhiệt độ lõi 70° 72° 75° 78° 80° bấm 1 chạm** hoặc **🌡 Nhiệt độ khác** (màn số: −10 / −1 / +1 / +10 rồi ✅ Xác nhận); **✅ Xong mẻ** (số suất điền sẵn theo kế hoạch, chỉnh trên màn số); **♻️ Ghi thừa**, **➕ Nấu thêm** (màn số + nút lý do); **🧪 Lưu mẫu**, **🔄 Làm mới**. Ghi nhanh nhiệt độ: **⭐ Mẻ cần làm → ME-… → 78°** (3 chạm). Sau mỗi thao tác, đầu thẻ hiện dòng **✅ … · HH:mm** và thẻ giữ nút bước kế tiếp; **◀ Quay lại** về đúng màn trước, không lặp lại thao tác ghi. Mẻ đang nấu có theo dõi công đoạn: thẻ hiện dòng **Công đoạn: Sơ chế ✅ → Nấu ⏳ → Chia suất** và một nút cho công đoạn hiện tại – **▶️ Bắt đầu <công đoạn>** hoặc **⏹ Xong <công đoạn>** (làm lần lượt theo thứ tự).
- 📈 **Tiến độ ca** (`/tiendo`) – suất đạt / kế hoạch hôm nay.
- **QR dây chuyền / bàn nấu** (`?start=LINE_<mã>`): mở mẻ đang nấu / chờ nấu tại đó.
- **✅ Xong mẻ** trên bot ghi luôn **sản lượng** (số suất trên màn số) → mẻ sang Chờ QC, QA cho Đạt được ngay. Mẻ Chờ QC chưa có sản lượng (kết thúc từ bản cũ): ngăn chi tiết mẻ trên web có nút **Ghi sản lượng**.
- Thông báo: 🔴 mẻ trễ > 15′, 🔴 nhiệt độ lõi < 70 °C, 🔴 lỗi trọng yếu checklist, 🟡 checklist sắp hạn, 🟡 người tổ chưa vào ca, 🟡 kế hoạch đã duyệt.
