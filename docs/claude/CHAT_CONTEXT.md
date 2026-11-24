# Bối cảnh cho cuộc hội thoại mới với Claude (chat tài liệu) · 2026-11-19

> File này để mở một cuộc hội thoại Claude mới mà vẫn nối tiếp được nội dung đã làm. Claude ở cuộc hội thoại mới **đọc file này trước tiên**, rồi đọc các tài liệu ở mục 4.

## 1. Vai trò và cách làm việc

- Anh (backend) là chủ dự án; Claude xưng **"em"**, trả lời **tiếng Việt**.
- Dự án **DELICARE** – hệ thống suất ăn công nghiệp 20.000 suất / ngày.
- Repo trên máy anh: `D:\Duan\Delicare\Delicare-ai` (GitHub `ngocpv90/Delicare-ai`, nhánh chính `main`).
- **Claude Code trong VS Code** là người viết code. **Claude ở chat** viết / cập nhật tài liệu, kế hoạch, đề xuất và lệnh giao việc cho Claude Code.
- Đề xuất phải là **phương án tốt nhất**, quyết đoán, có lý do; không đưa phương án cho có rồi hỏi ngược lại anh "làm thế nào". Khi cần anh xác nhận: kèm phương án đề xuất tốt nhất + lý do + phương án khác (D-49). Có thể đặt vai người dùng (persona) để đánh giá.
- **Không ghi vào repo khi anh chưa đồng ý** (trừ khi anh yêu cầu trực tiếp việc ghi đó).

## 2. Quy tắc bắt buộc

- Khung code cũ (D-30): `api/<Controller>/<Action>`, Controller → Service → UnitOfWork / Dapper SP, `Result<T>`, `[FromForm]`; không `/api/v1`.
- Phân quyền cũ (D-31): User → UserClaim → UserGroup → UserRole × ObjectMenu.
- DB: không tự tạo schema `dm`, `vh`, `ht`, `bc`; không tự tạo Foreign Key vật lý; không tự thêm bảng / cột – thiếu thì ghi **GAP PROPOSED**. Chỉ cú pháp SQL Server 2022 (D-61). Không có API tra cứu cấu trúc DB (D-58).
- Không commit bí mật (token bot, khóa webhook, mật khẩu – để biến môi trường / user-secrets / `appsettings.Development.json` không commit).
- **Frontend: Claude Code làm toàn bộ (D-65, thay D-49)** ở engine `app-delicare-client/src/assets/delicare/js`; chat em viết yêu cầu FE thành task ROADMAP (không còn bàn giao cho dev khác). Triển khai Linux + Docker (D-60).
- Số quyết định tiếp theo: **D-95** (D-94 = làm lại toàn bộ biểu đồ bằng Chart.js + chuẩn chung – ROADMAP CHART-1, 2026-11-24; D-93 = mục Phê duyệt trong nhóm Điều hành, dưới Bảng điều hành – ROADMAP FIX-1.4b, 2026-11-24; D-92 = DB dev chạy script ngay không chờ backup + chuẩn Hoàn thành 7 điểm + kiểm điểm – ROADMAP FIX-NOW-1, 2026-11-24; D-91 = đổi món tại chỗ + tự duyệt + bỏ mục menu Yêu cầu đổi món; Zalo để T41 – ROADMAP UI-6.13; D-90 = không áp dụng thực đơn riêng cho khách – ROADMAP UI-6.12; D-89 = thực đơn tuần tự lập nháp chu kỳ 4 tuần + gợi ý món – ROADMAP UI-6.11; D-88 = hàng chỉ số tự co giãn theo số lượng + giao diện 100 % tiếng Việt – ROADMAP UI-6.10; D-87 = bảng kê thanh toán mọi khách một bảng + xuất hàng loạt, gửi khách để đợt sau – ROADMAP UI-6.9; D-86 = biểu đồ Chart.js toàn hệ thống + đối soát xác nhận hàng loạt – ROADMAP UI-6.7, UI-6.8; D-85 = đăng ký suất tuần theo mẫu chuẩn – ROADMAP UI-6.6, mô phỏng `order-week-v2-mockup.html`; D-84 = dự báo theo tuần + tự tạo / tự công bố + **mẫu thiết kế chuẩn màn ma trận** (g) – ROADMAP UI-6.5, mô phỏng `docs/frontend-handoff/assets/forecast-week-mockup.html`; D-83 = chính sách giá → phụ lục điều chỉnh giá trong hợp đồng – ROADMAP UI-6.4, 2026-11-23; D-82 = tài khoản ↔ con người ↔ nhân viên: `Employee.PersonId` + `User.AccountType` + vòng đời tài khoản – ROADMAP HR-ACC-1, migration 2 cột anh đã duyệt, GAP-PORTAL-01 cổng khách R2, 2026-11-23; D-81 = Phê duyệt một màn tổng + khuôn tóm tắt chung – ROADMAP APR-UI-1, mockup canvas `Xm5iKMTRTnwAb5zWwY9Xkt` / `docs/frontend-handoff/assets/apr-ui-1/`, 2026-11-23; D-80 = trang chi tiết toàn màn + phần đầu gọn – ROADMAP UI-6, 2026-11-23; D-79 = phân quyền theo từng màn + sắp lại menu theo vai trò – ROADMAP MENU-5, 2026-11-23; D-78 = các đề xuất sau BIZ-3 – Claude Code ghi; D-77 = sửa logic nghiệp vụ 3 đợt – ROADMAP BIZ-1/2/3, báo cáo `REVIEW_BUSINESS_LOGIC_2026-11-23.md`; D-76 = dự báo suất ăn hệ thống tự gợi ý – ROADMAP FC-1, 2026-11-23; D-75 = đổi tên toàn bộ menu – ROADMAP MENU-2; D-74 = đăng ký suất theo lựa chọn + thực đơn chung – ROADMAP ORD-OPT-1, 2026-11-22; D-70…D-73 đã dùng – xem DECISIONS; D-69 = bố cục chuẩn màn chi tiết 1/3 – 2/3 + khối thông tin rút gọn – ROADMAP UI-2R; D-68 = màn chi tiết nhân viên – ROADMAP UI-4, GAP-HR-06 / 07; D-67 = màn chi tiết nhà cung cấp – ROADMAP UI-3; D-66 = màn chi tiết khách hàng 360 – ROADMAP UI-2; D-65 = Claude Code làm toàn bộ FE, thay D-49 – ROADMAP FE-E; D-63 = quy tắc mã toàn hệ thống + khách hàng theo loại – ROADMAP MA-1; D-64 = Claude Code làm FE khi anh giao + chuẩn thẻ chỉ số – ROADMAP UI-1; 2026-11-19).

## 3. Cách đọc / ghi file trên máy anh

- Đọc / sửa qua cầu nối thiết bị (desktop app Claude, thư mục `D:\Duan\Delicare\Delicare-ai` đã kết nối).
- Khi ghi: dùng tên file đầu ra mới mỗi lần, **giữ CRLF**, commit với `expectedMtimeMs`, kiểm tra lại kích thước file sau khi ghi.
- Trước khi sửa **luôn đọc lại bản mới nhất** `ROADMAP.md`, `DECISIONS.md`, `PROGRESS.md` (Claude Code cũng ghi các file này).
- Phiên chat không gõ được lệnh trong terminal / VS Code của máy anh (chỉ bấm chuột). Việc cần chạy lệnh (git pull, dotnet run, npm start) → anh hoặc Claude Code làm.

## 4. Tài liệu cần đọc

| File | Nội dung |
|---|---|
| `CLAUDE.md` | quy tắc dự án, bảng tài liệu |
| `docs/claude/REVIEW_BUSINESS_FLOWS.md` | **rà soát 25 luồng nghiệp vụ đã xong (BE + FE), 2026-11-19** – điểm xuất phát tốt nhất |
| `docs/claude/ARCHITECTURE.md` | kiến trúc ứng dụng |
| `docs/claude/BOT_SOURCE_ARCHITECTURE.md`, `docs/claude/BOT_TELEGRAM.md` | kiến trúc + tính năng bot Telegram (D-62) |
| `docs/claude/ROADMAP.md`, `PROGRESS.md`, `DECISIONS.md` | kế hoạch, nhật ký, quyết định |
| `docs/claude/UAT_CHECKLIST.md` | kịch bản kiểm thử |
| `docs/claude/BUSINESS_RULES.md`, `ORG_STRUCTURE.md` | quy tắc nghiệp vụ, cơ cấu tổ chức / vai trò |
| `docs/reference/DELICARE_Business_Workflow.md`, `DELICARE_API_Specification.md`, `STATUS_CATALOG.md` | quy trình nghiệp vụ, đặc tả API, trạng thái |
| `docs/frontend-handoff/README.md` + các file P*.md, BE*.md | bàn giao FE theo màn hình |
| `app-delicare-client/src/assets/delicare/js/modules/*.js` | **giao diện thật**: mỗi `APP.register('<screen-id>')` là một màn; `core/app.js` menu, `data/catalog.js` danh mục màn |

## 5. Hiện trạng (2026-11-19)

- **Backend**: xong toàn luồng R1 (BE-2), BE-3 (dashboard, KPI, IoT, cảnh báo, tìm kiếm), job tự động (T23.1), bot Telegram (BOT-1.1 → 1.7d, 2.1 → 2.4). Test 437 / 437; `full-flow.http` 216 / 216 trên DB dev + staging.
- **Frontend**: giao diện theo mockup đã gộp `main` (PR #1 GHEP-FE); 54 màn, 52 màn gọi API thật. Thiếu: màn `adm-job`, tab Lịch sử (`Common/GetHistory`), thẻ / chip trạng thái (`Common/GetStatusSummary`).
- **Chưa làm**: UAT trên giao diện (định chạy trên máy local, DB dev – chờ anh `git pull main` + chạy API và `npm start`), BOT-2.9 (server test + token thật), Docker chạy thật, cập nhật Catalog API, gửi PO cho NCC qua email / Zalo.
- **Bot – đã chốt (D-62)**: container `messaging` / project `AppDelicare.Messaging` chứa mọi bot, gọi `api` qua HTTP; 3 bot: **Vận hành** (mọi nhân viên), **Điều hành** (quản lý), **Trợ lý** (Hermes, chạy riêng `agents/hermes`). UX "4 thứ": Thẻ ca / Thẻ ngày, Thẻ việc (3 mức 🔴 / 🟡 / ⚪), QR "quét để làm", menu 3 phím cố định. Duyệt theo mức rủi ro. Không làm Mini App, không làm màn web xem sự việc liên phòng. Không làm app mobile native ở R1 (dùng bot + Angular responsive / PWA).
- **Việc em đã hứa cập nhật tài liệu (chưa làm, khi làm phải đọc lại bản mới nhất)**: ROADMAP BOT-3.4 (bỏ Mini App), BOT-3.3 ON-HOLD, quy tắc "đợt BOT chỉ làm BE"; BOT_TELEGRAM §7/§8 bỏ Mini App; DECISIONS mục E: FE-BOT-4 (in QR hàng loạt), FE-BOT-5 (chế độ tablet), FE-BOT-6 (báo cáo hiệu quả bot, ON-HOLD); D-62 ghi "không Mini App, không màn web sự việc liên phòng"; BOT-1.3 / BOT_SOURCE §7: token qua user-secrets **hoặc** `appsettings.Development.json` (không commit) + file `.example` + `.gitignore`.
