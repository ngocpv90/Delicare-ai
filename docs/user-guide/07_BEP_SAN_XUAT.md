# 07 – Bếp trung tâm / sản xuất

## 1. Bạn là ai
> **BIZ-2.7 / 2.8 – Mẻ tự Đạt, tự lệnh đóng gói:** sau Kết thúc nấu, mẻ đủ điều kiện – lõi gần nhất ≥ 70 °C, **đã ghi sản lượng**, **đã lưu mẫu**, không tiêu chí trọng yếu FAIL đang mở – **tự Đạt QC** (không cần bấm Đạt). Thiếu điều kiện → giữ Chờ QC, màn báo còn thiếu gì. QA vẫn **Tạm giữ** (cả mẻ đã tự Đạt, chưa đóng gói → về Chờ QC) / **Loại** được. Mẻ Đạt (tay / tự) → **tự sinh lệnh đóng gói** theo đơn; đóng gói hoàn tất bằng lệnh đóng gói (Bắt đầu → Ghi kết quả).


- Nhóm quyền: **OPS-KIT** (1.3 Bếp trung tâm – bếp trưởng, trưởng ca, tổ trưởng các tổ Sơ chế, Nấu, Cơm / Canh, Chia suất / Đóng gói, Vệ sinh, QA bếp).
- Bot dùng: **Vận hành** (phân hệ 🍳 Bếp) – kênh chính của công nhân và tổ trưởng; QR dán ở dây chuyền / bàn nấu. Bếp trưởng được cấp thêm bot **Điều hành**.
- Việc chính: chạy mẻ theo bảng điều độ (bắt đầu → đo nhiệt độ lõi → nấu xong + sản lượng → lưu mẫu), checklist ATTP theo ca, ghi hao hụt, theo dõi lệnh sản xuất.

## 2. Màn hình được dùng

| screen-id | Menu › Mục | Quyền |
|---|---|---|
| `prod-batch` | Sản xuất & Giá thành › Mẻ sản xuất | Xem, sửa |
| `prod-order` | … › Lệnh sản xuất | Xem, sửa |
| `prod-plan` | … › Kế hoạch sản xuất | Xem, sửa (duyệt: COO) |
| `prod-waste` | … › Hao hụt thực phẩm | Xem, sửa |
| `prod-line`, `prod-workstation` | … › Dây chuyền, Khu vực & trạm làm việc | Xem, sửa |
| `cost-actual`, `cost-meal`, `cost-standard` | … giá thành | Xem, sửa |
| `qc-checklist-result`, `qc-temperature`, `qc-cleaning`, `qc-cleaning-result` | QC & ATTP | Xem, sửa (xem `06_QC_ATTP.md`) |
| `scm-issue`, `scm-reservation`, `scm-inv-balance` | Mua hàng & Kho | Xem |
| `hr-shift-assign`, `hr-attendance` | Nhân sự › Phân ca, Chấm công | Xem |

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

### 4.1 Bảng điều độ mẻ – `prod-batch`
1. Chọn **Ngày sản xuất** (chip 5 ngày gần nhất, hoặc ô chọn ngày).
2. Dòng trạng thái đếm mẻ: Kế hoạch · Đang nấu · Chờ QC · Đạt · Đã đóng gói · Không đạt.
3. Thẻ số liệu: Số mẻ (số lệnh), Phần món kế hoạch, Phần món đạt QC, Tạm giữ / Loại.
4. Lưới **Ca × Dây chuyền** (mỗi dây chuyền ghi suất / mẻ, phút / mẻ; cột "Chưa gán dây chuyền" nếu có). Mỗi thẻ mẻ: món, số mẻ `ME-…`, số suất, **Lõi …°C ✓/✗** hoặc "Chưa đo nhiệt độ lõi", "đã / chưa lưu mẫu", trạng thái, QC.
5. Bấm thẻ mẻ → ngăn chi tiết (thanh tiến trình, lệnh SX + ca, công thức phiên bản khóa, nhiệt độ lõi, checklist theo mẻ; tab **Nguyên liệu & an toàn**).

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

**Nhiệt độ lõi:** ngưỡng đạt hiện trên hộp nhập (cấu hình, mặc định **≥ 70 °C**); ô nhiệt độ để trống, nhập số đo thật. Đo dưới ngưỡng: hiện ô **Hành động khắc phục** (bắt buộc, vd. *gia nhiệt lại 10 phút*) – trên bot, bot tự hỏi hành động khắc phục sau khi gõ nhiệt độ. Mẻ chưa cho Đạt QC được tới khi đo lại đạt.

### 4.3 Lệnh sản xuất – `prod-order`
Xem `03_KE_HOACH.md` mục 4.8 (tab Mẻ & nguyên liệu, Giá thành & chênh lệch; **Đóng lệnh** khi Hoàn thành; **Hủy lệnh** khi chưa mẻ nào bắt đầu).

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
- 🍳 **Mẻ cần làm** (`/me`) – mẻ hôm nay (của tổ tôi). Mỗi mẻ: **▶️ Bắt đầu**, **🌡 Nhiệt độ lõi** (nút đặt sẵn 70 / 72 / 75 / 78 / 80 / 85 °C hoặc **Khác…** để gõ), **✅ Xong mẻ** (số suất thực tế: **✅ Đúng N** hoặc **Khác…**), **🧪 Lưu mẫu**, **🔄 Làm mới**. Mẻ đang nấu có theo dõi công đoạn: thẻ hiện dòng **Công đoạn: Sơ chế ✅ → Nấu ⏳ → Chia suất** và một nút cho công đoạn hiện tại – **▶️ Bắt đầu <công đoạn>** hoặc **⏹ Xong <công đoạn>** (làm lần lượt theo thứ tự).
- 📈 **Tiến độ ca** (`/tiendo`) – suất đạt / kế hoạch hôm nay.
- **QR dây chuyền / bàn nấu** (`?start=LINE_<mã>`): mở mẻ đang nấu / chờ nấu tại đó.
- **✅ Xong mẻ** trên bot ghi luôn **sản lượng** (số suất vừa chọn / gõ) → mẻ sang Chờ QC, QA cho Đạt được ngay. Mẻ Chờ QC chưa có sản lượng (kết thúc từ bản cũ): ngăn chi tiết mẻ trên web có nút **Ghi sản lượng**.
- Thông báo: 🔴 mẻ trễ > 15′, 🔴 nhiệt độ lõi < 70 °C, 🔴 lỗi trọng yếu checklist, 🟡 checklist sắp hạn, 🟡 người tổ chưa vào ca, 🟡 kế hoạch đã duyệt.
