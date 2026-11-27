# 16 – Kho tri thức

Viết theo code AI-5 / AI-6 (D-133, 27/11/2026). Kho chứa **tri thức**: cách làm (SOP), quy định, hướng dẫn, bài học, quyết định, bản chụp báo cáo có ngày. **Số liệu hiện tại luôn xem trên màn nghiệp vụ** – ghi chú tự sinh chỉ là bản chụp lúc xuất. Kho là thư mục markdown chuẩn Obsidian trên máy chủ; web, agent (KNOW) và Obsidian dùng chung.

## 1. Bạn là ai

- **Mọi nhóm nội bộ**: xem **Tri thức › Kho tri thức**, thư mục **80 Đào tạo** và **99 Mẫu**.
- Thư mục theo bộ phận (Xem): 20 Kinh doanh (KD, CSKH, Kế toán, Giao vận), 30 Thực đơn & Công thức (Kế hoạch, Mua hàng, Bếp, TP KD, QA), 40 Kho (Mua hàng, Kho, Kế hoạch, QA, Kế toán), 45 ATTP (QA, Bếp, Kho, Giao vận), 50 Sản xuất (Bếp, Kế hoạch, QA), 55 Giao vận (Giao vận, CSKH, TP KD, QA), 60 Kế toán, 70 Nhân sự, 95 / 96–98 (IT).
- **Chủ thư mục (Sửa – soạn ghi chú)**: 10 CEO / COO · 20 TP KD · 30, 50 Bếp · 40 Kho + TP Mua hàng · 45 QA · 55 Giao vận · 60 Kế toán · 70 Nhân sự · 80 COO · 95, 99 IT · BOD CEO.
- **Ban GĐ** (CEO, CEO-PMO, COO) xem mọi thư mục, kể cả **BOD Bảo mật**. Quản trị hệ thống **không** tự thấy BOD.

## 2. Màn hình

| Phần | Dùng để |
|---|---|
| Ô tìm (trái trên) | Tìm toàn văn, **gõ không dấu cũng được** ("thit heo" ra "thịt heo"); kết quả có đoạn trích, ngày hiệu lực, cờ "cần rà soát" |
| Cây thư mục (trái) | Bấm thư mục để mở danh sách ghi chú; số bên cạnh = số ghi chú bạn được xem |
| Ghi chú (phải) | Đường dẫn, tiêu đề, nhãn (loại, trạng thái, hiệu lực từ, hạn rà soát, độ mật, "Tự sinh – chỉ đọc"), nội dung, **Liên kết ngược**, nút **Lịch sử**; **Sửa** khi bạn là chủ thư mục |

Trong nội dung: `[[Tên ghi chú]]` bấm được để mở ghi chú đó; **mã chứng từ** (PO-…, LO-…, ME-…, PA-…) bấm được để mở màn chứng từ; ngưỡng viết dạng `{{BusinessRules:…}}` hiển thị bằng **giá trị cấu hình hiện tại** (SOP không bao giờ cũ so với cấu hình).

## 3. Soạn ghi chú (chủ thư mục)

1. Mở thư mục → **Ghi chú mới** (hoặc mở ghi chú → **Sửa**).
2. Giữ khối **frontmatter** đầu ghi chú (giữa hai dòng `---`) và điền đủ: `id`, `type` (sop / su-co / bai-hoc / bao-cao / quyet-dinh / ho-so / nhat-ky / huong-dan), `source: human`, `owner`, `status` (nháp / chờ duyệt / hiệu lực / lưu trữ), `valid_from`, `review_by`, `confidentiality` (công khai / nội bộ / hạn chế / BOD), `refs` (mã chứng từ / danh mục liên quan). Thiếu trường → không lưu được.
3. **Lưu**. Máy chủ từ chối nếu nội dung có **số điện thoại, CCCD, số tài khoản, mật khẩu**.
4. Hai người cùng sửa: người lưu sau nhận "Ghi chú đã được người khác sửa sau khi anh/chị mở" → tải lại, sửa tiếp trên bản mới (không ai mất công).
5. **SOP / quyết định**: lưu ở *nháp* hoặc *chờ duyệt*; có hiệu lực sau khi duyệt (mục Duyệt SOP – đợt 3, AI-12). Mỗi quy trình chỉ một SOP hiệu lực; bản cũ chuyển *lưu trữ*, không xóa.
6. Mỗi lần lưu có dòng **Lịch sử** (ai, lúc nào, mã băm cũ / mới).

## 4. Ghi chú tự sinh (chỉ đọc)

Máy tự xuất (job **Xuất kho tri thức** 23:30, **Bản tin sáng / Báo cáo cuối ngày vào kho** 07:00 / 17:00): bản tin (10), thực đơn tuần đã duyệt + định mức đã khóa (30), phản ánh đã xử lý (20), sự cố chất lượng NCC đã đóng (45). Không sửa được – muốn bổ sung thì viết ghi chú riêng và liên kết `[[…]]` tới ghi chú tự sinh. Chạy lại không tạo bản trùng.

## 5. Agent dùng kho thế nào

- Agent chỉ đọc thư mục trong phạm vi của nó (vd WH: 40, 45, 80) **và** bạn cũng phải được xem thư mục đó; **không bao giờ** đọc BOD.
- Agent chỉ ghi vào **96 Nhật ký / 97 Tương tác / 98 Công việc** theo mẫu cố định; không sửa tay được các thư mục này.
- Câu trả lời dùng kho luôn nêu **tiêu đề + ngày hiệu lực**; ghi chú quá hạn rà soát được gắn "cần rà soát".

## 6. Lỗi thường gặp

| Thông báo | Cách xử lý |
|---|---|
| "Thiếu trường frontmatter: …" | Bổ sung trường còn thiếu trong khối `---` |
| "SOP / quyết định chỉ có hiệu lực sau khi duyệt" | Lưu trạng thái *nháp* / *chờ duyệt* |
| "Không lưu: Có số điện thoại …" | Bỏ dữ liệu cá nhân khỏi ghi chú |
| "Ghi chú tự sinh … chỉ đọc" | Viết ghi chú riêng, liên kết tới ghi chú tự sinh |
| Không thấy thư mục | Chưa được cấp mục quyền thư mục – xin chủ thư mục / IT |
