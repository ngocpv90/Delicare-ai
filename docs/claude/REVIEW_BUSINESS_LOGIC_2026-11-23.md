# Rà soát logic nghiệp vụ và dữ liệu toàn hệ thống – 2026-11-23

> **Phạm vi:** toàn bộ luồng từ hợp đồng đến đối soát, kèm kho, QC, sản xuất, giao vận, nhân sự và duyệt.
> **Cách rà:** đối chiếu quy tắc nghiệp vụ (`BUSINESS_RULES.md`), hướng dẫn sử dụng 02–13 (mô tả đúng hành vi code hiện tại), file vận hành thật `New_Menu_Delicare_202611_W3.xlsx` và cách vận hành anh đã mô tả.
> **Anh duyệt toàn bộ 2026-11-23 → D-77, ROADMAP BIZ-1 / BIZ-2 / BIZ-3.** Mục ghi *(cần kiểm code)* thì Claude Code xác nhận trước khi sửa. Câu hỏi mục E đã chốt theo đề xuất (xem D-77).

Ký hiệu: 🔴 sai nghiệp vụ, gây thiệt hại thật · 🟠 thừa bước hoặc dễ hiểu lầm · 🟢 nên để hệ thống tự làm.

---

## A. Sai nghiệp vụ / dữ liệu – nên sửa trước khi chạy thật

### 🔴 A1. Mua nguyên liệu quá muộn, không kịp nấu
- **Hiện tại:** đơn ngày D chốt lúc 20:00 ngày D-1 → 20:30 mới tính nhu cầu nguyên liệu → 21:00 lập đề nghị mua → duyệt đề nghị 2 cấp → lập PO → duyệt PO → gửi NCC → nhận hàng → QC → nhập kho → xuất kho.
- **Vì sao sai:** ca **Sáng 6h** ngày D phải nấu từ khoảng 3–4h sáng. Chuỗi hơn 8 bước, 3–4 lần duyệt chạy qua đêm là không thể làm kịp. File thật của anh (sheet *5. Plan_Order*) cũng lập **kế hoạch order cả tuần** từ trước.
- **Đề xuất:**
  1. **Kế hoạch mua theo tuần:** khi khách gửi đăng ký tuần (D-74) và có dự báo (D-76), hệ thống tính nhu cầu **cho từng ngày của tuần tới**. Ngày đã có đăng ký thì lấy số đăng ký, chưa có thì lấy dự báo. Từ đó lập đề nghị mua **theo ngày cần hàng** và lead time của NCC.
  2. **20:00 hằng ngày chỉ còn bước tính bổ sung:** so số chốt với số đã mua, thiếu thì mua bổ sung. Đây là nút "Tính bổ sung" đã có.
  3. **Trừ tồn lũy kế khi tính nhiều ngày:** không lấy cùng một tồn kho tính cho cả D+1 lẫn D+2 *(cần kiểm code)*.

### 🔴 A2. Công thức hao hụt khác file thật – mua thiếu nguyên liệu
- **File thật** (sheet *Thứ 2*, món *Thịt hầm đậu đỏ*): định lượng sau chế biến 70 g, hao hụt chế biến 35 %, hao hụt sơ chế 5 %.
  - Sau sơ chế = 70 ÷ (1 − 0,35) = 107,7 g
  - Trước sơ chế = 107,7 ÷ (1 − 0,05) = **113,4 g / suất**
- **Hệ thống** (BUSINESS_RULES §3, chốt T14.1): nhu cầu = định lượng × (1 + hao hụt %).
  - Nhân cả 2 tầng: 70 × 1,35 × 1,05 = 99,2 g → **thiếu 12,5 %**.
  - Nếu chỉ áp 1 tỉ lệ (màn công thức chỉ có một ô "Hao hụt %"): 70 × 1,35 = 94,5 g → **thiếu 17 %**.
- Với 4.500 suất, món này thiếu khoảng **64–85 kg thịt mỗi bữa**.
- **Đề xuất:** đổi sang công thức chia như file thật, dùng đủ 2 tầng: hao hụt sơ chế theo nguyên liệu × hao hụt chế biến theo phương pháp. Màn công thức hiện **3 cột** giống file: định lượng tinh, sau sơ chế, trước sơ chế (lượng mua) *(cần kiểm code: đang dùng 1 hay 2 tầng)*.

### 🔴 A3. Đối soát tính tiền theo "suất phục vụ" thay vì số khách đã nhận
- **Hiện tại:** tiền = giá trị đơn × suất **phục vụ thực tế** ÷ suất đặt (`ReconciliationBillingBasis = SERVED`). Suất phục vụ do điểm phát nhập sau bữa, nghĩa là số người thực ăn.
- **Vì sao sai:** khách đặt 200, ký nhận 200, chỉ 180 người ăn thì Delicare chỉ thu 180 suất. Suất ăn công nghiệp B2B thường thu theo **số khách đã chốt và ký nhận**, chỉ trừ suất lỗi do Delicare (thiếu, hỏng, không đạt nhiệt độ).
- **Đề xuất:** mặc định **tiền = suất khách ký nhận (POD) − suất bị trả lại do lỗi Delicare**. Hợp đồng nào thỏa thuận tính theo suất ăn thực tế thì chọn kiểu tính **theo từng hợp đồng**. Hiện không có cột lưu kiểu tính này nên cần anh quyết lưu ở đâu.
- **Anh cần chốt:** hợp đồng của Delicare tính tiền theo số chốt, số ký nhận, hay số suất ăn thực tế?

### 🔴 A4. Ba lớp dự phòng chồng lên nhau
- Tính nhu cầu cộng **tồn an toàn**, rồi đề nghị mua cộng **dự phòng 5 %**, rồi buổi nhiều lựa chọn (đơn cũ) cộng thêm **15 % mỗi lựa chọn**.
- Kết quả: có ngày mua dư 20–25 %, vượt mục tiêu chi phí nguyên liệu ≤ 55 %.
- **Đề xuất:** chỉ giữ **một** lớp, là tồn an toàn theo từng nguyên liệu. Dự phòng 5 % ở đề nghị mua mặc định = 0, người mua tự nhập khi cần. Dự phòng 15 % chỉ áp khi chưa có số theo lựa chọn; khi khách đã đăng ký theo lựa chọn thì bằng 0, đã làm ở D-74.

### 🔴 A5. Yêu cầu duyệt quá 48 giờ thì bị khóa, phải gửi lại
- Quá hạn thì **không duyệt được nữa**: người gửi phải hủy rồi gửi lại, và PR, PO, hợp đồng bị kẹt.
- **Đề xuất:** quá hạn thì **vẫn duyệt được**, chỉ gắn nhãn Quá hạn và **leo thang** lên cấp trên. Bot báo COO như hiện nay.

### 🔴 A6. Không có luồng giao bù
- Quy tắc (BUSINESS_RULES §7) yêu cầu: khách báo thiếu suất → bếp nấu bù → giao bù. Hệ thống chỉ tự tạo phản ánh; nấu và giao bù làm ngoài hệ thống, nên không có trong số sản xuất, giao hàng, đối soát hay giá vốn.
- **Đề xuất:** trên phản ánh *thiếu suất*, thêm nút **Tạo đơn bù**. Nút tạo đơn cùng ngày, gắn với phản ánh, tự duyệt kèm lý do, giá = 0 nếu lỗi do Delicare. Đơn bù đi qua lệnh nấu, mẻ, giao như đơn thường và **ưu tiên** trên bảng điều độ.

### 🔴 A7. Hết luồng ở bước đối soát, kế toán không có đầu ra
- Sau đối soát không có **bảng kê tháng theo khách** (suất × đơn giá, VAT, phí thất thoát khay) để xuất hóa đơn và theo dõi công nợ.
- Phí thất thoát khay chỉ có cảnh báo "báo khách / kế toán", không có số tiền đi vào bảng kê.
- **Đề xuất:** màn **Bảng kê thanh toán tháng** (chỉ xem và xuất Excel), theo khách / hợp đồng / kỳ:
  - suất theo buổi × đơn giá áp dụng, VAT bán hàng theo cấu hình;
  - phí thất thoát khay vượt ngưỡng;
  - giảm trừ suất lỗi.
  Màn này không lưu thêm dữ liệu, chỉ tính khi xem. Hóa đơn và công nợ làm ở phần mềm kế toán.

### 🟠 A8. Giá trên đơn có cố định lúc chốt không *(cần kiểm code)*
- Nếu chính sách giá mới được duyệt sau khi đơn đã chốt, đơn và đối soát phải giữ **giá tại thời điểm chốt**.
- **Đề xuất:** giá tính lúc chốt và lưu trên dòng đơn. Đối soát dùng giá đã lưu, không tra lại.

### 🟠 A9. Kế hoạch sản xuất và tính nhu cầu lấy hai nguồn đơn khác nhau
- Kế hoạch sản xuất sinh từ đơn **Đã xác nhận + Đã chốt**; tính nhu cầu chỉ lấy **Đã chốt**. Cùng một ngày, hai màn ra hai con số khác nhau.
- **Đề xuất:** thống nhất. Kế hoạch chính thức chỉ lấy đơn Đã chốt; số dự kiến (A1) lấy đăng ký và dự báo, ghi rõ nguồn từng ngày.

### 🟠 A10. Dữ liệu cấu hình nằm ở chỗ người dùng không thấy
- Ngưỡng thất thoát khay riêng của khách nằm trong `Customer.ExternalData` (JSON), không có ô trên giao diện.
- **Đề xuất:** hiện thành ô trong form khách hàng (nhóm *Điều khoản*), lưu vào chỗ cũ. Kiểu tính tiền ở A3 dùng cách tương tự nếu anh chọn lưu trên hợp đồng.

---

## B. Thừa bước / dễ hiểu lầm

### 🟠 B1. Đơn hàng có 4 bước bấm tay cho cùng một người
- Nháp → **Gửi** → **Xác nhận** → **Chốt**. Kinh doanh nhập số khách gửi, rồi tự gửi và tự xác nhận. Bước "Gửi" không có ai ở đầu kia.
- **Đề xuất:** đơn do nhân viên nhập: **Nháp → Đã xác nhận → Đã chốt** (20:00 tự động). Trạng thái "Đã gửi" chỉ dùng khi **khách tự đăng ký qua bot**, kinh doanh xác nhận lại.

### 🟠 B2. Sửa số trước giờ chốt lệch hơn 30 % phải chờ CEO duyệt
- Quy tắc gốc là so với **trung bình 4 tuần cùng thứ** để bắt số bất thường. Code đang so với **số đã xác nhận**, nên khách đổi số hợp lệ trước giờ chốt (180 → 250) cũng phải chờ CEO.
- **Đề xuất:** trước giờ chốt khách được đổi số, hệ thống **chỉ cảnh báo** khi lệch hơn 30 % so với trung bình 4 tuần. Sau giờ chốt mới phải duyệt, đã có luồng. Người duyệt là trưởng KD hoặc COO, không phải CEO.

### 🟠 B3. Hai lần duyệt cho cùng một lần mua
- Đề nghị mua duyệt 2 cấp, rồi PO duyệt thêm một lần (PO ≥ 50 triệu đồng là lần thứ ba).
- **Đề xuất:** PO lập từ đề nghị đã duyệt, đúng NCC ưu tiên, đúng giá bảng giá, không vượt số lượng đề nghị → **tự duyệt**. Chỉ phải duyệt khi đổi NCC, đổi giá, tăng số lượng, hoặc PO ≥ 50 triệu (Giám đốc).

### 🟠 B4. Công thức: duyệt xong còn bước "Khóa & áp dụng" riêng
- Cùng người, hai thao tác. **Đề xuất:** chọn *Áp dụng từ ngày* khi gửi duyệt; duyệt = khóa và áp dụng.

### 🟠 B5. COO duyệt kế hoạch sản xuất mỗi ngày, rồi còn bấm "Ra lệnh SX"
- Kế hoạch là phép cộng máy tính từ đơn đã chốt. Duyệt tay hằng ngày không thêm kiểm soát.
- **Đề xuất:** 20:00 chốt đơn → hệ thống tự lập kế hoạch → **tự phát hành lệnh và mẻ**. COO chỉ duyệt khi có người **sửa tay** kế hoạch, hoặc khi thiếu nguyên liệu hay vượt năng lực dây chuyền.

### 🟠 B6. Trên dòng đơn, "Ca" và "Loại suất" trùng nghĩa
- Mỗi buổi chỉ đi với một ca (Sáng ↔ SANG_6H…). Người dùng vẫn thấy và chọn ca, có thể chọn sai.
- **Đề xuất:** ẩn ô Ca, tự gán theo loại suất. Chỉ hiện khi một buổi có nhiều ca giao.

### 🟠 B7. Nhỏ, sửa nhanh
- Tạo đơn mặc định **ngày ăn = hôm nay + 4** → đổi thành **ngày mai**.
- Mục menu *Phê duyệt › Kiểm tra hàng nhập* trùng với nút kết luận trên màn QC → kiểm có loại duyệt nào dùng không, không có thì ẩn *(cần kiểm code)*.
- Tài liệu mâu thuẫn:
  - `02_KINH_DOANH` §4.6 ghi "sửa số đơn đã chốt báo IT (chờ bổ sung nút)" nhưng nút đã có.
  - `12_QUAN_LY_DUYET` ghi lệch 30 % "so dự báo / lịch sử", nhưng code so số đã xác nhận.

---

## C. Nên để hệ thống tự làm

| # | Việc hiện làm tay | Đề xuất tự động | Người còn làm |
|---|---|---|---|
| C1 | Tính nhu cầu, lập đề nghị mua (20:30, 21:00) | Tính theo tuần từ đăng ký + dự báo (A1); 20:00 tự tính bổ sung | Duyệt đề nghị |
| C2 | Lập PO từ đề nghị, gửi duyệt PO | Tự lập PO theo NCC ưu tiên, tự duyệt nếu đúng đề nghị (B3) | Gửi NCC, PO ≥ 50 triệu |
| C3 | Lập, duyệt kế hoạch SX, ra lệnh | Tự lập và phát hành sau chốt 20:00 (B5) | Chỉ khi sửa tay |
| C4 | Kho tạo phiếu xuất theo từng lệnh | Phát hành lệnh → tự tạo **phiếu xuất nháp** theo ca, gợi ý lô sẵn | Kho bấm **Ghi sổ** một lần mỗi ca |
| C5 | QC kết luận xong, kho bấm Ghi sổ phiếu nhập | QC kết luận → **tự ghi sổ phần đạt** | Không |
| C6 | QA bấm Đạt từng mẻ | Mẻ đủ điều kiện (lõi ≥ 70 °C, đã lưu mẫu, không lỗi trọng yếu) → **tự Đạt**, QA chỉ xử lý mẻ Tạm giữ / Loại. *Anh quyết – có thể QA muốn bấm tay.* | QA ngoại lệ |
| C7 | Lập lệnh đóng gói từ mẻ | Mẻ Đạt → **tự sinh lệnh đóng gói** chia theo đơn | Tổ đóng gói ghi kết quả |
| C8 | Mở đối soát từng đơn | Bàn giao xong → tự tạo đối soát với số nhận; khớp, không chênh → **tự chốt sau 24 giờ** | Kế toán xử lý chênh lệch |
| C9 | Bấm "Tính giá vốn" từng đơn | Tự tính khi đối soát chốt; giá thành lệnh tự tính khi đóng lệnh (đã có) | Không |
| C10 | Bấm "Tính lại giá thành chuẩn" | Đổi giá NCC (bảng giá) → tự tính lại món bị ảnh hưởng + cảnh báo FC % vượt mục tiêu ở thực đơn tuần sau | Không |
| C11 | Thực đơn chuyển "Hết hiệu lực" tay | Tự hết hiệu lực sau ngày cuối tuần; **nhắc thứ 4** nếu chưa công bố thực đơn tuần sau | Bếp lập thực đơn |
| C12 | Nhắc khách gửi đăng ký tuần | Danh sách khách chưa gửi đăng ký tuần sau + nhắc trên bot (sau này bot Zalo tự nhắc khách) | KD gọi |
| C13 | KPI chỉ có khi quản trị bấm làm mới | **Bật lịch tự tính** KPI ngày / tháng (đang tắt) | Không |
| C14 | Đơn bù khi thiếu suất | Nút Tạo đơn bù từ phản ánh (A6) | Bếp, giao vận làm như đơn thường |

---

## D. Đã ổn, giữ nguyên
- Sổ kho chỉ ghi thêm, FEFO, chặn lô hết hạn hoặc không đạt, truy xuất 2 chiều.
- Nhiệt độ lõi ≥ 70 °C, nhiệt độ giao ≥ 60 °C, lưu mẫu trước khi xe chạy, lỗi ATTP trọng yếu giữ mẻ.
- Nhận hàng 3 vùng nhiệt, QC đạt một phần chỉ nhập phần đạt, sự cố NCC tự lập.
- Kiểm kê lệch > 2 % duyệt 2 cấp, điều chỉnh tay có duyệt.
- Phân ca, chấm công GPS/QR, đơn nghỉ tự bỏ ca, đổi ca kiểm trùng.
- Đăng ký suất theo lựa chọn (D-74), dự báo tự gợi ý (D-76), dọn menu (MENU-4).

---

## E. Câu hỏi anh cần chốt
1. **A3:** tính tiền theo **số khách ký nhận** (em đề xuất) hay **số suất ăn thực tế**? Có hợp đồng nào tính khác không?
2. **A1:** NCC của Delicare thường giao trước mấy ngày? Mua theo tuần một lần, hay theo ngày nhưng đặt trước 2–3 ngày?
3. **C6:** mẻ đủ điều kiện có cho **tự Đạt** không, hay QA vẫn muốn bấm từng mẻ?
4. **B5:** bỏ duyệt kế hoạch sản xuất hằng ngày (chỉ duyệt khi sửa tay) – đồng ý không?
5. **A7:** có cần màn **Bảng kê thanh toán tháng** không, hay kế toán đã có cách khác?
