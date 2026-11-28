# Câu hỏi nhanh – chạy không qua AI (D-143, anh chốt 2026-11-28)

Mỗi agent có `app-delicare-agents/<MÃ>/quick.yaml`. Câu hỏi nhanh **biết trước cách lấy câu trả lời** (công cụ + tham số + mẫu trình bày); số liệu vẫn lấy mới từ lõi mỗi lần. Máy chủ chạy thẳng công cụ trong `AgentToolRegistry`, **không gọi Hermes / mô hình AI → 0 token**, trả ngay. Chỉ khi người dùng bấm **"Phân tích thêm"** mới gửi kết quả cho AI.

## Trường

| Trường | Bắt buộc | Ý nghĩa |
|---|---|---|
| `id` | ✓ | Mã duy nhất toàn hệ thống (`<agent>-<việc>`); dùng trong nhật ký, bản tin, nút bot |
| `hoi` | ✓ | Câu hiển thị trên nút (tiếng Việt, ngắn) |
| `tool` | ✓ | Tên công cụ – phải có trong `<MÃ>/tools.txt` (test chặn) |
| `args` |  | Tham số cố định. Giá trị ngày nhận `hom_nay` / `ngay_mai` / `hom_qua` / `{ngay+N}` (máy chủ thay theo giờ VN) |
| `nhap` |  | Ô người dùng nhập trước khi chạy: `{ tên_tham_số: "nhãn ô" }` (vd số lô). Có `nhap` thì nút mở ô nhập nhỏ, Enter chạy |
| `loc` |  | Lọc trên kết quả công cụ (mô tả tiếng Việt – Claude Code chuyển thành điều kiện trên trường thật; **không** gọi lại công cụ với tham số khác) |
| `hien_thi.kieu` | ✓ | `bang` (bảng) · `the` (thẻ chỉ số + bảng ngắn) · `danh_sach` (dòng thời gian / danh sách) · `bieu_do` (Chart.js, `UI.chart`) · `tom_tat` (một dòng + bảng gập) |
| `hien_thi.tieu_de` | ✓ | Tiêu đề kết quả |
| `hien_thi.tom_tat` |  | Câu tóm tắt có chỗ trống: `{so_dong}` số dòng sau lọc · `{dem.<trường>=<giá trị>}` đếm dòng thỏa · `{tong.<trường>}` / `{tb.<trường>}` / `{min.<trường>}` / `{pct.<trường>}` tổng / trung bình / nhỏ nhất / phần trăm · `{args.<tham số>}` · `{ngay}` hôm nay. Tên `<trường>` trong file là **tên gợi ý** – Claude Code ánh xạ sang trường thật của kết quả công cụ (bảng ánh xạ trong code, có test) |
| `hien_thi.cot` |  | Nhãn cột hiển thị (tiếng Việt, theo §4.5) – ánh xạ sang trường thật; trường không có thì bỏ cột và ghi PROGRESS |
| `hien_thi.sap_xep` |  | Thứ tự dòng |
| `hien_thi.rong` |  | Câu khi không có dòng nào (là "tin tốt", hiện màu xanh) |
| `mo_man` |  | Nút "Mở màn" – phải là screen-id có trong `catalog.js` / `ObjectMenu` (test chặn); người không có quyền màn thì ẩn nút |
| `phan_tich` |  | Gợi ý cho AI khi người dùng bấm **"Phân tích thêm"** (gửi kèm kết quả đã lọc quyền); trống = không có nút |
| `kenh` | ✓ | `web` (tab Chat với agent + "Hỏi nhanh" ở Tổng quan) · `VH` / `DH` (nút trong bot Vận hành / Điều hành – không AI) · `ZALO` (khách, CSKH) |

## Luật

1. Quyền: câu hiện cho người có quyền **Xem** màn của công cụ (`ScreenId`); chạy bằng **quyền của chính người hỏi** qua đúng Service của màn web (không dùng quyền agent). Công cụ "theo người hỏi" (`ds_cho_duyet`, `canh_bao`) như cũ.
2. Mỗi lần chạy ghi 1 dòng `IntegrationLog` loại `agent_quick` (id câu, người hỏi, số dòng, thời gian, **token = 0**); tab Sử dụng & chi phí đếm riêng "Hỏi nhanh" và "Chat AI".
3. Kết quả che dữ liệu cá nhân bằng `AgentMask` như công cụ agent; cắt `Agent:MaxRows`.
4. Không có câu nhanh nào là lệnh ghi (`propose_*` không được dùng trong `quick.yaml` – test chặn).
5. "Phân tích thêm" = 1 lượt chat AI có kèm kết quả + `phan_tich`; tính token như chat thường.
6. Sửa / thêm câu nhanh = sửa file này (review PR), không sửa code; `AgentConfigAi7Tests` (hoặc test mới) kiểm: `tool` hợp lệ, `id` không trùng, `mo_man` tồn tại, không `propose_*`, `kenh` hợp lệ.
