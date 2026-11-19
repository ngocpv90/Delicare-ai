# PROGRESS – Nhật ký tiến độ (Claude Code cập nhật sau mỗi task)

## Trạng thái hiện tại
- **2026-11-19: UG-1.4 DONE** – C-03, C-04, C-10, C-14, C-21 trên nhánh `feat/MA-1-code-rules`. Test **477 / 477**, build FE xanh. Còn của UG-1: C-15 nút công đoạn trên bot; UG-1.5 chờ anh (C-17, C-18, C-19, C-22, C-23); `docs/reference/STATUS_CATALOG.md` cần anh cập nhật theo `StatusCatalogEffective` (em không sửa docs/reference).
- **2026-11-19: UG-1.3 (mục TB) DONE** – C-05, C-09, C-11, C-12, C-13, C-15 (web), C-16, C-20 trên nhánh `feat/MA-1-code-rules`. Test **475 / 475**, build FE xanh. Còn của UG-1: C-15 nút công đoạn trên bot; UG-1.4 (C-10, C-03, C-04, C-14, C-21, REVIEW_BUSINESS_FLOWS); UG-1.5 chờ anh (C-17, C-18, C-19, C-22, C-23).
- **2026-11-19: UG-1 mục Cao (C-01, C-06, C-07, C-08) DONE** trên nhánh `feat/MA-1-code-rules` (BE + FE). Test **475 / 475**, build FE xanh. Tiếp theo: UG-1.3 mục TB (C-09, C-11, C-12, C-15, C-16, C-20, C-05, C-13).
- **2026-11-19: FE-E.1 → FE-E.3 DONE** trên nhánh `feat/MA-1-code-rules`: liên kết Telegram ở menu tài khoản + nút cấp mã trên dòng hồ sơ nhân viên; QR do máy chủ vẽ (QRCoder – D-67): ảnh QR hộp mã liên kết, PDF phiếu in, In QR hiện trường; bỏ ẩn int-channel / int-bot / int-log + Gửi lại trên dòng tin lỗi. Test **475 / 475**, build FE xanh. Chờ anh: D-68 (siết quyền màn tích hợp), khóa `Messaging:LinkSigningKey` cho máy chạy thử. Tiếp theo: giao diện UG-1 (FE-E.4).
- **2026-11-19: UI-2 DONE (chờ anh mở thử) – màn khách hàng 360 cả trang** trên nhánh `feat/MA-1-code-rules`: `GetOverview` + cảnh báo, `Customer/GetTrend`, `Order/GetAll` lọc ngày ăn; màn `crm-customer-360` 10 tab. Test **475 / 475**, `full-flow.http` **221 / 221** trên DB dev (DEMOUI2A, 2026-12-13), build FE xanh. Tiếp theo: FE-E (FE-BOT-1 → FE-BOT-3 → FE-BOT-2).
- **2026-11-19: UI-1 DONE (chờ anh xem giao diện) + MA-1 phần còn lại** trên nhánh `feat/MA-1-code-rules`: thẻ chỉ số kiểu mới (D-64 c) cho mọi màn, 4 thẻ danh sách / hồ sơ khách hàng, `Customer/GetSummary`, trường mới `GetOverview`; MA-1.6 HDSD, ghi chú .http cũ, Catalog (openpyxl trong venv tạm). Test **474 / 474**, build FE xanh. **Chưa tự mở trình duyệt** (không có công cụ trình duyệt) – anh xem ở http://localhost:4300. Tiếp theo theo D-65 d / ROADMAP: UI-2 (màn khách hàng 360) → FE-E.
- **2026-11-19: MA-1 (D-63) DOING – phần backend xong** trên nhánh `feat/MA-1-code-rules` (từ `main` @ bd71007, chưa push / gộp): sinh mã danh mục + tách tiền tố chứng từ §11, khách hàng theo loại, danh mục G1 tự sinh mã, bảng phân nhóm G1/G2/G3 + `Common/GetCodeRules` + test chặn, bot nhận mã mới, script đánh lại mã KH / NCC (chưa chạy thật). Test **472 / 472**; `full-flow.http` **218 / 218** trên DB dev. Bổ sung cùng ngày: MA-1.3 import (ô mã trống → sinh mã) và MA-1.7 (2) kiểm mã G2 ở 15 service – xong, test 473 / 473. MA-1.8 FE xong (anh đồng ý làm FE + 4 điểm PROPOSED → DECISIONS D-63 m–q; build FE xanh). Còn: MA-1.6 tài liệu HDSD, các `.http` khác, Catalog; script MA-1.4 chờ anh chạy thật. Chi tiết: mục MA-1 cuối nhật ký.
- **2026-11-18: BOT-1.7d DONE – QR trên phiếu in, nhóm chat, cảnh báo nhiệt độ giao thấp** trên nhánh `feat/BOT-1.7d-qr-phieu-nhom` (từ `release/pilot` @ 114aed4, chưa push / gộp – chờ anh duyệt); test **437 / 437**. Phần tích hợp bot còn BOT-2.9 (cần server test + token bot thật).
- **2026-11-18: BOT-2.4 DONE – QR vật thể + vị trí trực tiếp tài xế** trên nhánh `feat/BOT-2.4-qr-vitri` (từ `release/pilot` @ 97cbd52, chưa push / gộp – chờ anh duyệt); test **431 / 431**. Tiếp theo: BOT-2.9 (tích hợp, triển khai server test) hoặc 1.7d.
- **2026-11-18: BOT-2.3 DONE – giờ yên lặng theo ca + tin theo lịch** (bản tin 7h, báo cáo 17h, khách chưa chốt 19h, nguyên liệu 21h, kiểm tra định kỳ: mẻ trễ, xe chưa rời, điểm trễ, chưa vào ca, phản ánh sắp / quá SLA, PO trễ, duyệt quá hạn) trên nhánh `feat/BOT-2.1-the-ca` (cùng BOT-2.1, 2.2 – chưa push / gộp, chờ anh duyệt); test **424 / 424**. Tiếp theo: BOT-2.4.
- **2026-11-18: BOT-2.2 DONE – sự việc liên phòng (nhận xử lý, ghi chú, leo thang) + nhắc người duyệt trước ngày nghỉ** trên nhánh `feat/BOT-2.1-the-ca` (cùng BOT-2.1, chưa push / gộp – chờ anh duyệt); test **420 / 420**. Tiếp theo: BOT-2.3.
- **2026-11-18: BOT-2.1 DONE – Thẻ ca / Thẻ ngày** trên nhánh `feat/BOT-2.1-the-ca` (từ `release/pilot` @ 9f0c307, chưa push / gộp – chờ anh duyệt); test **416 / 416**. Chi tiết: mục BOT-2.1 cuối nhật ký. Tiếp theo: BOT-2.2.
- **2026-11-18: BOT-1.7 DONE – thông báo nghiệp vụ** (sự kiện tức thời) trên nhánh `feat/BOT-1.7-thong-bao` (từ `release/pilot` @ 807f159, chưa push / gộp – chờ anh duyệt); test **410 / 410**. Chi tiết: mục BOT-1.7 cuối nhật ký. Tiếp theo: 1.7d (QR phiếu in, nhóm chat) hoặc BOT-2.1.
- **2026-11-18: BOT-1.6 DONE – bot Điều hành** trên nhánh `feat/BOT-1.6-dieuhanh` (tách từ `feat/BOT-1.5c-vanhanh-con-lai`, chưa push / gộp – chờ anh duyệt cùng BOT-1.5c); test **406 / 406**; `flow-dieuhanh.mjs` đạt trên DB dev. Tiếp theo: BOT-1.7 (thông báo nghiệp vụ).
- **2026-11-18: BOT-1.5c DONE – bot Vận hành phần còn lại (BOT-1.5 xong)** trên nhánh `feat/BOT-1.5c-vanhanh-con-lai` (từ `release/pilot` @ 41dd0ba, chưa push / gộp – chờ anh duyệt); test **403 / 403**; `flow-15c.mjs` đạt trên DB dev (22 lệnh). Chi tiết: mục BOT-1.5c cuối nhật ký. Tiếp theo: BOT-1.6 (bot Điều hành).
- **2026-11-18: BOT-1.5b DONE – bot Vận hành: Giao vận + Điểm phát** trên nhánh `feat/BOT-1.5b-giaovan-diemphat` (từ `release/pilot` @ bd022e5, chưa push / gộp – chờ anh duyệt); test **393 / 393**; `flow-giaovan.mjs` đạt trên DB dev. Chi tiết: mục BOT-1.5b cuối nhật ký.
- **2026-11-18: DB-MIGRATIONS DONE – cấu trúc bảng quản lý bằng migration** (anh yêu cầu) trên nhánh `feat/DB-MIGRATIONS` (từ `release/pilot` @ 10981dd, **chưa push / gộp – chờ anh duyệt**): `DB/migrations` là nguồn cấu trúc duy nhất cho mọi môi trường; DB dev + staging đã ghi nhận, không đổi bảng; test **387 / 387**. Chi tiết: mục DB-MIGRATIONS cuối nhật ký.
- **2026-11-17: T48.4 DONE – production Linux + Docker (D-60)** trên nhánh `feat/T48.4-docker` (từ `release/pilot` @ 9951db7): compose production (nginx + api + job, SQL riêng), tài khoản SQL ứng dụng, kiểm giờ SQL, file nhịp Job, log console trong Docker; test **292 / 292**. **Docker chưa chạy thật** (máy dev không có Docker). Anh duyệt 2026-11-17: đồng ý toàn bộ đề xuất BE-3 + đã push `main` = `release/pilot` = 9951db7; đã đổi khóa JWT dev / staging + mật khẩu admin staging.
- **2026-11-17: BE3.7 DONE – ĐỢT BE-3 XONG (BE3.1–BE3.5; BE3.6 không làm – D-58). Dừng, chờ anh duyệt** – tất cả đã gộp `release/pilot` (T23 → BE3.1 → BE3.2 → BE3.3 → BE3.4 → BE3.5), **chưa push / chưa gộp `main`**. Test **288 / 288**; mọi `.http` BE-2 / T23 / BE-3 xanh trên DB dev; staging cập nhật (DB + publish) – `full-flow.http` 217 / 217. Tiếp theo theo ROADMAP: **T48.4** (production Linux + Docker – D-60) khi anh cho làm.
- **2026-11-17: BE3.5 DONE – dashboard điều hành + hành trình đơn hàng** (Catalog 231, 232) trên nhánh `feat/BE3.5-dashboard` (worktree `wt-BE35`, từ `release/pilot` @ af24e5c, chưa push): `Dashboard/GetExecutive`, `Dashboard/GetOrderJourney`; test **279 / 279**, `be3.5-dashboard.http` **13 / 13** trên DB dev, đối chiếu số dashboard với Order / ProductionBatch / DeliveryExecution GetBoard + chuông khớp 100 %. PROPOSED D-57 ad–ai. Handoff `docs/frontend-handoff/BE3.5.md`.
- **2026-11-17: BE3.3 DONE – chuông cảnh báo, tìm kiếm toàn cục, tab Liên quan** (Catalog 16, 17, 18) trên nhánh `feat/BE3.3-alerts-search` (worktree `wt-BE33`, từ `release/pilot` @ 5aeaf30, chưa push): `Common/GetAlerts`, `Common/GlobalSearch`, `Common/GetRelated`; test **274 / 274**; `be3.3-alerts-search.http` **31 / 31** trên DB dev. PROPOSED D-57 p–u. Handoff `docs/frontend-handoff/BE3.3.md`.
- **2026-11-17: BE3.4 DONE – KPI** (Catalog 221–225) trên nhánh `feat/BE3.4-kpi` (worktree `wt-BE34`, từ `release/pilot` @ 5aeaf30, chưa push): định nghĩa + phiên bản + mục tiêu, `KpiSnapshot/Refresh` (chỉ admin, sp_getapplock + AnalyticsRefreshLog, upsert), `GetDaily` / `GetMonthly`, `AnalyticsRefreshLog/GetList`; seed 11 chỉ tiêu; job KPI có code nhưng tắt; test **275 / 275**, `be3.4-kpi.http` **33 / 33** trên DB dev. PROPOSED D-57 w–ac, GAP-KPI-01. Chi tiết: mục BE3.4 cuối file.
- **2026-11-17: BE3.2 DONE – IoT nhiệt độ** (Catalog 166) trên nhánh `feat/BE3.2-iot` (từ `release/pilot` @ 2aeb84a, chưa push): `POST api/IotTemperature/Receive` (khóa API riêng, không JWT), test **268 / 268**, `be3.2-iot.http` 3 / 3 trên DB dev (khóa đặt lúc chạy, không commit). PROPOSED D-57 f, o. Tiếp theo: BE3.3 cảnh báo, tìm kiếm, tab Liên quan.
- **2026-11-17: BE3.1 DONE – biểu đồ + sự kiện giao** (đợt BE-3, D-57) trên nhánh `feat/BE3.1-charts` (worktree `wt-BE31`, từ `release/pilot` @ 8e12696, chưa push): Catalog 100 / 157 / 163 / 192 / 193; test **264 / 264**; `be3.1-charts.http` **22 / 22** trên DB dev. PROPOSED D-57 (j)–(n). Tiếp theo: BE3.2 IoT nhiệt độ.
- **2026-11-17: T23.1 DONE – Job tự động** trên nhánh `feat/T23-jobs` (worktree `wt-T23`, từ `release/pilot` @ f648505, chưa push / chưa gộp): project mới `AppDelicare.Job` (D-48) + 5 job LockOrders 20:00 / Mrp 20:30 (+ đề nghị mua) / LotExpiry 00:15 / ContractExpiry 00:30 / MarkAbsent 08:00, `api/Job/GetAll` / `Run` (admin), `Contract/ExpireOverdue`; seed user `system`; test **258 / 258**; `t23-jobs.http` 6 / 6 trên DB dev. PROPOSED D-59. Anh chỉ đạo 2026-11-17: **chỉ làm backend** (không FE).
- **2026-11-17: Đã gộp + push** BE2.9 và BE2.7.1 vào `release/pilot` = `main` (`81bcf3a`); test 245 / 245. **Staging cập nhật bản có PDF** (`Publish-Iis.ps1 -Output C:\Deploy\delicare-staging -Environment Staging`): `full-flow.http` **217 / 217** trên staging (`@key=B272`, `@day=2026-12-15` – ngày 2026-12-01 đã dùng ở lần chạy BE2.9, kế hoạch SX đã Hoàn thành nên không chạy lại được), 6 mẫu in + `Common/PrintBatch` trả `application/pdf`; chạy xong đã dừng API staging. Dữ liệu thử còn lại trên staging: mã `DEMOB29S…`, `DEMOB271…` (lần chạy lỗi giữa chừng vì trùng ngày), `DEMOB272…`. **Còn 0 task backend BE-2**; tiếp theo: GHEP-FE (khi anh yêu cầu) → UAT toàn luồng → T48.2.
- **2026-11-17: BE2.9 DONE – ĐỢT BE-2 XONG (BE2.1–BE2.7 + BE2.9; BE2.8 để sau D-45). Dừng, chờ anh duyệt** – nhánh `feat/BE2.9-full-flow` (worktree `wt-BE29`, từ `release/pilot` @ 7b0fc43, chưa push / chưa gộp `main`): `full-flow.http` (toàn luồng R1, 216 request / 22 chốt / 60 kiểm số liệu) **216 / 216 trên DB dev và trên staging**; chạy lại `pilot-flow` + `be2.1…be2.7` đều đạt; test **243 / 243** (+ FullFlowSqlServerTests, MissingDateParameterTests); sửa 1 lỗi (6 bảng điều hành thiếu ngày → HTTP 500); staging `AppDelicare_Staging` / `http://localhost:5200` đã cập nhật + smoke 49 / 49 rồi **dừng**. Danh sách PROPOSED / GAP cần anh chốt (thư viện PDF, cột `DeliveryOrder.MealQuantity`, User ↔ Employee, cột liên kết DocumentFile, D-53…D-56): mục BE2.9 cuối file.
- **2026-11-17: BE2.7.1 DONE** trên nhánh `feat/BE2.7.1-pdf` (worktree `wt-PDF`, từ `release/pilot` @ 7b0fc43, chưa push / chưa gộp): mẫu in **PDF** (anh chốt 2026-11-17 – PDFsharp-MigraDoc 6.2.4 MIT + font Noto Sans OFL nhúng) cho 6 phiếu + in hàng loạt (1 PDF, mỗi phiếu trang mới); **PDF mặc định** (PROPOSED D-56 k, cấu hình `BusinessRules:Common:PrintDefaultFormat`). GAP-PRINT-01 / D-56 (b) đã xử lý. Test 236/236, `be2.7-common-print.http` 47/47 trên DB dev. Chi tiết: mục BE2.7.1 cuối file.
- **2026-11-17: BE2.7 DONE** trên nhánh `feat/BE2.7-common-print` (worktree `wt-BE27`, từ `release/pilot` @ 724488d, chưa push / chưa gộp): danh sách trắng + thống kê trạng thái + xuất Excel / CSV (`Common/*`), lịch sử gộp AuditChange + duyệt + phản ánh + tệp, tệp đính kèm ngoài wwwroot (`DocumentFile/Upload`, `GetList`, `Download`), mẫu in phiếu nhập / xuất / kiểm kê / giao / POD / đối soát + in hàng loạt – **xlsx / docx, chưa PDF (D-56 b, cần anh chọn thư viện)**. Test 234/234, `be2.7-common-print.http` 43/43. Chi tiết: mục BE2.7 cuối file.
- **2026-11-17: BE2.5 DONE** trên nhánh `feat/BE2.5-foodsafety-packaging` (worktree `wt-BE25`, từ `release/pilot` @ 7e87f7c, chưa push / chưa gộp): checklist ATTP theo ca / mẻ (hạn tương đối, lỗi trọng yếu giữ mẻ + chặn đạt QC / đóng gói / xuất chuyến), bàn giao ghi sổ bao bì OUT, thu hồi + kiểm tra khay, mẻ vệ sinh 6 bước, số dư + tỷ lệ thất thoát, sự cố NCC chuyển trạng thái. `dotnet test` **216/216**; `.http` trên DB dev: `be2.5-foodsafety-packaging` 142/142, `pilot-flow` 141/141, `be2.1` 147/147, `be2.2` 73/73, `be2.3` 58/58, `be2.4` 125/125. Seed mới `FoodSafetyQa_seed.sql` (đã chạy trên DB dev, có trong `scripts.order`). PROPOSED: DECISIONS **D-54 (a)–(h)**. Kế tiếp: BE2.6. Chi tiết: mục BE2.5 cuối file.
- **2026-11-17: BE2.4 DONE** trên nhánh `feat/BE2.4-inventory-production` (worktree `wt-BE24`, từ `release/pilot` @ eae2b71, chưa push / chưa gộp): giữ chỗ nguyên liệu khi phát hành (phân bổ FEFO lên tồn, xuất cho lệnh dùng phần đã giữ), MRP trừ hàng sắp về từ PO, hủy / đóng lệnh SX, chênh lệch tiêu hao, giá thành thực tế + giá vốn suất, công đoạn theo tổ (Sơ chế → Nấu → Chia suất), xử lý lô hết hạn bằng thao tác tay. `dotnet test` **210/210**; `.http` trên DB dev: `be2.4-inventory-production` 125/125, `pilot-flow` 136/136, `be2.1-flow-gaps` 147/147, `be2.2-contract-forecast` 73/73, `be2.3-multilevel-approval` 58/58. Seed mới `ProductionStage_seed.sql` (đã chạy trên DB dev, có trong `scripts.order`). PROPOSED: DECISIONS **D-53 (a)–(h)**. Kế tiếp: BE2.5. Chi tiết: mục BE2.4 cuối file.
- **2026-11-17: BE2.3 DONE** trên nhánh `feat/BE2.3-multilevel-approval` (worktree `wt-BE23`, từ `release/pilot` @ 69aad11, chưa push / chưa gộp): engine duyệt nhiều cấp theo `ApprovalStep` (InProgress, quy trình lưu ở ApprovalItem, hạn = cờ, mỗi cấp 1 người, hủy ở mọi cấp, cấu hình `ApprovalSteps`); đề nghị mua 2 cấp D-46 (`scm-pr-l1` → `scm-pr-l2`, lập PO chỉ khi Đã duyệt); kiểm kê lệch > 2% Submit + 2 cấp → Post; điều chỉnh tồn tay qua STOCK_ADJUSTMENT; PO 2 cấp theo ngưỡng (cấu hình, mặc định D-11). `dotnet test` **203/203**; `.http` trên DB dev: `be2.3-multilevel-approval` 58/58, `pilot-flow` 131/131, `be2.1-flow-gaps` 147/147, `be2.2-contract-forecast` 73/73. Seed sinh lại (ObjectMenu / UserRole – đã chạy trên DB dev). PROPOSED: DECISIONS **D-52 (a)–(j)**. Kế tiếp: BE2.4. Chi tiết: mục BE2.3 cuối file.
- **2026-11-17: BE2.2 DONE** trên nhánh `feat/BE2.2-contract-forecast` (worktree `wt-BE22`, từ `release/pilot` @ 75306d5, chưa push / chưa gộp): hợp đồng kèm suất + số HD- + gửi duyệt / duyệt (engine 1 cấp T12) + chấm dứt; dự báo nhập phiên bản / công bố / ma trận + gợi ý số suất + lệch dự báo khi xác nhận; yêu cầu đổi món (áp kế hoạch + MRP); sửa / thêm đơn sau giờ chốt qua duyệt ORDER_CHANGE_AFTER_CUTOFF. `dotnet test` **194/194**; `be2.2-contract-forecast.http` 73/73, `pilot-flow.http` 123/123, `be2.1-flow-gaps.http` 142/142 trên DB dev. PROPOSED: DECISIONS **D-51 (a)–(j)**. Không SP / seed mới. Kế tiếp: BE2.3. Chi tiết: mục BE2.2 cuối file.
- **2026-11-17: BE2.1 DONE** trên nhánh `feat/BE2.1-flow-gaps` (worktree `wt-BE21`, từ `release/pilot`, chưa push / chưa gộp): đóng gói từ mẻ, xếp hàng lên xe (bắt buộc trước khi xuất phát), đơn giao tay / tách / gán – chuyển chuyến / hủy, lập chuyến nhiều xe theo khu vực, thực hiện giao, kế hoạch sinh theo ca (1 KH- / ngày), đề nghị mua chọn dòng + dự phòng, nhiệt độ tự do, hao hụt, bảng đơn, danh sách thực đơn / phản ánh, chi tiết + hủy yêu cầu duyệt, truy xuất 2 chiều. `dotnet test` **185/185**; `be2.1-flow-gaps.http` 138/138 + `pilot-flow.http` 121/121 chạy thật trên DB dev. PROPOSED chờ anh: DECISIONS **D-50 (a)–(h)**. Không SP / seed mới. Kế tiếp: T48.3 (ROADMAP) → BE2.2. Chi tiết: mục BE2.1 cuối file.

- **2026-11-16: T48.3 DONE** (nhánh `feat/T48.3-db-autoupdate`, chưa push / gộp): thứ tự script DB duy nhất `deploy/db/scripts.order`; dev FE cập nhật DB Docker bằng `git pull && docker compose -f deploy/docker/docker-compose.staging.yml --env-file deploy/docker/staging.env run --rm db-init` (hoặc `deploy/docker/update-db.sh`). Docker thật chưa chạy (máy không có Docker) – xem mục T48.3 cuối file.
- **2026-11-16 (D-44): KHÔNG CHẠY PILOT – hoàn thiện toàn bộ luồng R1.** Mọi mục DEFERRED(PILOT) vào BE-2. Để sau khi xong luồng: bot, Hermes + quản lý agent, dashboard / KPI, báo cáo.
- **2026-11-16 (D-43): ĐỢT BE-2 – hoàn thiện toàn bộ API backend** (trừ bot, dashboard / KPI, báo cáo). Task kế tiếp: **BE2.1** (lấp chỗ thiếu luồng Pilot: đóng gói, xếp xe, đơn giao, lập chuyến nhiều xe…) → BE2.2 → … → BE2.9 (ROADMAP mục ★★★). FE của Claude Code tạm dừng; bạn frontend dùng `docs/frontend-handoff/`. Catalog đã cập nhật 2026-11-16: 124/234 xong, Pilot 114/140.
- **2026-11-17: BACKEND PILOT XONG (P0 → P9) – dừng, chờ anh UAT-PILOT** (ROADMAP P9 `DONE(PILOT)`, mục T50R1 + T48.1 cuối file). Luồng P3 → P8 chạy thật bằng API trên DB dev (`docs/api-tests/pilot-flow.http`, 117 request / 8 chốt an toàn đúng) + test end-to-end qua HTTP (`Pilot/PilotFlowSqlServerTests`). Staging (LocalDB `AppDelicare_Staging`, `http://localhost:5200`) đã cập nhật backend Pilot + bản build Angular.
- Nhánh: `feat/T50R1-pilot-flow` (từ `release/pilot` = T11 … T19 + T17.1 đã gộp, D-41). Chưa push / chưa gộp `main` – chờ anh duyệt.
- Build: `dotnet build --no-incremental` 0 lỗi, 10 cảnh báo; `dotnet test` **173/173** (baseline 169 + 4); `npm run build` 0 lỗi.
- **Tiếp theo (sau khi anh UAT-PILOT backend):** đợt FE theo D-40: FE-3 (phần còn lại) → FE của T45R.8 → FE-P2 → FE-P3 → FE-P4 → FE-P5 → FE-P7 → FE-P8 → UAT-PILOT trên giao diện (UAT_CHECKLIST mục PILOT-FE).
- Khung chuẩn = code cũ (D-30), không sinh `/api/v1`. Tiến độ API theo `docs/reference/DELICARE_API_Catalog.xlsx` **chưa cập nhật** cho T11 … T19 / T50R1 (máy không có python / openpyxl) – gợi ý trạng thái: mục T50R1 bên dưới + gợi ý cuối từng mục T13 … T19.
- Việc anh cần làm: (1) UAT-PILOT theo `docs/claude/UAT_CHECKLIST.md` mục PILOT trên staging; (2) duyệt / commit nhánh `feat/T50R1-pilot-flow`, rồi duyệt gộp `release/pilot` → `main`; (3) chốt các điểm PROPOSED của Pilot (mục T50R1 "Câu hỏi còn mở"); (4) IIS thật (deploy/README.md mục 4, cần quyền admin) nếu chọn IIS (D-15); (5) cập nhật / cho phép cài openpyxl để cập nhật Catalog; (6) các việc cũ: chạy T4 trong Visual Studio (T45R.6), D-26, D-27, đổi khóa đã lộ (SECRETS_TODO.md), nhánh `origin/develop`.
- Lịch sử trạng thái trước 2026-11-17 (R1.0 / R1.1, D-34, D-40) xem các mục nhật ký bên dưới.

---

## Nhật ký
<!-- Mẫu cho mỗi task:
### T05 – Bộ sinh mã mới · 2026-11-15 · DONE
Kế hoạch: 1) ... 2) ...
Đã làm: ...
File chính: ...
Kiểm tra: dotnet build OK (0 lỗi, N cảnh báo), dotnet test 12/12
AC: [x] ... [x] ... 
Còn lại / GAP: ...
Cần anh: ...
-->

### T00 – Khởi tạo môi trường làm việc · 2026-11-14 · DONE
Kế hoạch: 1) kiểm tra git 2) cài `.claude/` 3) tạo nhánh + commit tài liệu 4) build baseline 5) so DB dev với DDL 6) xuất SP 7) thử đăng nhập API 8) liệt kê vị trí bí mật.

**Git:** trước khi làm: nhánh `main` (1 commit `2501aef Initial commit`), remote `origin` = GitHub `ngocpv90/Delicare-ai`. Thay đổi chưa commit lúc đó: `.gitignore` (sửa), `CLAUDE.md`, `DB/`, `docs/` (mới) – anh xác nhận là bộ tài liệu cho T00 → đã commit vào `chore/T00-claude-setup`. `docs/claude/LOCAL_DEV.local.md` và `backup-db/` được `.gitignore` bỏ qua (đã kiểm tra bằng `git check-ignore`).

**Lệnh tắt:** `.claude/commands/` (next, task, status, verify, gap) + `.claude/settings.json`. Em **bỏ** 2 quyền `npm run build` / `npm ci` khỏi bản sao vì trái D-20 (bản gốc trong `docs/claude/claude-config/` giữ nguyên).

**Build baseline:**
- SDK: .NET 10.0.401 (có runtime ASP.NET Core 8.0.31 → chạy được app net8.0).
- `dotnet build AppDelicare/AppDelicare.sln`: **0 lỗi, 10 cảnh báo**: NU1903 × 3 loại (AutoMapper 13.0.1 lỗ hổng mức cao – A17), CS8604 (null có thể truyền vào `List<Claim>.Add` / `List<string>.Add`).
- Solution gồm 5 project: WebApi, Data, Model, Services, Generator.

**DB dev:** connection string trong `appsettings.json` trỏ `(localdb)\MSSQLLocalDB`, DB `AppDelicare`, Windows Auth (không phải `.\SQLEXPRESS`; instance SQLEXPRESS đang chạy nhưng không có DB này).
- **124 bảng `dbo` khớp 100% DDL 20261113**: tên bảng, tên cột, kiểu dữ liệu, NULL/NOT NULL (2264 cột). Khác biệt duy nhất: `CoreDataMergeConflict.EntityName/FieldName` DDL ghi `sysname`, DB báo `nvarchar(128)` – tương đương. Thêm bảng hệ thống `sysdiagrams` (SSMS). Không có FK vật lý, không có view. → **D-14 đã rõ, không cần chạy script nâng cấp.**
- 35 SP + 1 function. Đã xuất (chỉ đọc `OBJECT_DEFINITION`) dạng `CREATE OR ALTER`:
  - `DB/sp/` – 28 SP nghiệp vụ / hệ thống (`usp*`).
  - `DB/_ssms_diagrams/` – 7 SP `sp_*diagram*` + `fn_diagramobjects` (đồ của SSMS, không dùng).
- **SP code gọi mà DB thiếu: 74** → `DB/sp/_MISSING_FROM_DB.md` (A15). Ví dụ `api/Customer/GetAll` trả 500.
- SP có trong DB nhưng code không gọi: `uspSystem_BackupDB` – thực chất là RESTORE ghi đè DB (A16).

**Đăng nhập API:** chạy WebApi (Development, http://localhost:5125). `POST api/Authentication/Login` với tài khoản dev admin → 200, nhận JWT (hạn 7200 s, `isAdmin=true`, kèm `listMenu`). `GET api/UnitMeasure/GetAll` không token → 401; có token → 200 (`totalRecords=0`). Đã tắt WebApi sau khi thử.

**Vị trí bí mật trong code (chỉ vị trí, không ghi giá trị) – xử lý ở T01:**
| File:dòng | Loại |
|---|---|
| `AppDelicare/ClinicManagement.WebApi/Infrastructure/ConfigureServices.cs:11` | Khóa ký JWT (`SecretKey`) viết cứng |
| `AppDelicare/ClinicManagement.WebApi/Startup.cs:20` | ApplicationInsights QuickPulse `AuthenticationApiKey` |
| `AppDelicare/ClinicManagement.WebApi/Startup.cs:98` | License GemBox (`ComponentInfo.SetLicense`) |
| `AppDelicare/ClinicManagement.WebApi/appsettings.json:37` | Connection string (đã comment) có user `sa` + mật khẩu |
| `AppDelicare/ClinicManagement.Generator/App.config:4` | Connection string có user id + mật khẩu |
| WebApi `.csproj` | Chưa có `UserSecretsId` → T01 cần `dotnet user-secrets init` |

Các khóa trên đã nằm trong lịch sử git (commit `2501aef`) → khi làm T01 anh cần **sinh khóa mới / đổi mật khẩu**, không chỉ xóa khỏi code.

**Phát hiện thêm (ghi CODE_AUDIT):** A14 `GET api/AuditChange/DeleteData` xóa vật lý toàn DB (rất cao, D-26); A15 thiếu 74 SP; A16 `uspSystem_BackupDB`; A17 AutoMapper lỗ hổng.

File chính: `.claude/*`, `DB/sp/*.sql`, `DB/sp/_MISSING_FROM_DB.md`, `DB/_ssms_diagrams/*`, `DB/README.md`, `docs/claude/{PROGRESS,ROADMAP,DECISIONS,CODE_AUDIT}.md`.
Kiểm tra: `dotnet build` 0 lỗi; đăng nhập + gọi API bảo vệ OK; `git status` không có thay đổi trong `app-delicare-client/`.
AC: [x] nhánh + commit · [x] build baseline · [x] báo cáo lệch DB · [x] DB/sp + danh sách SP thiếu · [x] đăng nhập API · [x] danh sách vị trí bí mật.
Còn lại / GAP: không có GAP dữ liệu mới.
Cần anh: D-26; merge nhánh khi thấy ổn; chuẩn bị đổi khóa JWT / license / mật khẩu `sa` cho T01.

### T01 – Đưa bí mật ra khỏi code · 2026-11-14 · DONE
Kế hoạch: 1) tìm đủ vị trí bí mật 2) bật user-secrets, đặt khóa dev 3) đọc từ `IConfiguration` 4) kiểm tra bí mật lúc khởi động 5) placeholder + `.example` 6) `RequireExpirationTime`, thời hạn từ cấu hình 7) SECRETS_TODO 8) build + chạy thử.

Đã làm:
- `Infrastructure/StartupSecrets.cs` (mới): tên khóa cấu hình, `Validate()` dừng app và liệt kê đủ khóa thiếu / sai (thiếu `Jwt:SigningKey`, khóa < 32 byte, thiếu `ConnectionStrings:MainConnectionString`, `Jwt:AccessTokenMinutes` không hợp lệ) kèm cách đặt.
- `ConfigureServices.InitAuthentication(services, configuration)`: khóa ký lấy từ `Jwt:SigningKey`; `RequireExpirationTime = true`; `JwtIssuerOptions.ValidFor` lấy từ `Jwt:AccessTokenMinutes` (mặc định 120). Bỏ `ServiceLocator` trong hàm này.
- `Startup`: gọi `StartupSecrets.Validate` đầu tiên; QuickPulse key chỉ cấu hình khi có `ApplicationInsights:QuickPulseApiKey`; license GemBox từ `Licenses:GemBox`, trống thì dùng `FREE-LIMITED-KEY` và log cảnh báo.
- `JwtIssuerOptions`: xóa property `Secret` viết cứng (không nơi nào dùng). **T00 đã bỏ sót vị trí này**, T01 phát hiện khi quét rộng hơn.
- `ConfigurationCommon.Initialize(IConfiguration)`: trước đây lớp này chỉ đọc file `appsettings.json`, nên `DapperRepository` / `UnitOfWork` sẽ mất connection string khi chuyển sang user-secrets. Nay dùng cấu hình của host.
- `appsettings.json`: bỏ connection string (kể cả dòng comment có mật khẩu `sa`), thêm placeholder rỗng `Jwt`, `Licenses`, `ApplicationInsights`. Thêm `appsettings.Development.json.example`.
- `Generator/App.config`: connection string có user / mật khẩu → LocalDB + Windows Auth.
- `dotnet user-secrets init` (WebApi). Trên máy anh đã đặt: `Jwt:SigningKey` (**khóa ngẫu nhiên MỚI**, không dùng lại khóa cũ), `ConnectionStrings:MainConnectionString` (LocalDB), `Licenses:GemBox` (chép license hiện có).
- `docs/claude/SECRETS_TODO.md`: 6 mục anh cần đổi / thu hồi, cách đặt cho dev / staging / prod.

Kiểm tra:
- `dotnet build`: 0 lỗi, 10 cảnh báo (bằng mốc T00).
- Grep toàn repo (trừ `.git`, `bin`, `obj`, `.vs`, `backup-db`), kể cả `app-delicare-client/`: **0 file** còn chứa 6 chuỗi bí mật cũ.
- Chạy Development với user-secrets: login 200; token có `exp`, hạn 120 phút; `UnitMeasure/GetAll` không token 401, có token 200; `User/GetAll` (Dapper) 200 có dữ liệu; token sửa chữ ký 401.
- Thiếu / sai bí mật (`--ConnectionStrings:MainConnectionString=` `--Jwt:SigningKey=short`): app dừng, thông báo liệt kê 2 lỗi + cách đặt. Chạy Production thật (`--no-launch-profile`, không nạp user-secrets): app dừng, báo thiếu 2 khóa.
- Lưu ý: `dotnet run` dùng launch profile sẽ ép `ASPNETCORE_ENVIRONMENT=Development`. Muốn thử Production phải thêm `--no-launch-profile`.
- `git status`: không có thay đổi trong `app-delicare-client/`.

AC: [x] grep repo không còn chuỗi bí mật cũ · [x] app chạy được khi secret nằm trong user-secrets · [x] thiếu secret thì báo lỗi rõ lúc khởi động · [x] `RequireExpirationTime = true`, thời hạn từ cấu hình · [x] `.example` + SECRETS_TODO.
Còn lại: chưa có test tự động cho `StartupSecrets.Validate` (chưa có project test; thêm khi làm T09). `ServiceLocator.BuildServiceProvider` trong Startup để T03.
Cần anh: đổi khóa theo SECRETS_TODO.md (bắt buộc: khóa JWT staging / prod, mật khẩu `sa`, thu hồi QuickPulse key). Máy dev khác muốn chạy thì phải tự đặt user-secrets.

### T04 – Nâng gói lên .NET 8 đồng bộ · 2026-11-14 · DONE
Kế hoạch: 1) `dotnet list package --vulnerable` 2) tra bản 8.x mới nhất và bản vá 3) nâng gói chính 4) sửa lỗi build 5) ghim gói gián tiếp có lỗ hổng 6) giữ số cảnh báo ≤ mốc 7) chạy thử app.

Đã làm (gói thương mại EvoPdf, Syncfusion, GemBox **không đụng**):
| Gói | Cũ → Mới | Project |
|---|---|---|
| Microsoft.EntityFrameworkCore.SqlServer / Tools | 7.0.3 → 8.0.34 | WebApi |
| Microsoft.AspNetCore.Authentication.JwtBearer / OpenApi | 7.0.3 → 8.0.34 | WebApi |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 7.0.3 → 8.0.34 | Data |
| EFCore.BulkExtensions 7.0.1 → **EFCore.BulkExtensions.SqlServer** 8.1.3 | gói chỉ SQL Server, bỏ được Npgsql / MySqlConnector có lỗ hổng | Data |
| Microsoft.IdentityModel.Tokens | 6.27.0 → 7.7.3 (khớp bản JwtBearer 8 dùng, tránh trộn 7 / 8) | Model, WebApi |
| Swashbuckle.AspNetCore | 6.4.0 → 6.9.0 | WebApi |
| Serilog.AspNetCore | 6.1.0 → 8.0.3 | tất cả |
| Microsoft.Extensions.* | 7.0.x → 8.0.x | Model, WebApi |
| System.Data.SqlClient (gián tiếp, đã ngừng phát triển) → **Microsoft.Data.SqlClient 6.0.1** | đổi `using` ở `DapperRepository`, `UnitOfWork` | Data |
| AutoMapper.Extensions.Microsoft.DependencyInjection 12.0.0 → bỏ; Services dùng thẳng AutoMapper 13.0.1; Model bỏ hẳn (không dùng) | Model, Services |
| Ghim gói gián tiếp đã vá: System.Formats.Asn1 8.0.2, System.IO.Packaging 8.0.1, System.Net.Http 4.3.4, System.Security.Cryptography.Pkcs 8.0.2, System.Text.RegularExpressions 4.3.1 | Model |

- `Program.cs`: `UseSerilog` chuyển từ `IWebHostBuilder` (bị bỏ ở Serilog.AspNetCore 8) sang `IHostBuilder`. Cấu hình đọc từ `Serilog` trong appsettings vẫn như cũ.
- AutoMapper 13.0.1: **giữ**, vì bản vá 15.1.4+ là license thương mại → D-27. `NuGetAuditSuppress` 3 advisory **chỉ ở Services** để không báo trùng; cảnh báo vẫn hiện ở WebApi.

Kiểm tra:
- `dotnet build --no-incremental`: **0 lỗi, 10 cảnh báo** (= mốc T00: 6 dòng NU1903 AutoMapper ở WebApi + 4 dòng CS8604).
- `dotnet list package --vulnerable --include-transitive`: Data, Model **sạch**; WebApi / Services chỉ còn AutoMapper (D-27). Trước T04 có khoảng 15 gói có lỗ hổng.
- Chạy app: Swagger JSON 200 (572 route), UI 200; login 200; không token 401; `User/GetAll` (Dapper + Microsoft.Data.SqlClient) 200; `User/GetById` (EF Core 8) 200; `UnitMeasure/GetAllData`, `ObjectMenu/GetAll` 200; log không có Error / Exception.
- Chưa thử được: `DataTableBulkInsert` / `DataReaderBulkInsert` (SqlBulkCopy, chưa có màn hình gọi) và các thao tác ghi. Sẽ có test khi làm T09.
- `git status`: không có thay đổi trong `app-delicare-client/`.

AC: [x] build 0 lỗi · [x] số cảnh báo không tăng (10 = 10) · [x] app chạy.
Còn lại: D-27 AutoMapper. `Microsoft.EntityFrameworkCore.SqlServer` 8.0.34 kéo Microsoft.Data.SqlClient 5.1.x, còn Data dùng 6.0.1 → NuGet chọn 6.0.1 cho cả app (đã chạy thử OK).
Ghi chú: `AppDelicare/.vscode/` chưa được theo dõi, không phải em tạo, em không đụng. `.gitignore` gốc chỉ bỏ qua `.vscode/` ở thư mục gốc.

### T02 – Định danh người thao tác (ICurrentActor) · 2026-11-14 · DONE
Đã làm:
- `Data/Share/CurrentActor.cs` (mới): `ActorType` (User / System / Agent), `ActorInfo`, `ICurrentActor` (Actor, CorrelationId, `RequireActor()`, `BeginScope()` cho job / agent / bước đăng nhập), `UnauthorizedActorException`. HTTP lấy claim `id`; claim sai thì coi như không có người thao tác.
- `AuthenticatedService`: **bỏ fallback "user đầu tiên"**; `UserInfo` dùng truy vấn có tham số `@Id` (hết ghép chuỗi SQL).
- `DatabaseContext`: dùng `ICurrentActor`; có thay đổi mà không có người thao tác thì ném `UnauthorizedActorException`. Chặn ở **cả** `SaveChangesAsync(typeSave)` lẫn `SaveChanges()` / `SaveChangesAsync()` thường (trước đây 2 đường này bỏ qua audit). Audit ghi người thao tác + CorrelationId ở đầu cột `Changes` (GAP-AUD-01).
- `LoginService`: ghi `LocationTracking` trong `BeginScope(ActorInfo.ForUser(id))`.
- `CorrelationIdMiddleware` (đầu pipeline): header `X-Correlation-Id` hợp lệ (`^[A-Za-z0-9._-]{1,64}$`) hoặc tự sinh; trả lại ở response header; đẩy vào Serilog LogContext. Template log thêm `[{CorrelationId}]`, bật `Enrich: FromLogContext`.
- `appsettings`: `SystemUsers:JobUserId` (GAP-AUD-02). `HttpContextAccessor` đăng ký SingleInstance (đúng chuẩn ASP.NET Core).
- Project **`AppDelicare.Tests`** (xUnit, FluentAssertions 7.2.0 – bản 8 là license trả phí, EF InMemory) – nền cho T09.

Kiểm tra:
- `dotnet build`: 0 lỗi, 10 cảnh báo. `dotnet test`: **13/13 xanh**: chặn ghi khi không có người thao tác (3 đường SaveChanges); có token thì CreatedBy / ModifiedBy / DeleteddBy / AuditChange đúng; scope System cho job; scope lồng nhau; claim sai; middleware dùng header hợp lệ, tự sinh khi thiếu, từ chối header lạ (khoảng trắng, xuống dòng, quá 64 ký tự); log có thuộc tính CorrelationId.
- Chạy thật trên DB dev: login (header `X-Correlation-Id` được trả lại) → `TestDemo/Insert` bản ghi `[DEMO] T02 actor check`: `CreatedBy` = admin; AuditChange có `Actor: USER:<admin> | CorrelationId: t02-insert-check`; `LocationTracking.CreatedBy` = admin; log có `[t02-...]`. Sau đó xóa mềm bản ghi demo qua API: `DeleteddBy` = admin.
AC: [x] unit test chặn ghi khi không có người thao tác · [x] request có token thì CreatedBy đúng · [x] log có CorrelationId.
Còn lại: dùng `IClock` thay `DateTime.Now` để T05 (cùng D-18). Agent actor chỉ dùng thật ở Release 2.

### T03 – Pipeline HTTP chuẩn · 2026-11-14 · DONE (1/2 + 2/2)
> **Lưu ý:** phần dưới làm theo T03 bản cũ. Bộ tài liệu mới của anh (2026-11-14, CLAUDE.md / ROADMAP / DECISIONS / ORG_STRUCTURE…) đổi quy ước v1 sang envelope + If-Match + Idempotency-Key + X-Branch-Id và bỏ yêu cầu tương thích Angular (D-20). Phần 2/2 sẽ làm theo bản mới; lỗi v1 dùng envelope (D-28).
Đã làm:
- `Startup.Configure` theo thứ tự: CorrelationId → ExceptionHandler → HttpsRedirection → Swagger / Static → Routing → CORS → Authentication → Authorization → Endpoints. Bỏ `UseDeveloperExceptionPage` (lộ stack trace), `ServiceLocator.BuildServiceProvider`, `CorsMiddleware` (`Access-Control-Allow-Origin: *`), policy `AllowAnyOrigin` trong `DatabaseConnection`. `ServiceLocator.cs`, `CorsMiddleware.cs` → `_obsolete/AppDelicare/ClinicManagement.WebApi/Infrastructure/`.
- CORS từ `Cors:AllowedOrigins` (mặc định `http://localhost:4200`), expose `X-Correlation-Id`, `Content-Disposition`. Angular (đọc, không sửa): gọi `URL_API = '/api/'` qua `proxy.conf.json` → cùng origin, không bị ảnh hưởng; `jwt.interceptor` chỉ xử lý 401 / 403 → route cũ giữ nguyên mã HTTP.
- `ProblemDetailsExceptionHandler`: lỗi chưa bắt → log (kèm CorrelationId) + 500 ProblemDetails, không lộ message / stack; `UnauthorizedActorException` → 401. `AddProblemDetails`: mọi ProblemDetails có `traceId` = CorrelationId.
- Route mới: `Controllers/V1/ApiV1ControllerBase` (`FromResult` / `FromResults` → mã HTTP thật, lỗi → ProblemDetails); `ApiV1Convention` chỉ áp cho controller v1 (khai báo response 400 / 401 / 403 / 500 dạng problem cho Swagger). Route cũ **không đổi**.
- Endpoint v1 đầu tiên (Spec 2.2): `GET /api/v1/audit/{table}/{id}/history?take=` – lịch sử xử lý của bản ghi (AuditChange + tên người thao tác), validate `table` (regex) và `take` (1–200) → 400 ValidationProblemDetails.
- Swagger: XML comments (WebApi + Model, tắt CS1591 cho code cũ), mô tả quy ước v1 / route cũ, JWT kiểu `http bearer` (dán token, không cần chữ "Bearer").
- Mâu thuẫn tài liệu: Spec 1.2 đề xuất envelope `{success, data, …}` cho v1; CLAUDE.md mục 4 chốt `Result<T>` + ProblemDetails → **theo CLAUDE.md** (mới hơn, là quy ước của anh).

Kiểm tra:
- `dotnet build`: 0 lỗi, cảnh báo ≤ 10. `dotnet test`: **25/25 xanh** (thêm 12): integration test bằng `WebApplicationFactory` (DB dev qua user-secrets, không có DB thì Skip kèm lý do): `Supplier/GetAll` cũ HTTP 200 + đúng dạng `totalRecords / listData / responseCode / responseMessage`; route cũ / v1 không token 401; v1 `table` sai → 400 `application/problem+json` có `errors.table`, `traceId` = header gửi lên; `take=0` → 400; v1 hợp lệ 200; CORS: localhost:4200 được, origin lạ không có header; Swagger có endpoint v1, summary tiếng Việt, response 400, scheme bearer; mọi response có `X-Correlation-Id`; exception handler 500 không lộ chuỗi bí mật / tên exception / stack; actor thiếu → 401.
AC: [x] swagger chạy · [x] `Supplier/GetAll` cũ trả đúng dạng cũ · [x] endpoint v1 mẫu trả 400 ProblemDetails khi input sai.
Phát hiện: A18 – `Repository.GetAllNoneDeleted()` không lọc `IsDeleted` (sửa ở T05).

#### T03 phần 2/2 – quy ước v1 theo bộ tài liệu mới · 2026-11-14 · DONE
Đã làm (thư mục `WebApi/Api/`):
- `ApiEnvelope<T>` `{success, data, message, errors[{code, field, message}], traceId}`, `PagedData<T>` `{items, total, page, pageSize}`, `ErrorCodes` = Phụ lục A.
- `ApiControllerBase` (thay `ApiV1ControllerBase`): action trả dữ liệu thô; `ComputeETag` (ModifiedDate ?? CreatedDate), `SetETag`, `EnsureIfMatch` → 409 `CONCURRENCY_CONFLICT`.
- `EnvelopeResultFilter`: bọc mọi kết quả của controller v1; 400 model binding / DataAnnotations → `VALIDATION_FAILED` kèm `field` camelCase (route cũ giữ factory mặc định).
- `ExceptionToErrorMiddleware` (chỉ `/api/v1`): `ApiException` → mã HTTP + envelope; `UnauthorizedActorException` → 401; lỗi khác → 500 không lộ chi tiết; response lỗi không body (401 JWT, 403, 404 route, 405) cũng thành envelope (D-28).
- `IdempotencyFilter` + `[Idempotent]`: header `Idempotency-Key` (≤ 200) lưu vào sổ `IntegrationLog` (INSERT 1 dòng sau khi lệnh thành công, cùng transaction với lệnh trên SQL Server); gửi lại cùng nội dung → không chạy lại, trả mã cũ + `Idempotency-Replayed: true`; khác nội dung → 422; lệnh lỗi không lưu → thử lại được. GAP-IDEM-01/02.
- `BranchHeaderMiddleware` + `IBranchContext`: `X-Branch-Id` phải là GUID (sai → 400); kiểm tra quyền theo đơn vị ở T07.
- `PageQuery` (`page`, `pageSize` ≤ 200, `q`, `sort=field:asc`) + `ApplySort` theo whitelist (cột lạ → 400).
- `ApiJson`: camelCase; **không bao giờ ghi ra** `PassWord` / `Password` / `PasswordHash` (vẫn đọc được khi đăng nhập) – áp cho toàn app.
- Swagger nhóm theo module: `v1 / hr / shifts`, `v1 / audit`, `legacy / <Controller>`; mô tả quy ước v1.
- Entity `IntegrationLog` (đúng DDL, không kế thừa BaseEntity vì bảng không có DeletedDate / DeleteddBy) + DbSet.
- Endpoint mẫu **`/api/v1/hr/shifts`** (Spec 4.7): GET danh sách / chi tiết (ETag), POST (201, `[Idempotent]`), PUT (If-Match), DELETE (xóa mềm, 204). Kiểm tra: bắt buộc mã / tên / giờ (400), `companyId` không tồn tại (422 `REF_NOT_FOUND`), giờ bắt đầu = kết thúc (422 `BUSINESS_RULE`, GAP-HR-01), trùng mã (409 `DUPLICATE`), ca qua đêm hợp lệ (`isOvernight`). Sau khi lưu đọc lại bản ghi để ETag khớp độ chính xác `datetime` của SQL.
- `AuditController` chuyển sang envelope.
- `GemBoxLicense.Apply`: GemBox chỉ cho `SetLicense` 1 lần mỗi tiến trình (lỗi "serial key can be set only once" khi host khởi động lại – lộ ra khi chạy 2 test host).
- `docs/api-tests/v1-hr-shifts.http`: luồng thử bằng REST Client.

Kiểm tra:
- `dotnet build`: 0 lỗi, 6 cảnh báo. `dotnet test`: **45/45 xanh**. Test v1 chạy trên host InMemory + JWT tự ký (không cần DB): 201 / 200 envelope + phân trang; 400 thiếu trường (đủ field), sai giờ, sort / pageSize / page sai, X-Branch-Id sai; 422 `REF_NOT_FOUND`, `BUSINESS_RULE`; 409 `DUPLICATE`, `CONCURRENCY_CONFLICT` (dữ liệu không đổi); Idempotency gửi 2 lần → 1 ca + 1 log; key dùng lại khác nội dung → 422; lệnh lỗi rồi thử lại cùng key → 201; 404 sau xóa; 401 / 404 route dạng envelope; mật khẩu không bị serialize.
- Chạy thật trên SQL Server dev: POST 2 lần cùng key → 201 + `Idempotency-Replayed: true`, DB có 1 ca + 1 dòng IntegrationLog (`Succeeded`, 201); ETag lúc tạo = lúc GET; PUT ETag mới 200, ETag cũ 409; DELETE 204; tiếng Việt lưu / đọc đúng. Dữ liệu thử: ca `DEMO-T03`, `DEMO-T03-UTF8` (đã xóa mềm) và 1 dòng IntegrationLog key `demo-t03-live-1` (sổ chỉ ghi thêm, để lại).
AC: [x] swagger chạy · [x] 200 đúng envelope · [x] 400 / 422 đúng mã lỗi · [x] 409 khi If-Match lệch · [x] Idempotency-Key gửi 2 lần chỉ ghi 1 lần.
Còn lại: lọc cột deprecated làm theo DTO khi sinh mã (T05 / T45B); `[Permission]` ở T07.

### T05 – Bộ sinh mã mới + đồng bộ entity với 124 bảng · 2026-11-14 · DONE
Đã làm:
- **`tools/AppDelicare.CodeGen`** (console .NET 8, có trong solution, README): đọc DDL (cột, kiểu, NULL, UNIQUE, DEFAULT) + DEPRECATED_COLUMNS + LOGICAL_RELATIONS; sinh theo mẫu `Warehouse*`: Entity, Request, SearchRequest, ViewModel, I/Repository, I/Service, Controller (route cũ), SP `usp<Class>_GetAll`, `DatabaseContext.Generated.cs`, `IUnitOfWork.Generated.cs` / `UnitOfWork.Generated.cs`, `AutoMapperGeneratedProfile`. Cờ `--only`, `--skip-existing` (mặc định), `--force`, `--layers`, `--dry-run`. Entity + file tổng hợp luôn sinh lại; các lớp khác chỉ tạo khi chưa có (không đè service sửa tay).
- Khác mẫu cũ có chủ đích: xóa **không** dùng SQL động `uspSystem_ExecuteQuery` (để ReferenceGuard T07); kiểm tra trùng theo 18 UNIQUE của DDL + `Code`; gán DEFAULT của SQL khi client không gửi; ghi song song cột deprecated; sổ chỉ ghi thêm không có Update / Delete.
- **Entity đồng bộ 124 bảng**: `[Table("TênThật")]` (Dishe, ProductionBatche… giữ tên bảng thật, class bỏ hậu tố), decimal / datetime / date có `[Column(TypeName)]`, `[MaxLength]`, `[Required]`. `BaseEntity` thêm `DeletedBy`; xóa mềm ghi cả `DeletedBy` và `DeleteddBy`; bảng thiếu cột nào thì EF `Ignore`. `CoreDataMergeConflict` (Id bigint, không cột audit) là entity riêng.
- `DatabaseContext`: `partial`, kế thừa `DbContext` (bỏ `IdentityDbContext` – không có bảng AspNet* và không code nào dùng, A11). `IUnitOfWork` / `UnitOfWork`: `partial`, thuộc tính repository viết tay thay bằng bản sinh.
- **Chuyển `_obsolete/AppDelicare/…` (81 file)**: AppConfig, Area, Branch, Department, Opportunity, Region, Lead(s), Product(s), Employees (không có bảng – D-13) và UnitOfMeasure (bảng đã thành `_Legacy`, dùng UnitMeasure – D-02). `ContractMeals` → `ContractMeal`.
- Sửa lỗi lộ ra khi đồng bộ: `CustomerService` kiểm tra trùng bằng cột `CifNo` **không tồn tại** (so với `request.Code`) → so `Code`; 28 SearchRequest có bộ lọc ngày `DateTime` không nullable (gửi `0001-01-01` → `SqlDateTime overflow`, 27 API GetAll trả 500) → `DateTime?`; A18 `Repository.GetAllNoneDeleted()` nay lọc `IsDeleted`.
- 102 SP `GetAll` mới trong `DB/sp/` (74 SP code cũ gọi mà DB thiếu + bảng mới), khai báo thêm mọi tham số service cũ đang truyền. **Đã chạy lên DB dev** (LocalDB): DB có 122 SP `usp*_GetAll`.
- Test `Schema/SchemaMappingTests` (đọc DDL trong test, dựng model SQL Server không kết nối): DDL 124 bảng; mọi DbSet → bảng có thật, mọi property → cột có thật; mọi bảng / cột DDL đều được map; kiểu C# khớp kiểu SQL; tên bảng legacy giữ nguyên.

Kiểm tra:
- `dotnet build --no-incremental`: 0 lỗi, **10 cảnh báo** (= mốc). `dotnet test`: **50/50 xanh**.
- Chạy thật trên DB dev: **119/119** route `api/<X>/GetAll` (Dapper + SP) trả 200 / `responseCode` 200; **117/117** route `GetAllData` (EF, đọc mọi cột) trả 200; log không có lỗi.
AC: [x] build xanh · [x] test DbSet ↔ bảng / property ↔ cột (đọc DDL) xanh · [x] không còn entity map tới bảng không tồn tại.
Còn lại: service cũ vẫn kiểm tra xóa bằng SQL động (A10) → T07. D-27 (AutoMapper) chưa chốt nên template vẫn dùng AutoMapper.

### T06 – Sinh mã cho 35 bảng còn thiếu · 2026-11-14 · DONE
Đã làm: CodeGen sinh trọn CRUD (Request, SearchRequest, ViewModel, Service, Controller, SP) cho **34 bảng** của CODE_AUDIT A1(c) – 238 file; bảng thứ 35 `CoreDataMergeConflict` chỉ có entity + repository (không theo khuôn BaseEntity). 2 bảng `_Legacy_20261113` chỉ có entity + repository (chỉ đọc). 7 sổ chỉ ghi thêm (`IntegrationLog`, `ApprovalDecision`, `TemperatureLog`, `PackagingLedger`, `AttendanceEvent`, `DeliveryEvent`, `ComplaintHistory`): controller **không có** Update / Delete, service từ chối sửa / xóa.
Kiểm tra: build xanh; SP trong `DB/sp/` và đã chạy lên DB dev; DbSet đủ **124** bảng (có entity đọc cho 2 bảng legacy); GetAll / GetAllData của các bảng mới chạy 200 trên DB dev (nằm trong 119 / 117 ở T05).
AC: [x] build xanh · [x] SP nằm trong `DB/sp/` · [x] DbSet đủ 124.

### T07 – Phân quyền UserRole × ObjectMenu + ReferenceGuard + seed tổ chức · 2026-11-14 · DONE
Đã làm:
- `WebApi/Security`: `[Permission(screenId, PermissionAction.View|Add|Edit|Delete|Print|Export|Import|Approve)]`, `[AdminOnly]`, `[SkipPermission]`; `PermissionService` (UserClaim → UserGroup → UserRole × ObjectMenu theo MetaLink `#/<screen-id>`, OR qua các nhóm, cache `Permissions:CacheMinutes` = 5′, `User.IsAdmin` bỏ qua, user không hoạt động → không quyền); `PermissionFilter` toàn cục (403; v1 → envelope `PERMISSION_DENIED`; action cần đăng nhập mà không khai báo quyền → chỉ admin).
- `PermissionConvention`: gắn quyền tự động cho **mọi controller CRUD cũ** theo `ScreenMap` (tên controller → screen-id của catalog.js; không có màn riêng → `dbx-explorer`) + tên action (Get* → View, Insert → Add, Update / Save* / Reset* → Edit, Delete → Delete, Export / Print / Import). `GetProfile`, `UpdateProfile`, `ChangePassword` chỉ cần đăng nhập. **`AuditChange/DeleteData` và `User/SaveIsAdmin` chỉ admin** (D-26, chống tự nâng quyền). v1: `hr/shifts` và `audit` khai báo tường minh.
- Duyệt (A): GAP-PERM-01 → `Permissions:ApproverGroups` trong appsettings (production: CEO, COO → **OPS-SCM không duyệt được kế hoạch sản xuất**).
- **ReferenceGuard** (`Data/Share`): đếm bản ghi chưa xóa ở các bảng nguồn theo `ReferenceMap.Generated.cs` (sinh từ LOGICAL_RELATIONS) bằng LINQ có tham số – chạy được cả SQL Server lẫn InMemory. Thay khối SQL động `uspSystem_ExecuteQuery` trong **83 service cũ** (qua `IAuditChangeService.IsReferencedAsync`, không phải sửa constructor) và template CodeGen. v1 `DELETE /hr/shifts/{id}` → 409 `IN_USE` kèm bảng đang dùng.
- CodeGen: sinh `ScreenMap.Generated.cs`, `ReferenceMap.Generated.cs`, `DB/seed/OrganizationUnit_seed.sql` (Company DLC + 33 đơn vị + 6 KitchenWorkstation), `ObjectMenu_seed.sql` (11 nhóm + 119 màn, MetaLink `#/<screen-id>`), `UserRole_seed.sql` (16 nhóm × 119 màn theo ma trận). Idempotent theo Code / MetaLink / Name, 1 batch, không DELETE, CreatedBy = tài khoản `admin` khai báo rõ. File do generator sở hữu (header `<auto-generated>`) nay luôn sinh lại.
- **Sửa lỗi CodeGen của T05**: regex đọc LOGICAL_RELATIONS đòi ký hiệu `→` (chỉ có ở tiêu đề) → map quan hệ rỗng → ViewModel / SP sinh mới thiếu nhãn `XxxName`. Đã sửa, sinh lại 22 ViewModel + 83 SP (có JOIN nhãn), đã chạy lại SP lên DB dev.
- **Đã chạy seed lên DB dev 2 lần**: Company 1, OrganizationUnit 33, KitchenWorkstation 6, ObjectMenu 156 (26 cũ + 130), UserGroup 36 (20 cũ + 16), UserRole 2.349 – lần 2 không đổi.

Kiểm tra:
- `dotnet build --no-incremental`: 0 lỗi, 10 cảnh báo. `dotnet test`: **61/61 xanh** (+11): không AllowAdd → 403 envelope (v1) và 403 (route cũ `Shift/Insert`), vẫn xem được; có AllowAdd / admin → 201; không AllowAccess → 403 cả khi xem; user không hoạt động → 403; DeleteData / SaveIsAdmin → 403 với user đủ quyền nhưng không phải admin; xóa ca đang có ShiftAssignment → 409 `IN_USE`, tham chiếu đã xóa mềm không chặn; OPS-SCM không duyệt `prod-plan`, COO / CEO duyệt được (đọc appsettings thật); **mọi endpoint cần đăng nhập đều có khai báo quyền** (> 600 endpoint); seed chạy 2 lần trên DB dev trong transaction rồi rollback → số dòng không đổi, không trùng mã / MetaLink / cặp nhóm × menu, cây OPS-KIT-COOK → OPS-KIT.
- Chạy thật trên DB dev: 119/119 `GetAll` 200 với admin; v1 shifts 200; log không lỗi.
AC: [x] không AllowAdd gọi Insert → 403 · [x] admin → 200 · [x] xóa bản ghi đang được tham chiếu → 409 · [x] seed tổ chức chạy 2 lần không nhân bản · [x] OPS-SCM không duyệt được kế hoạch sản xuất.
Còn lại: kiểm tra quyền theo đơn vị (`X-Branch-Id` ↔ OrganizationUnit của user) chưa có dữ liệu liên kết user ↔ đơn vị (GAP-C-10) → làm khi có Employee ↔ User. Cache quyền 5′: đổi quyền có hiệu lực sau tối đa 5 phút.

### T08 – Khóa sổ chỉ ghi thêm · 2026-11-14 · DONE
Đã làm:
- `DatabaseContext.AppendOnlyTypes` = 9 sổ CLAUDE.md 3.3 (InventoryTransaction, PackagingLedger, TemperatureLog, AuditChange, IntegrationLog, ApprovalDecision, AttendanceEvent, DeliveryEvent, ComplaintHistory). Có dòng sổ ở trạng thái Modified / Deleted (kể cả xóa mềm) → `AppendOnlyViolationException` trên **mọi** đường SaveChanges. Route v1 → 422 envelope.
- Bỏ `Update` / `Delete` của `InventoryTransactionController` cũ (các sổ sinh ở T06 vốn không có).
- `InventoryLedgerService` (Spec 7): `PostAsync` ghi sổ + upsert `InventoryBalance` theo (kho, vị trí, nguyên liệu, lô) trong 1 transaction, chặn tồn âm (422 `INSUFFICIENT_STOCK`), ghi song song cột deprecated NOT NULL (UomId, LotId, TransactionNo, OnHandQty / ReservedQty / AvailableQty). `ReverseAsync(id, reason)`: bút toán ngược dấu tham chiếu dòng gốc (GAP-INV-01), không đảo 2 lần / không đảo bút toán đảo (409 `INVALID_TRANSITION`). Dùng chung transaction ngoài nếu có (vd. `[Idempotent]`).
- `BusinessRuleException` (tầng service, không phụ thuộc WebApi) → envelope với mã HTTP + mã lỗi Phụ lục A.
- v1 `POST /api/v1/scm/inventory-transactions/{id}/reverse` (`[Idempotent]`, quyền Edit `#/scm-inv-txn`).

Kiểm tra:
- `dotnet build --no-incremental`: 0 lỗi, 10 cảnh báo. `dotnet test`: **71/71 xanh** (+11): danh sách 9 sổ; sửa dòng sổ bị chặn ở cả 3 đường SaveChanges; xóa mềm / xóa cứng bị chặn; bảng thường vẫn sửa được; nhập 120 → xuất 20 (tồn 100) → đảo phiếu xuất → **tồn 120**, dòng gốc giữ nguyên, dòng đảo có ReferenceType / ReferenceId / Reason / CreatedBy; đảo 2 lần / đảo bút toán đảo → 409; đảo phiếu nhập đã xuất bớt → 422, xuất quá tồn → 422; v1 reverse 201 → lần 2 409, thiếu lý do 400; route cũ không còn Update / Delete.
- **SQL Server thật**: Post + Reverse trên DB dev trong transaction rồi rollback – kiểu decimal(18,4) / cột deprecated NOT NULL ghi đúng, tồn về 0, rollback không để lại dòng sổ.
AC: [x] cập nhật / xóa sổ bị chặn · [x] bút toán đảo làm tồn trở về đúng.
Còn lại: `AuditChange/DeleteData` (đã khóa admin) vẫn DELETE vật lý bằng SQL nên **vượt qua khóa sổ** → D-26. Khóa dòng tồn khi nhiều người xuất cùng lúc (UPDLOCK / rowversion) làm cùng T16.

### T09 – Dự án test · 2026-11-14 · DONE
Đã làm: `AppDelicare.Tests` (xUnit, FluentAssertions 7.2.0, EF InMemory, Mvc.Testing, Xunit.SkippableFact) đã dựng dần từ T02 / T03 / T05 / T07 / T08; T09 bổ sung `ConnectionStrings:TestDb` (có thì test tích hợp dùng DB này thay MainConnectionString) và `AppDelicare.Tests/README.md` (3 loại test, cấu hình, quy ước: test ghi SQL Server phải ROLLBACK, không commit dữ liệu test vào sổ).
Kiểm tra: `dotnet test` trên máy anh **71/71 xanh**, 0 skip (DB dev có); không có DB → test tích hợp Skip kèm lý do.
AC: [x] `dotnet test` chạy được trên máy anh · [x] README test.
Ghi chú: "Unit test cho Application" làm khi có `AppDelicare.Application` (T10).

### T48.1 – Staging backend · 2026-11-14 · DONE
Đã làm:
- Cấu hình theo môi trường: `appsettings.Staging.json`, `appsettings.Production.json` (không có bí mật – chỉ biến môi trường); log path dùng `/` (chạy được Linux).
- `/health` (API + DB qua `AddDbContextCheck`, JSON: status, thời gian, environment, version) và `/health/live`; không cần đăng nhập, không lộ chi tiết.
- `deploy/db/Deploy-Database.ps1` (idempotent): tạo DB (tùy chọn) → **chỉ khi DB rỗng** chạy DDL chuẩn của PO (bỏ dòng `USE`) → mọi SP `DB/sp` → admin ban đầu (`DB/seed/00_Admin_seed.sql`, hash tính lúc chạy, không lưu mật khẩu) → seed tổ chức / menu / quyền. GAP-DEP-01.
- IIS: `deploy/iis/Publish-Iis.ps1` (publish có web.config), hướng dẫn Hosting Bundle, App Pool, biến môi trường bí mật trên máy chủ.
- Docker: `AppDelicare/ClinicManagement.WebApi/Dockerfile` (multi-stage, giờ VN, healthcheck, user không root), `AppDelicare/.dockerignore`, `deploy/docker/docker-compose.staging.yml` (SQL Server 2022 + `db-init` + API, không Angular), `db-init.sh`, `staging.env.example` (file thật gitignore). **Chưa chạy thử được – máy dev chưa cài Docker.**
- CI: `.github/workflows/backend-ci.yml` – build + test + báo cáo gói lỗ hổng cho mỗi PR / push main; không deploy.
- `deploy/README.md` (từng bước 3 cách + kiểm tra + sự cố), `docs/claude/UAT_CHECKLIST.md` (R1.0, 14 kịch bản), `docs/api-tests/r1.0-foundation.http`.
- Gỡ ProjectReference tới `AppDelicare.Generator` (T4 .NET Framework, không code nào dùng) khỏi WebApi để publish / build Linux được. `uspSystem_BackupDB` (RESTORE ghi đè DB) → `_obsolete/DB/sp/` để script triển khai không bao giờ cài.
- **Sửa lỗi lộ ra khi dựng DB mới**: đăng nhập lỗi 500 khi user không có Person; `uspUserRole_GetByUserGroupId` lỗi chuyển kiểu khi user chưa thuộc nhóm nào (chạy lại lên DB dev + staging); đăng nhập gọi `api.iplocation.net` (A20) → ghi IP client.

Kiểm tra (máy anh, mô phỏng máy sạch – DB mới, bí mật chỉ qua biến môi trường, chạy bản publish Release ở `Staging`):
- `Deploy-Database.ps1` trên DB **trống** `AppDelicare_Staging` (LocalDB): 124 bảng, 130 SP, admin, seed; chạy lần 2 không đổi gì (124 bảng / 1 user / 33 đơn vị / 130 menu / 16 nhóm / 1.904 quyền).
- `/health` Healthy (database Healthy, environment Staging); `/health/live` 200; Swagger UI + JSON 200; đăng nhập admin staging → token; **119/119** `GetAll` 200; OrganizationUnit 33, UserGroup 16, v1 shifts 200; LocationTracking ghi IP client.
- `dotnet build --no-incremental` 0 lỗi / 10 cảnh báo; `dotnet test` 71/71.
- Hash mật khẩu ở 3 nơi (C#/PowerShell, Node, `md5sum` trong container) cho cùng kết quả.
AC: [x] staging backend chạy được từ máy sạch theo README (cách A / B) · [x] `/health` xanh · [x] Swagger staging mở được · [x] đăng nhập qua API và gọi được API danh mục. (Cách C Docker: chưa kiểm được.)
Bí mật staging trên máy anh: `deploy/staging.local.md`, `deploy/staging.local.env.ps1` (gitignore, không commit).

### T10 – Khung AppDelicare.Application · 2026-11-14 · DONE
Đã làm:
- Project **`AppDelicare.Application`** (tham chiếu Data, Model; WebApi + Tests tham chiếu nó), thư mục theo miền bắt đầu từ `Common/`.
- Messaging tự viết (MediatR từ v13 là license thương mại): `ICommand<T>` / `IQuery<T>`, `IRequestHandler`, `IDispatcher` (validator → handler → phát sự kiện), `IDomainEvent` + `IDomainEventHandler` + `IDomainEventCollector`: sự kiện chỉ phát khi **lệnh ngoài cùng thành công**, lệnh lỗi → hủy sự kiện. Gọi handler kiểu mạnh (exception không bị bọc).
- Validator: **FluentValidation 12.2** (Apache-2.0). Lỗi → `ValidationFailedException` → 400 `VALIDATION_FAILED` từng `field` (camelCase).
- `IClock` / `SystemClock`: giờ VN theo `BusinessRules:TimeZone` (Kind = Unspecified để ghi thẳng cột datetime). `BusinessRulesOptions` + section `BusinessRules` trong appsettings (BUSINESS_RULES.md).
- `AddApplication()` quét assembly đăng ký handler / validator / handler sự kiện.
- Use case mẫu **`Common.LookupQuery`** (Spec 2.2): `GET /api/v1/meta/lookups/{table}?q&take&ids` → `[{ id, label, code, status }]`; nhãn DisplayName → Name → FullName → số chứng từ → Code; chỉ Active khi bảng có Status; `ids` lấy đúng giá trị đang chọn; nhận tên bảng thật (`Dishe`) hoặc tên class (`Dish`); chặn bảng nhạy cảm (GAP-API-02). Truy vấn động theo metadata EF, không SQL ghép chuỗi. `MetaController` mỏng chỉ gọi dispatcher.
- Sửa: dò bảng theo `[Table]` (đúng cả provider InMemory) trong LookupQuery và ReferenceGuard.

Kiểm tra: build 0 lỗi / 10 cảnh báo; test **83/83** (+12): dispatcher chạy handler + phát sự kiện sau thành công, lệnh lỗi hủy sự kiện, validator lỗi không chạy handler; IClock = UTC+7; BusinessRules đọc từ cấu hình + mặc định; lookup: thứ tự nhãn, lọc Inactive / đã xóa, tìm theo mã, theo ids, tên bảng legacy, chặn User / AuditChange / Patient / bảng lạ / _Legacy; end-to-end qua API (200, 400 `take`, 404 bảng chặn). Chạy thật trên DB dev: lookup OrganizationUnit / UserGroup / KitchenWorkstation / ObjectMenu / Company / Dishe 200, log không lỗi.
AC: [x] 1 use case mẫu (`Common.LookupQuery`) chạy end-to-end và có test.

### T21.1 – Seed ca làm việc · 2026-11-14 · DONE
Đã làm: `DB/seed/Shift_seed.sql` – 4 ca theo đề xuất D-19 (chưa chốt, mã ca chỉ nằm ở bảng Shift nên đổi lại dễ): SANG_6H Sáng 06:00–09:00, TRUA Trưa 10:30–13:30, TOI Tối 16:30–19:30, DEM_23H Đêm 23:00–01:00 (qua đêm). Giờ là PROPOSED. Idempotent theo Code, **không ghi đè** tên / giờ đã có (giữ chỉnh sửa qua `/api/v1/hr/shifts`). Thêm vào `Deploy-Database.ps1`, `db-init.sh`, `SeedTests`.
Đã chạy lên DB dev và staging (2 lần mỗi DB): đúng 4 ca, không trùng.
Kèm theo: log chuyển từ `wwwroot/Logs` sang `Logs/` (ngoài thư mục web tĩnh). Đã kiểm: `/Logs/*.log` hiện trả 404 (kiểu .log không được phục vụ) nên **chưa lộ**, nhưng để trong wwwroot dễ lộ nếu bật phục vụ mọi kiểu file, bị đóng gói khi publish và khóa file khi build.
Kiểm tra: test 83/83 (SeedTests kiểm thêm 4 ca, không trùng mã).
AC (phần seed của T21): [x] 4 ca TRUA, TOI, DEM_23H, SANG_6H.

### T45B.1 – CRUD chuẩn v1 cho màn hình P0–P2 · 2026-11-14 · DONE
Đã làm:
- CodeGen sinh `AppDelicare.Application/Common/Crud/TableMetadata.Generated.cs`: mỗi bảng – cột (kiểu, NOT NULL không DEFAULT, độ dài, audit, deprecated + cột thay thế, bảng đích theo LOGICAL_RELATIONS), UNIQUE, cột nhãn, cột tìm kiếm.
- `ResourceRegistry`: **39 resource** cho 34 màn P0–P2 (`/api/v1/{module}/{resource}` theo spec mục 3–5), cờ CanCreate / CanUpdate / CanDelete (audit-changes, access-logs, recipes, recipe-lines, standard-costs: chỉ đọc – ghi qua API nghiệp vụ T45B.4), luật riêng theo spec: org-unit chặn vòng cha–con, object-menu MetaLink duy nhất, delivery-location khung giờ + tọa độ, ingredient YieldPercent / StandardWastePercent / ShelfLifeDays, uom-conversion factor > 0 + from ≠ to + không trùng cặp, inventory-policy Min ≤ Safety ≤ Reorder ≤ Max + một dòng / (nguyên liệu, kho), price-policy UnitPrice > 0.
- `CrudService` (Application): danh sách (page / pageSize / q / sort / lọc theo cột camelCase / `xxxFrom` `xxxTo`), `summary` (total + byStatus), chi tiết + ETag + `{field}Label` cho cột tham chiếu, POST / PUT (chỉ trường gửi lên) / DELETE mềm. Kiểm tra: bắt buộc (gồm cột thay thế của cột cũ NOT NULL), kiểu, độ dài, trường lạ / cột deprecated / chỉ đọc → 400; UNIQUE + Code → 409 DUPLICATE; tham chiếu → 422 REF_NOT_FOUND; If-Match → 409; đang dùng → 409 IN_USE (ReferenceGuard). Ghi song song cột deprecated. Không trả mật khẩu / cột xóa mềm. Truy vấn động bằng expression tree (có tham số).
- WebApi: `CrudResourceController` + 39 controller con (route tường minh, Swagger nhóm theo module); quyền theo màn qua `PermissionConvention` (đọc → View, POST → Add, PUT → Edit, DELETE → Delete); POST có `[Idempotent]`. `GET /api/v1/meta/resources`, `GET /api/v1/meta/schema/{module}/{resource}` (trường, kiểu, bắt buộc, độ dài, bảng tham chiếu, trường nhãn) cho frontend.

Kiểm tra:
- Build 0 lỗi / 10 cảnh báo; test **91/91** (+8): vòng tạo / đọc / danh sách / summary; 400 trường lạ / sai kiểu / quá dài / cột deprecated / thiếu cột thay thế; 409 DUPLICATE; 422 REF_NOT_FOUND (đúng field); nhãn tham chiếu; If-Match 409; xóa đang dùng 409 IN_USE; ghi song song FromUomId; luật uom-conversion; resource chỉ đọc 405; phân quyền theo màn (xem được, thêm 403, màn khác 403); schema + danh sách resource.
- **SQL Server thật**: 39 resource × (danh sách có sort + summary) = **78/78 200**; tạo kho `[DEMO]` 201 → trùng 409 → sửa ETag mới 200 / cũ 409 → xóa 204 → đọc 404; lịch sử có Added + Modified; log không lỗi.

### T45B.2 – API quản trị riêng của P0 · 2026-11-14 · DONE
Đã làm (logic ở `AppDelicare.Application/Admin`, controller mỏng ở `WebApi/Controllers/V1/Admin`, `AuthAndMe.cs`):
- **Người dùng** `/api/v1/admin/users`: danh sách (isActive, groupId, q trên tên đăng nhập / họ tên / SĐT) kèm person + groups; chi tiết; tạo (Person → User → UserClaim trong 1 transaction, tên đăng nhập chuẩn hóa chữ thường, trùng → 409, mật khẩu ≥ 8, nhóm không có → 422); sửa hồ sơ; `PUT {id}/groups` (thêm / xóa mềm); `lock` (bắt buộc lý do, không tự khóa mình) / `unlock`; `reset-password` (**chỉ admin**, mật khẩu tạm 12 ký tự RNG, trả 1 lần); `effective-permissions` (OR mọi nhóm + `grantedBy`). Không bao giờ trả mật khẩu.
- **Ma trận quyền nhóm** `/api/v1/admin/user-groups/{id}/permissions` GET (cây menu + cờ) / PUT (chuẩn hóa: bật cờ bất kỳ → bật AllowAccess, tắt AllowAccess → tắt hết) / `bulk`; `preview` (chỉ admin).
- Sự kiện `PermissionsChanged` (phát sau commit) → `PermissionCacheInvalidator` xóa cache quyền **ngay** cho user liên quan (cấp / thu hồi quyền, đổi nhóm, khóa / mở).
- **Menu** `/admin/object-menus/tree`, `PUT reorder` (đổi cha / thứ tự, chặn vòng lặp). **Đơn vị** `/admin/org-units/tree` (số nhân viên, số kho, tên quản lý), `/admin/branches` (UnitType = BRANCH, mọi user đăng nhập). **Merge-conflict** `/admin/merge-conflicts` (lọc resolved) + `POST {id}/resolve` (Id bigint, xử lý 2 lần → 409).
- **Meta** `/api/v1/meta/menu` (cây menu user được truy cập + cờ), `/api/v1/meta/me` ({ user, branches, permissions }).
- **Auth v1** `POST /api/v1/auth/login` → `{ accessToken, expiresIn, tokenType, userId, … }`, sai → 401 envelope (không phân biệt sai mật khẩu / không tồn tại); `POST /api/v1/auth/change-password` (sai mật khẩu cũ → 422). Refresh token chưa làm (GAP-AUTH-01).
- `PasswordHasher` – một chỗ băm duy nhất (vẫn MD5 chờ D-29). **AuditChange không còn ghi giá trị mật khẩu (kể cả hash)** – chỉ ghi "đã thay đổi" (trước đây đổi mật khẩu là hash lọt vào nhật ký).

Kiểm tra: build 0 lỗi / 10 cảnh báo; test **98/98** (+7): tạo user (không lộ mật khẩu, chuẩn hóa tên, 409 / 400 / 422, lọc q + groupId); cấp quyền có hiệu lực ngay + chuẩn hóa cờ + grantedBy + thu hồi ngay; khóa (thiếu lý do 400, tự khóa 422) → mất quyền ngay → mở lại; reset chỉ admin; đổi mật khẩu sai / đúng; audit không chứa hash; cây menu + reorder + chặn vòng lặp; cây đơn vị + đếm nhân viên + branches; merge-conflict resolve + 409; /meta/me + /meta/menu theo user; login v1 trên DB dev (token / 401 envelope).
Còn lại (→ T45B.5): `/meta/statuses`, `/meta/reference-lists`, refresh token.

### T45B.3 – API riêng của P1 (master data) · 2026-11-14 · DONE
Đã làm:
- CRUD chuẩn thêm 3 hook: `Enrich` (thêm trường tính toán vào danh sách / chi tiết), `CustomFilters` (key lọc riêng), `AfterSave` (xử lý sau khi lưu, cùng request).
- `AppDelicare.Application/MasterData/P1MasterData.cs`:
  - Khách hàng: danh sách thêm `activeContracts`, `locationCount`, `avgMealsPerDay7d`, `branchName`; trùng CIF → 409; `GET /sales/customers/{id}/overview` (khách, liên hệ, điểm giao, hợp đồng + suất, 15 đơn gần nhất, KPI: số đơn, doanh thu đã đối soát qua Order, số phản ánh).
  - Điểm giao: không cho `status = Inactive` khi còn DeliveryOrder chưa kết thúc → 409 IN_USE.
  - Giá NCC: lọc `effectiveOn` (EffectiveFrom ≤ ngày ≤ EffectiveTo); đặt dòng mới `isPreferred` → tự tắt dòng ưu tiên cũ của cùng nguyên liệu.
  - Nhà cung cấp: `GET /scm/suppliers/{id}/overview` (bảng giá + tên nguyên liệu, PO đang mở, vấn đề NCC, KPI 90 ngày onTimeRate / rejectRate – GAP-P1-01).
  - Nguyên liệu: danh sách thêm `availableQuantity` (Σ InventoryBalance) và `belowMin`.
  - Kho: `GET /scm/warehouses/{id}/layout` (khu + nhiệt độ + vị trí, dùng StorageZoneId).
  - `IUomService.ConvertAsync` + `GET /scm/uom-conversions/convert`: chiều xuôi, ngược, qua đơn vị trung gian (BFS), chỉ quy đổi đang hiệu lực; không có → 422 UOM_CONVERSION_MISSING.
Kiểm tra: build 0 lỗi / 10 cảnh báo; test **103/103** (+5) phủ từng mục trên. SQL Server thật: danh sách khách / nguyên liệu (có trường tính toán), lọc effectiveOn, overview (404 khi không có), convert (422 khi thiếu quy đổi), users / org tree / meta me / menu đều chạy, log không lỗi.

### T45B.4 – API riêng của P2 (công thức, giá thành, chính sách giá) · 2026-11-14 · DONE
Đã làm (`AppDelicare.Application/Menu/Recipes.cs`, `WebApi/Controllers/V1/Menu/P2Controllers.cs`):
- **Phiên bản công thức** (không có bảng RecipeVersion – mỗi phiên bản là một Recipe cùng DishId): `GET /menu/dishes/{dishId}/recipes` (mới nhất trước, kèm dòng + chi phí / suất); `POST /menu/dishes/{dishId}/recipes` (VersionNo = max + 1, VersionCode = {mã món}-V{n}, Draft, sao chép dòng từ bản nguồn; chặn khi đã có bản Draft / PendingApproval → 409).
- **Dòng định lượng** chỉ khi Draft: `POST /menu/recipes/{id}/lines`, `PUT` / `DELETE /menu/recipe-lines/{id}` (quantity > 0, nguyên liệu / đơn vị phải tồn tại, ghi song song UomId).
- **Duyệt**: `submit` (≥ 1 dòng → PendingApproval + ApprovalRequest RECIPE_VERSION), `approve` / `reject` (quyền Approve – nhóm CEO, COO; từ chối bắt buộc lý do → về Draft) – GAP-P2-01.
- **Khóa** `POST /menu/recipes/{id}/lock {effectiveFrom}` – spec 5.2 bước 1–5: bản hiệu lực cũ → Obsolete (EffectiveTo = from − 1), bản này Locked / IsActive / LockedAt / LockedBy, SnapshotJson, StandardCost mới (cũ → Inactive), Dishe.StandardCost.
- **Giá thành** `GET /menu/recipes/{id}/cost?mealCount&on` – bước 6 qua `IRecipeCostCalculator` (giá NCC ưu tiên đang hiệu lực + UomService); dòng thiếu giá / thiếu quy đổi có `warning`, `complete = false`. `POST /production/standard-costs/recalculate` chạy lại bước 4–5 với giá hiện hành – GAP-P2-02.
- **Món**: danh sách thêm `activeRecipe` + `estimatedMaterialCost`.
- **Chính sách giá**: chặn chồng hiệu lực (422); `GET /sales/price-policies/resolve` (hợp đồng → khách → ContractMeal) – GAP-P2-03.
- CRUD chuẩn: convention **gỡ action không được phép** (resource chỉ đọc không còn POST / PUT / DELETE – Swagger đúng thực tế, không chiếm route của API nghiệp vụ như `PUT /menu/recipe-lines/{id}`).

Kiểm tra: build 0 lỗi / 10 cảnh báo; test **108/108** (+5): vòng đời đầy đủ V1 → khóa → V2 sao chép → khóa → V1 Obsolete (EffectiveTo đúng), chi phí có quy đổi g → kg (20 400 / suất), StandardCost Active / Inactive, Dishe.StandardCost, ApprovalRequest Approved, sửa dòng khi không Draft 409, khóa khi chưa duyệt 409; tính lại theo giá mới; thiếu giá → cảnh báo; tra đơn giá theo thứ tự + chồng hiệu lực 422 + nối tiếp hợp lệ; OPS-PLAN không duyệt được (403). **SQL Server thật** (transaction rồi rollback): tạo → dòng → gửi → duyệt → khóa, LockedAt / SnapshotJson / StandardCost 9 900 / UomId đúng, không để lại dữ liệu.

### CHORE – Đổi tên thư mục ClinicManagement.* → AppDelicare.* · 2026-11-14 · DONE
Theo yêu cầu của anh: `AppDelicare/ClinicManagement.{Model,Data,Services,WebApi}` → `AppDelicare/AppDelicare.{Model,Data,Services,WebApi}` (git mv, giữ lịch sử). Cập nhật đường dẫn trong `.sln`, các `.csproj`, Dockerfile, `deploy/*`, `CLAUDE.md`, `ARCHITECTURE.md`, `SECRETS_TODO.md`, `tools/AppDelicare.CodeGen`, `ClinicManagement.Generator/RunCopyFile.bat` (chỉ đích chép). Giữ nguyên `ClinicManagement.Generator`, `docs/reference/*` và các mục lịch sử cũ trong PROGRESS / CODE_AUDIT. Namespace, tên project, `UserSecretsId` không đổi.
Kiểm tra: build 0 lỗi / 10 cảnh báo; test **108/108**; CodeGen `--dry-run` ghi 0 file.

### T45R – Về khung code cũ, gỡ lớp v1 (D-30, D-31) · 2026-11-15 · DONE
Kế hoạch: 1) rà soát code cũ + kiểm kê v1 → `LEGACY_REVIEW.md` 2) hạ tầng: IClock / BusinessRules về tầng cũ, sửa transaction UnitOfWork, PermissionService sang Services, quyền duyệt = menu "Duyệt …" 3) quản trị P0 4) danh mục P1 5) công thức / giá P2 6) gỡ lớp v1 vào `_obsolete/v1` 7) viết lại test 8) tài liệu.

Rà soát (đã có – bổ sung gì – ở đâu): `docs/claude/LEGACY_REVIEW.md` (bảng màn hình P0–P2 và bảng endpoint v1 → action cũ).

Đã làm:
- **Hạ tầng (giữ trong khung cũ):**
  - `IClock` → `Data/Share/Clock.cs`, `BusinessRulesOptions` / `PermissionOptions` → `Model/Option`.
  - **Sửa `UnitOfWork.OpenTransaction` (CODE_AUDIT A21)**: trước đây EF không nằm trong transaction. Nay transaction mở trên kết nối DbContext, Dapper dùng chung, service lồng nhau tham gia transaction ngoài; InMemory bỏ qua.
  - `Result<T>.Success / Fail` (cùng hình dạng cũ).
  - `IPermissionService` / `PermissionService` → `AppDelicare.Services`; service User / UserClaim / UserGroup(UserRole) / ObjectMenu xóa cache quyền ngay khi ghi.
  - **Quyền duyệt (D-31)**: mục ObjectMenu "Duyệt – …" (`#/approve/<screen-id>`, 14 màn có loại AP) + UserRole.AllowEdit; bỏ `Permissions:ApproverGroups` và `ScreenMap.MatrixGroup`. Seed chỉ thêm phần thiếu.
- **P0**:
  - `UserRole/GetMatrix`, `Update` (tạo dòng thiếu, chuẩn hóa cờ), `SaveAccessAll`, `CopyFromUserGroup`, `GetPreview` (admin), `GetByUserId`.
  - `User/GetCurrent`; `SaveIsActive` chặn tự khóa; `ResetPassword` chỉ admin.
  - **Nhóm mới không còn toàn quyền** (A22, PROPOSED).
  - `ObjectMenu/GetTree`, `UpdateOrder` (chặn vòng lặp), MetaLink duy nhất.
  - `OrganizationUnit/GetTree`, `GetBranches`, chặn vòng lặp cha.
  - `CoreDataMergeConflict` controller / service / SP mới (Id bigint).
- **P1**:
  - Luật trong Insert / Update: Customer (CIF), CustomerDeliveryLocation (giờ giao, tọa độ, chặn ngừng khi còn đơn), Ingredient, SupplierMaterial (1 NCC ưu tiên), UomConversion, InventoryPolicy, Shift.
  - `Customer/GetOverview`, `Supplier/GetOverview`, `Warehouse/GetLayout`, `Ingredient/GetStock`, `UomConversion/Convert`, `InventoryTransaction/Reverse`.
- **P2**:
  - `Recipe/GetByDishId, CreateVersion, Submit, Approve, Reject, Lock, CalculateCost`; Insert luôn tạo Draft, Update / Delete chỉ khi Draft; RecipeLine chỉ sửa khi Draft, ghi song song UomId.
  - `StandardCost/Recalculate`, `Dish/GetActiveRecipe`.
  - `PricePolicy/Resolve` + đơn giá > 0 + chặn chồng hiệu lực; checklist (Code, VersionNo).
- **CodeGen**:
  - `--merge` bổ sung cột DDL còn thiếu vào Request / ViewModel cũ (A23, 144 file).
  - SP `GetAll` có `@EffectiveOn` cho bảng có hiệu lực (10 SP, đã cài lên DB dev).
  - Bỏ layer `crud`; không thêm kiểm trùng Code đơn lẻ khi DDL có UNIQUE chứa Code.
- **Gỡ v1 → `_obsolete/v1/`** (không xóa):
  - `Controllers/V1/**` và `Api/**` (giữ `ApiJson` – ẩn mật khẩu – chuyển `Infrastructure`).
  - Project `AppDelicare.Application` (đã gỡ khỏi sln / csproj), test v1, `CrudMetadataGen.cs`, `v1-hr-shifts.http`.
  - Startup bỏ envelope / idempotency / X-Branch-Id; Swagger nhóm theo controller.

Kiểm tra:
- Build: `dotnet build --no-incremental` 0 lỗi / 10 cảnh báo (= mốc).
- Test: **76/76**, gồm 3 test chạy SQL Server dev:
  - rollback transaction UnitOfWork thật sự hủy ghi EF;
  - vòng đời công thức trên DB thật rồi rollback;
  - chạy thử action mới dùng SP.
- CodeGen `--dry-run` ghi 0 file.
- Seed ObjectMenu / UserRole chạy trên DB dev: 14 mục duyệt; "Duyệt – Công thức" cấp cho CEO, COO.
- `grep api/v1` trong .cs = 0, ngoài `_obsolete` và 2 test kiểm tra sự vắng mặt.
- `git status` không có thay đổi trong `app-delicare-client/`.

AC: [x] LEGACY_REVIEW · [x] không route v1 · [x] đúng mẫu WarehouseController, không controller inject DbContext / UnitOfWork · [x] không service quản trị song song · [x] quyền không cấu hình appsettings / tên nhóm · [x] build + test xanh.

Còn lại / GAP:
- `.http` chưa chạy tay từng request.
- Update theo kiểu cũ là **ghi đè đủ trường** (AutoMapper): frontend phải gửi lại mọi trường, nếu không cột mới sẽ thành null. Ghi rõ trong T45B.5.
- Cột NOT NULL chưa có `[Required]` ở DTO cũ: thiếu thì trả 500 (theo hành vi cũ). Sẽ xem lại ở T45B.5.

Cần anh: duyệt 2 điểm PROPOSED (nhóm mới không có quyền; ResetPassword / GetPreview chỉ admin).

### T45B.5 – Bàn giao frontend P0–P2, trạng thái, danh mục tham chiếu · 2026-11-15 · DONE
Kế hoạch: 1) rà soát: chưa có action trạng thái / tham chiếu; STATUS_CATALOG (47 bộ), Gap Register sheet 08 (danh mục chưa có bảng) 2) CodeGen sinh StatusCatalog 3) MetaDataService / MetaDataController (mẫu cũ, không gắn bảng) 4) tài liệu bàn giao 5) test, .http, UAT.

Đã làm:
- CodeGen layer `status` → `Model/Helper/StatusCatalog.Generated.cs` (47 bộ, mã + nhãn tiếng Việt + nguồn).
- `MetaData/GetStatuses?key=`, `MetaData/GetReferenceLists?name=`; chỉ cần đăng nhập.
- Danh mục tham chiếu trong `appsettings.json` → `ReferenceLists`: 9 danh mục (MealType, WarehouseType, StorageType, WasteType, AdjustmentReason, ReturnInspectionResult, CustomerType, SupplierType, QcResult), đều PROPOSED.
  - **MealType theo D-10 / BUSINESS_RULES** (SANG / TRUA / TOI / DEM), không theo Gap Register (BREAKFAST / LUNCH…).
- `docs/frontend-handoff/P0-P2.md`:
  - quy ước (đăng nhập, Result, lỗi, danh sách / form, ghi đè đủ trường, quyền, duyệt);
  - 265 action theo màn hình, sinh từ code controller.
- `.http` r1.1 có thêm MetaData; UAT_CHECKLIST thêm R1.1 (13 kịch bản), sửa R1.0 theo route cũ.

Kiểm tra: build 0 lỗi / 10 cảnh báo; test **79/79** (+3: 47 bộ trạng thái và lọc key, danh mục từ cấu hình, user không quyền vẫn đọc được, chưa đăng nhập 401).

AC: [x] bảng đối chiếu màn hình → action · [x] test · [x] .http.

Còn lại / GAP: GAP-AUTH-01 (refresh token) vẫn mở. Danh mục tham chiếu và MealType chờ BA/PO chốt.

### T49 – Nạp dữ liệu danh mục ban đầu (Excel) · 2026-11-15 · DONE
Kế hoạch:
1. Rà soát: `ImportExcelController.Save` / `ImportService` cũ chỉ nạp TestDemo (sheet 1, tiêu đề = tên thuộc tính, không báo lỗi từng dòng, lưu file vào wwwroot). Đọc cấu trúc file thật: chỉ dòng tiêu đề, không chép dữ liệu.
2. `MasterDataImportService` + action trên `ImportExcelController`.
3. File mẫu từng danh mục.
4. Nạp file thật.
5. Test (InMemory + SQL Server).
6. Tài liệu.

Đã làm:
- `ImportExcel/GetTemplateList`, `GetTemplate?tableName=` (xlsx: tiêu đề tiếng Việt, cột * bắt buộc, ghi chú, 1 dòng ví dụ [DEMO]), `Import` (multipart).
- Quyền: cờ **Import** trên màn hình đích, theo UserRole (D-31).
- **11 file mẫu**: Customer, CustomerDeliveryLocation, Supplier, UnitMeasure, Ingredient, Warehouse, Employee, Vehicle, Dish, RecipeLine (định mức), SupplierMaterial (bảng giá NCC).
- **3 bố cục file thật**: Rule Menu – Ngân hàng món; Rule Menu – Chi tiết món và hao hụt, giá; Tổng hợp báo giá – Summary.
- Cách nạp:
  - Idempotent theo mã; món / nhóm từ file thật nhận diện theo tên, mã sinh không dấu.
  - Nhóm NL, nhóm món, ĐVT chưa có thì tạo.
  - Định mức tạo công thức Draft V1 nếu món chưa có công thức.
  - Ghi song song cột cũ (BaseUomId, CategoryId, EmployeeCode, UomId).
  - Kiểm luật như service (hao hụt, hạn dùng, giờ giao, tọa độ, CIF, đơn giá > 0, 1 NCC ưu tiên); kiểm độ dài theo [MaxLength].
  - Dòng lỗi bỏ qua và báo số dòng + lý do; dòng hợp lệ ghi trong 1 transaction.
  - File đọc từ stream, không lưu trên server.
- CIF trống → dùng mã khách (cột NOT NULL) – PROPOSED.

Kiểm tra:
- Build 0 lỗi / 10 cảnh báo.
- Test **85/85** (+6):
  - tải mẫu;
  - lỗi từng dòng + nạp lại không trùng;
  - chuỗi NL → món → định mức → bảng giá (NCC ưu tiên);
  - 3 bố cục file thật với dữ liệu [DEMO] tự tạo;
  - sai bố cục, sai loại, không quyền → 403;
  - **SQL Server**: dòng ví dụ của cả 11 mẫu nạp được vào DB thật (transaction + rollback).
- Không nạp dữ liệu thật; file tạm đã xóa.

AC: [x] nạp file mẫu thành công · [x] nạp lại không trùng · [x] dòng lỗi được báo rõ.

Còn lại / GAP:
- **GAP-IMP-01**: "Chi tiết món và hao hụt, giá" không có cột **định lượng** → không tạo dòng định mức. Phải nhập bằng mẫu RecipeLine. % hao hụt theo từng món (cột G/H) và giá cột O chưa nạp.
- **GAP-IMP-02**: SupplierMaterial không có cột VAT → % VAT trong báo giá không nạp.
- Sheet "Rule" (quy tắc thực đơn) và "DS NCC" (địa chỉ, điện thoại NCC) chưa nạp: để T13 / R1.2.

Cần anh: tự nạp file thật (UAT R1.1 mục 15); chốt cách xử lý GAP-IMP-01, ví dụ bổ sung cột định lượng vào file, hoặc lấy định lượng mặc định từ sheet Rule.

### Deploy staging sau lát R1.1 · 2026-11-15 · DONE
- `deploy/db/Deploy-Database.ps1` lên `AppDelicare_Staging` (LocalDB): giữ nguyên 124 bảng; 130 SP; seed tổ chức / menu (gồm 14 mục "Duyệt …") / nhóm quyền / ca.
- Publish Release, chạy ở chế độ Staging:
  - `/health` Healthy;
  - gọi không token → 401;
  - Swagger 747 route, 0 route `/api/v1`, có ImportExcel/Import, Recipe/Lock, MetaData.
- **Lỗi phát hiện khi deploy và đã sửa:** `CodeGen --merge` so tên phân biệt hoa thường, nên thêm `QCInspectionId` vào DTO đã có `QcInspectionId` (trùng tên camelCase → Swagger JSON lỗi).
  - Đã gỡ thuộc tính trùng; Merger so tên không phân biệt hoa thường.
  - Thêm test `DtoNamingTests`.
- Test 86/86.
- **Dừng lát R1.1 – chờ anh UAT-1** theo `docs/claude/UAT_CHECKLIST.md` mục R1.0 + R1.1 (`deploy/README.md` mục 3 để chạy staging).

### T45R.6 – Đổi tên bộ sinh T4 thành AppDelicare.Generator · 2026-11-15 · DONE (chờ anh chạy T4)
Nhánh `chore/T45R.6-rename-generator` (từ `feat/T45R-legacy-framework`).

Đã làm:
- `git mv` 20 file (giữ lịch sử): `AppDelicare/ClinicManagement.Generator` → `AppDelicare/AppDelicare.Generator`; 14 thư mục con `ClinicManagement.*` → `AppDelicare.*`.
  - Danh sách thư mục con: Data.Connection, Data.Interface, Data.Interface.UnitOfWork, Data.Repositories, Data.Repositories.UnitOfWork, Model.Entity, Model.Request, Model.SearchRequest, Model.ViewModels, Services.Implement, Services.Interface, WebApi.Controllers, WebApi.Infrastructure.
- Sửa tham chiếu:
  - `AppDelicare.sln`;
  - `AppDelicare.Generator.csproj` (RootNamespace, AssemblyName, Compile Include);
  - `Properties/AssemblyInfo.cs`;
  - `RunCopyFile.bat` (mọi dòng copy / del, giữ CRLF);
  - 14 `.tt`: Namespace và using `ClinicManagement.*` → `AppDelicare.*`, khớp namespace thật;
  - `OrmLite.Core.ttinclude`: `DefaultClinicManagementspace` → `DefaultNamespace` (dấu vết lần thay chữ "Name" cũ, chỉ 2 chỗ: định nghĩa + override);
  - `.dockerignore`, `CLAUDE.md` mục 2, `tools/AppDelicare.CodeGen/README.md`, `CODE_AUDIT` (A1, A2, A12).
- Không chạy T4, không sinh lại code, không đổi nội dung template ngoài tên / namespace.

Kiểm tra:
- Build 0 lỗi / 10 cảnh báo (= mốc); test 86/86.
- `RunCopyFile.bat`: 24 / 24 đường dẫn khớp thư mục thật (14 thư mục nguồn T4 + 10 thư mục đích).
- grep "ClinicManagement" trong AppDelicare, tools, docs/claude, CLAUDE.md = 0. Ngoại lệ:
  - log cũ (không theo dõi);
  - mô tả task T45R.6 trong ROADMAP;
  - D-32 trong DECISIONS;
  - câu lịch sử lần đổi tên 2026-11-14 trong CLAUDE.md.

Cần anh: mở solution bằng Visual Studio, chạy T4 một lần trên nhánh này, xác nhận file sinh ra đúng chỗ rồi mới merge.

### Theo CLAUDE.md 4.3 (anh bổ sung 2026-11-15): seed không ghi đè, MetaLink mục duyệt · DONE
Nhánh `fix/T45R-seed-approval-link` (từ `chore/T45R.6-rename-generator`).
- **Seed chỉ thêm phần chưa có**:
  - `ObjectMenu_seed.sql` bỏ `ELSE UPDATE` tên / thứ tự / cha;
  - `UserRole_seed.sql` bỏ khối `UPDATE ur` (trước đây chạy lại seed sẽ đưa quyền về ma trận đề xuất, ghi đè chỉnh sửa của admin);
  - `OrganizationUnit_seed.sql` giữ nguyên (ngoài phạm vi 4.3).
- **MetaLink mục duyệt** = hộp duyệt lọc theo loại: `#/wf-inbox?type=<screen-id>` (thay `#/approve/<screen-id>`).
  - `wf-inbox` là màn "Hộp duyệt" trong catalog.js. Vẫn 14 mục, 1 mục / màn có loại AP (PROPOSED – anh có thể gộp theo nhóm khi UAT-1).
  - Seed đổi MetaLink kiểu cũ tại chỗ, giữ dòng và quyền đã gán. Đã chạy trên DB dev và staging: 0 mục cũ, 14 mục mới; "Duyệt – Công thức" vẫn của CEO, COO.
- Test 87/87 (+1 SQL Server `Seeds_do_not_overwrite_admin_changes`: thu hồi quyền duyệt của CEO + đổi tên menu → chạy lại seed → giữ nguyên; rollback).

### T48.0 – Băm mật khẩu an toàn (D-29) · 2026-11-16 · DONE
Nhánh `feat/T48.0-password-hash` (từ `main`).

Rà soát code cũ:
- **Đăng nhập** so `MD5(mật khẩu)` trong SQL.
- **Tạo user / Reset** lưu `MD5(tên đăng nhập)`, tức mật khẩu khởi tạo = tên đăng nhập.
- **Lỗi cũ ở ChangePassword**: kiểm và lưu `MD5(mật khẩu + tên)`, nên sau khi đổi mật khẩu user **không đăng nhập lại được**. Ngoài ra đòi độ dài ≥ 9 trong khi thông báo ghi "từ 8 ký tự".

Đã làm (chỉ `EncriptFunctionsHelper`, `LoginService`, `UserService`, script deploy – không đổi API, bảng, cột):
- `GeneratePassword` = PBKDF2-SHA256, salt ngẫu nhiên 16 byte, 100.000 vòng, chuỗi `PBKDF2$100000$<salt>$<hash>` trong `User.PassWord` (nvarchar(max)).
- `VerifyPassword(mật khẩu, chuỗi lưu, tên, out needsUpgrade)`:
  - PBKDF2: so sánh bằng `FixedTimeEquals`;
  - MD5 cũ: chấp nhận `MD5(mk)` và `MD5(mk + tên)`, kèm cờ cần nâng.
- Đăng nhập: tìm theo tên rồi kiểm; hash cũ đúng → ghi PBKDF2 ngay; `GetClaimsIdentity` kiểm cùng cách. Không log mật khẩu / hash.
- Tạo / Reset / InsertUserAfter tự ghi PBKDF2 qua `GeneratePassword`.
- ChangePassword: kiểm mật khẩu cũ bằng `VerifyPassword`, lưu PBKDF2 của mật khẩu mới (sửa lỗi không đăng nhập lại được); tối thiểu 8 ký tự đúng thông báo.
- `Deploy-Database.ps1` tạo admin bằng PBKDF2 cùng định dạng (PowerShell 5.1); `00_Admin_seed.sql` sửa chú thích.

Kiểm tra:
- Build 0 lỗi / 10 cảnh báo.
- Test **92/92** (+5 `PasswordHashTests`):
  - MD5 cũ đăng nhập → nâng PBKDF2 → đăng nhập lần 2;
  - hash `MD5(mk + tên)` đăng nhập được;
  - sai mật khẩu bị từ chối (cũ / mới);
  - 2 user cùng mật khẩu có hash khác nhau;
  - hash do PowerShell sinh kiểm được bằng C#;
  - tạo / đổi / reset ghi PBKDF2, đổi xong đăng nhập bằng mật khẩu mới, < 8 ký tự bị từ chối.
- Trên SQL Server dev: admin đã tự nâng lên PBKDF2 sau lần đăng nhập của test tích hợp.

AC: [x] cả 4 mục.

Còn lại / cần anh:
- **GAP-ADM-02 chưa đủ**: reset vẫn đặt mật khẩu = tên đăng nhập và trả `Result<bool>` (giữ API theo T48.0). Muốn "mật khẩu tạm 12 ký tự trả một lần" thì phải đổi response của `User/ResetPassword` → cần anh duyệt.
- Mỗi lần đăng nhập tính PBKDF2 2–3 lần (~0,1–0,2 s), chấp nhận cho Pilot.

### FE-0 – Khung ứng dụng Angular (D-35, D-36) · 2026-11-16 · DONE (chờ thử tay)
Nhánh `feat/FE-0-app-shell` (từ `feat/T48.0-password-hash`). Chỉ sửa trong `app-delicare-client/`.

Mốc ban đầu: `npm run build` của bản cũ **lỗi sẵn 5 lỗi** ở `main/medical-examination`. Không sửa file đó; gỡ route nên module không còn được build.

Đã làm:
- MetaLink `#/<screen-id>` (cả `?type=`) xử lý một chỗ trong `helpers/common-helper.ts` (`menuPath`, `menuLink`, `findMenuByUrl`); `permission()` / `checkPermission()` dùng chung.
- Đăng nhập, phiên:
  - guard gọi `api/User/GetCurrent` khi tải lại trang;
  - sửa lỗi cũ "chưa đăng nhập vẫn qua guard" (`userValue` là `{}`);
  - 401 → về đăng nhập kèm `returnUrl`; 403 → màn hình tự báo.
- Route theo screen-id: `adm-user`, `adm-usergroup`, `adm-objectmenu`, `adm-person`, `adm-access-log`, `adm-audit`, `scm-supplier`, `scm-uom`. Đường dẫn cũ chuyển hướng sang các route này; mục menu chưa có màn hình → "Chức năng chưa có".
- Gỡ khỏi route (không xóa file): demo w3crm (`pages`, `components`, `plugins`), `patient*`, `medical-*`, `test-demo`, `delete-data` (D-26).
- Sidebar dựng từ `listMenu` (nhóm cấp 1 → màn cấp 2); ô chọn chi nhánh `api/OrganizationUnit/GetBranches`; bỏ nút sáng / tối.
- Tên DELI-CARE + tên pháp nhân (tab, footer, đăng nhập); không còn chữ "phòng khám" / "W3CRM" trên giao diện.
- Giao diện D-36 trong `assets/css/clinic.css` (không sửa `style.css`): màu, font Be Vietnam Pro (Google Fonts), cỡ 14 / 13 / 12px, dòng ≥ 44px, nút ≥ 36px.
- Thành phần dùng chung: `shared/page-header`, `shared/status-badge`, `shared/detail-drawer`; `services/meta-data.service.ts` (cache GetStatuses / GetReferenceLists).
- Màn mẫu `scm-uom` (unit-measure) theo khung mới.
- Đổi mật khẩu sửa 3 lỗi: báo thành công cả khi lỗi; đòi ≥ 9 ký tự; gửi sai trường.

Kiểm tra: `npm run build` **0 lỗi**, 1 cảnh báo có sẵn (`<link style.css>` trong index.html). **Chưa thử trên trình duyệt, chưa chụp ảnh trước / sau** (không có công cụ trình duyệt trong phiên này).

Cần anh / còn lại:
- Thử tay theo UAT (đăng nhập, tải lại, đổi mật khẩu, 401, sidebar, chi nhánh, scm-uom).
- DB dev còn **26 mục ObjectMenu của app cũ** với MetaLink kiểu cũ (`quan-ly-dang-nhap`, `quan-ly-vung`…) – vẫn hiện trên menu nếu nhóm có quyền. Em không tự tắt (dữ liệu của anh): anh chọn tắt (IsActive = 0) hay giữ.
- Font đang tải từ Google Fonts; muốn chạy nội bộ không mạng thì cần file woff2.
- Màn `administrative-*`, `orgchart` còn route cũ, ngoài menu: gỡ hay giữ?
- Chi nhánh mới lưu ở trình duyệt; màn nào cần lọc theo chi nhánh sẽ đọc giá trị này.
- Màn `scm-uom` mới đổi giao diện; form vẫn chỉ có trường Tên (Update ghi đè đủ trường → FE-2 bổ sung).

### FE-1 – Màn quản trị P0 · 2026-11-16 · DONE (chờ thử tay)
Nhánh `feat/FE-1-admin` (từ FE-0). Chỉ sửa trong `app-delicare-client/`.

Đã làm:
- Màn `adm-user` (rà soát màn cũ `main/user`): lọc, nhóm, khóa / mở, reset (chỉ admin), drawer "Quyền hiệu lực". Sửa lỗi cũ: user mới mặc định là admin.
- Màn `adm-usergroup`: popup phân quyền cũ thay bằng link sang `adm-permission`.
- Màn `adm-permission` (mới) – ma trận 7 cờ:
  - lưu theo dòng đã đổi qua `UserRole/Update`;
  - Cấp / Thu hồi tất cả, Sao chép từ nhóm, Xem trước (admin);
  - dòng duyệt `#/wf-inbox?type=` gắn nhãn "Duyệt".
- Màn `adm-objectmenu`: đổi sang dạng cây; kéo thả bằng CDK có sẵn + nút lên / xuống, lưu qua `UpdateOrder`.
- Màn `adm-orgunit` (mới, cây `GetTree`).
- Màn `adm-employee`, `adm-position` (mới).
- Màn `adm-person` đổi giao diện.
- Helper `toFormData` gửi đủ trường khi Update (Update ghi đè đủ trường).
- Service: user-role (5 action mới), object-menu (cây, sắp xếp), organization-unit, employee, position, company.

Kiểm tra: `npm run build` 0 lỗi (1 cảnh báo có sẵn). Kịch bản thử tay: UAT_CHECKLIST mục PILOT-FE (chưa thử trên trình duyệt).

Phát hiện → việc backend nhỏ (làm sau T13, ghi ở đây):
- `UserRole/SaveAccessAll` chỉ cấp menu cấp 2 → sidebar ẩn nhóm cấp 1. FE tạm gửi thêm Update bật nhóm.
- `User/GetAll` chưa có lọc IsActive (SP / SearchRequest).
- `ObjectMenu` cho đặt cha cho menu cấp 1 đang có con (thành 3 cấp) – cần chặn ở `UpdateOrder` / Update.

Cần anh:
- **Tạo user kèm hồ sơ trong 1 bước chưa làm**: `Person/Insert` đã tự tạo 1 user (tên tự sinh, chưa có nhóm), nên làm thêm sẽ ra 2 tài khoản. Hiện màn user chỉ chọn hồ sơ có sẵn – anh chọn cách xử lý.
- Danh sách loại đơn vị / trạng thái đơn vị lấy theo seed (PROPOSED), chưa có trong STATUS_CATALOG.
### T11 – Mã chứng từ (Pilot P1) · 2026-11-16 · DONE
Nhánh `feat/T11-document-number` (từ `feat/T48.0-password-hash`, làm trong worktree riêng song song FE-0).

Rà soát: chưa có service sinh mã; `RecipeService.Submit` tự đặt `RV-yyMMdd-HHmmssfff` (có thể trùng khi bấm đồng thời, sai mẫu XN).

Đã làm:
- `IDocumentNumberService.NextAsync(kind, date, suffix)` (`AppDelicare.Services`, Autofac tự đăng ký), đủ 10 loại BUSINESS_RULES §11:
  - PH (đơn, nhập, xuất, kiểm kê, đề nghị mua, lệnh SX, đóng gói, giao – dùng chung, không trùng giữa các bảng – PROPOSED);
  - XN, PA, PO, DN;
  - NL / ME / CX: hậu tố NCC / ca / xe, bắt buộc;
  - KH: `KH-YYMMDD`, phiên bản sau `-2`, `-3`;
  - VS: `VS-YYMMDD-<CA>`, lần sau `-2`.
- SQL Server:
  - `sp_getapplock` (Exclusive, LockOwner = Transaction) theo mã gốc;
  - `MAX(TRY_CAST(phần số))` gộp cột chuẩn + cột cũ (DEPRECATED_COLUMNS);
  - tên bảng / cột lấy từ metadata EF, giá trị là tham số, LIKE có escape;
  - bắt buộc gọi trong transaction đang mở (sau T45R, `UnitOfWork.OpenTransaction` đã bao EF).
- Không bảng đếm, không SEQUENCE.
- Hậu tố: bỏ dấu, chữ hoa, chỉ A–Z 0–9, tối đa 20 ký tự.
- `RecipeService.Submit` dùng mã `XN-` cho ApprovalRequest.RequestNo.

Kiểm tra:
- Build 0 lỗi.
- Test **105/105** (+13):
  - định dạng 10 loại;
  - tăng tiếp theo dữ liệu có sẵn (cả cột cũ `ReceiptNo`, PH chung nhiều bảng), đếm riêng theo hậu tố và theo ngày;
  - hậu tố bắt buộc;
  - **SQL Server: 50 transaction song song → 50 mã khác nhau, liên tục XN-991231-001..050** (dùng ngày 2099, xóa đúng 50 dòng test theo Id).

AC: [x] 50 lần song song không trùng · [x] đúng định dạng mọi loại.

GAP:
- `AttendanceEvent` không có cột mã → loại CC chưa có chỗ ghi (ngoài Pilot).
- Bảng có cột cũ NOT NULL (vd. `MaterialLot.LotNo`) → nơi tạo bản ghi phải ghi song song cả cột cũ (T15).

### T12 – Duyệt 1 cấp (Pilot P2) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T12-approval` (từ `feat/T11-document-number`, worktree riêng).

Rà soát:
- `ApprovalRequestService` / `Controller` chỉ có CRUD sinh mã – CRUD sửa được cả `RequestStatus` (duyệt "chui").
- Duyệt công thức tự cập nhật ApprovalRequest, không ghi ApprovalDecision.
- `ApprovalDecision.ApprovalStepId` NOT NULL nên cần ApprovalStep.

Đã làm (trong `ApprovalRequestService` cũ, gỡ header auto-generated):
- `IApprovalHandler` – registry theo BusinessType: BuildPayload / Describe / Approve / Reject; đăng ký trong `Ioc/ComponentRegistrar`; lấy handler lúc dùng qua IServiceProvider để tránh vòng phụ thuộc.
- `CreateAsync(businessType, businessId)`: gọi trong transaction của nghiệp vụ; mã `XN-` (T11) + ApprovalStep bước 1 + PayloadJson; chặn 2 yêu cầu Pending cùng chứng từ.
- `GetMyPending(type)`: chỉ loại người đăng nhập có quyền duyệt (mục `#/wf-inbox?type=<screen>`, D-31; admin thấy hết); kèm tiêu đề, người gửi.
- `Decide`, trong 1 transaction:
  - kiểm quyền duyệt;
  - từ chối bắt buộc lý do;
  - **so hash nội dung lúc gửi với nội dung hiện tại – khác thì không duyệt, không ghi gì**;
  - ghi ApprovalDecision (chỉ ghi thêm, PayloadHash SHA-256), cập nhật bước + yêu cầu;
  - thực thi handler (lỗi → rollback toàn bộ).
- CRUD `ApprovalRequest/Update` không đổi được trạng thái / nghiệp vụ / nội dung / mã.
- Action: `ApprovalRequest/GetMyPending`, `ApprovalRequest/Decide` (chỉ cần đăng nhập, service tự kiểm quyền).
- Công thức (RECIPE_VERSION, `RecipeApprovalHandler`): `Recipe/Submit` tạo yêu cầu qua engine; `Recipe/Approve` / `Reject` gọi `Decide`. Hành vi cũ giữ nguyên, nay có ApprovalDecision.

Kiểm tra:
- Build 0 lỗi.
- Test **110/110** (+5 ApprovalTests):
  - gửi → mã XN + bước + người có quyền thấy, người không quyền không thấy / không duyệt được;
  - duyệt → thực thi + ApprovalDecision;
  - từ chối cần lý do và không thực thi;
  - nội dung bị sửa → không duyệt, không ghi;
  - CRUD không đổi được trạng thái; Recipe/Approve đi qua engine.
- RecipeServiceTests cũ vẫn xanh.

AC (bản Pilot): [x] tạo → duyệt → thực thi · [x] từ chối không thực thi · [x] payload bị sửa thì không duyệt.

DEFERRED(PILOT): duyệt nhiều cấp theo ApproverRoleCode, hết hạn tự động (job T23).

Handler PO và đơn lệch > 30% làm cùng T15 / T13.

**Chưa cập nhật `DELICARE_API_Catalog.xlsx` (sheet API_Catalog)** cho T11 / T12 và các task trước: máy chưa có `openpyxl`, lệnh cài bị chặn – cần anh cho phép cài hoặc tự cập nhật.

### T13 + T46B.1 – Thực đơn tuần + Đơn hàng (Pilot P3) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T13-order-menu` (worktree `wt-T11`, từ T12).

Kế hoạch:
1. Rà soát Menu / MenuLine / MenuAssignment / Order / OrderLine / Contract / ContractMeal → LEGACY_REVIEW mục (c).
2. Mở rộng engine duyệt T12: đề xuất (proposal) đi kèm yêu cầu duyệt, tương thích ngược.
3. Thực đơn tuần: lịch + giá vốn + FC%, luật dòng thực đơn, Approve / Publish / Expire / Clone.
4. Phân công thực đơn: kiểm chồng hiệu lực, Resolve.
5. Đơn hàng: mẫu tạo đơn, Create, UpdateLines, máy trạng thái, giờ chốt 20h D-1, lệch suất > 30% → duyệt.
6. In "Thực đơn gửi khách" (.xlsx).
7. Test (InMemory + SQL Server rollback), tài liệu bàn giao, `.http`.

Rà soát: 7 bảng đều có CRUD sinh mã, chưa có nghiệp vụ; CRUD Menu / Order đổi được trạng thái, số, tổng tiền; OrderLine CRUD không tính lại tiền. `MenuLine` không có cột lựa chọn (GAP-MENU-01), `OrderLine` không có cột điểm giao (GAP-ORD-01). Chi tiết: LEGACY_REVIEW (c).

Đã làm (đều trong file cũ, đúng mẫu 4.2):
- **Engine duyệt (T12 mở rộng):** `IApprovalRequestService.CreateAsync(type, id, proposalJson = null)`; có đề xuất → `PayloadJson = {"state":"<nội dung>","proposal":"<đề xuất>"}` (lưu dạng chuỗi để hash đúng byte), hash chỉ trên state; không có đề xuất → định dạng T12. `IApprovalHandler.ApproveAsync(…, proposalJson)` là default method gọi bản cũ → `RecipeApprovalHandler` không phải sửa. Test T12 giữ xanh.
- **Thực đơn (`MenuService` / `MenuController`, `MenuLineService`):** `GetCalendar` (hàng = loại suất × lựa chọn × nhóm món, cột thứ 1–7, giá vốn / suất từng món theo công thức khóa, tổng + FC% theo (loại suất, lựa chọn, thứ), giá bán nhập tay hoặc tra theo khách); `Approve` (quyền duyệt `menu-menu`), `Publish` (đủ món mỗi thứ trong hiệu lực × loại suất đã gán), `Expire`, `Clone`; Insert luôn Draft, Update / Delete theo trạng thái; dòng thực đơn: thứ 1–7, lựa chọn 1–9, chỉ khi Draft / Approved, món chưa có công thức khóa → cảnh báo không chặn.
- **Phân công (`MenuAssignmentService` / `Controller`):** luật Insert / Update (hợp đồng thuộc khách + có loại suất, điểm giao thuộc khách, không chồng hiệu lực); `Resolve` (điểm giao → khách; chỉ thực đơn Approved / Published).
- **Đơn hàng (`OrderService` / `OrderController`):** `GetNewTemplate`, `GetDetail` (allowedActions, cutoffAt), `Create` (JSON – có dòng lồng nhau; 1 transaction; số `PH-YYMMDD-NNNN` qua `IDocumentNumberService.Voucher` – theo BUSINESS_RULES §11, không dùng `DH-` của spec; ghi song song OrderNo / OrderNumber, Amount / LineAmount; MenuId qua Resolve; UnitPrice qua `PricePolicyService.Resolve`; ca mặc định `BusinessRules:MealTypeDefaultShift`; bỏ dòng 0; chặn trùng đơn khách / điểm / ngày), `UpdateLines` (JSON), `Submit`, `Confirm`, `Lock`, `LockByMealDate` (nút thay job 20h), `Cancel` (lý do bắt buộc). CRUD Insert chuyển sang Create; Update chỉ Note / DeliveryDate đơn Draft; Delete chỉ Draft (kèm dòng); OrderLine CRUD ghi → lỗi nghiệp vụ.
- **Giờ chốt (PROPOSED, GAP-ORD-02):** ngày ăn D chốt lúc 20:00 ngày D-1 (giờ VN qua `IClock`, giờ từ `BusinessRulesOptions`). Từ giờ chốt: chặn Create / UpdateLines / Submit / Confirm / Cancel đơn Confirmed; cho Lock, Cancel đơn Draft / Submitted. `LockByMealDate` chỉ chạy khi đã tới giờ chốt, khóa đơn Confirmed, trả danh sách đơn chưa xác nhận + đơn còn yêu cầu lệch suất chờ duyệt.
- **Lệch suất (PROPOSED, GAP-ORD-03):** đơn Confirmed, dòng bất kỳ lệch (mới − cũ) / cũ (trị tuyệt đối) > 30% (`PortionDeviationPctNeedApproval`) → không ghi, tạo ApprovalRequest `ORDER_DEVIATION` (handler `OrderDeviationApprovalHandler`, màn `crm-order`) kèm đề xuất; duyệt → ghi số mới + tính lại tiền; từ chối → không đổi (ghi chú lý do vào đơn); đơn bị chốt / sửa sau khi gửi → không duyệt được (hash). Đúng 30% ghi ngay. Khi đang chờ duyệt không sửa tiếp được.
- **In "Thực đơn gửi khách" (T46B.1):** `Menu/ExportForCustomer` (.xlsx, ClosedXML) theo bố cục sheet "4. Menu_Sent" của file thật (chỉ lấy cấu trúc, không chép dữ liệu): tên / địa chỉ pháp nhân từ bảng `Company` (Menu.CompanyId, không có thì công ty đầu tiên), tiêu đề, dự án, hiệu lực, bảng Thứ × buổi (Món + Trọng lượng thành phẩm = `Dish.StandardServingSize`); nhiều lựa chọn → tiền tố "LC n:".
- **Quyền:** theo `PermissionConvention` (Get* = View, Create / Clone = Add, ExportForCustomer = Export, còn lại Edit). Khai báo rõ: `Menu/Approve` = Approve (`menu-menu`), `Menu/Publish`, `Expire` = Edit, `Menu/Clone` = Add, `Order/Confirm`, `Lock`, `LockByMealDate`, `Cancel` = Edit trên `crm-order` (thao tác vận hành, không phải duyệt – PROPOSED). Duyệt lệch suất = mục menu mới `#/wf-inbox?type=crm-order` "Duyệt – Đơn đặt suất ăn" (CodeGen `Seeds.ExtraApprovable`, sinh lại `ObjectMenu_seed.sql` / `UserRole_seed.sql`: chỉ thêm, nhóm CEO theo ma trận).
- Thông báo tiếng Việt mới trong `ConstantHelper.Message`; trạng thái trong `OrderStatus` / `MenuStatus` (`Model/Helper/OrderStatus.cs`).
- Test: `TestClock` (IClock điều khiển được, mặc định giờ thật) đăng ký trong `InMemoryApiFactory`.

File chính: `Services/Implement/{OrderService, MenuService, MenuLineService, MenuAssignmentService, OrderLineService, ApprovalRequestService}.cs`, `Services/Implement/Approval/OrderDeviationApprovalHandler.cs`, `Services/Interface/{IOrderService, IMenuService, IMenuAssignmentService, IApprovalHandler, IApprovalRequestService}.cs`, `WebApi/Controllers/{Order, Menu, MenuAssignment}Controller.cs`, `Model/Request/Custom/{Order, Menu}CustomRequest.cs`, `Model/Model/{Order, Menu}Models.cs`, `Model/Helper/OrderStatus.cs`, `Model/Option/BusinessRulesOptions.cs` + `appsettings.json` (`MealTypeDefaultShift`), `tools/AppDelicare.CodeGen/Seeds.cs`, `DB/seed/{ObjectMenu, UserRole}_seed.sql`, tài liệu `docs/frontend-handoff/P3.md`, `docs/api-tests/p3-menu-order.http`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline worktree: 10 + 1 MSB3245 Generator).
- `dotnet test`: **123/123** xanh (+13): `Sales/OrderTests` (8: mẫu → tạo → gửi → xác nhận → chốt, số PH-, ghi song song cột cũ, tổng tiền; chuyển trạng thái sai; giờ chốt trước / đúng 20:00 D-1 + LockByMealDate; lệch ≤ 30% ghi ngay, > 30% → duyệt / từ chối; duyệt sau khi đơn bị chốt bị chặn; thứ tự tra giá hợp đồng → khách → ContractMeal; không có hợp đồng; tương thích ngược PayloadJson), `Menu/MenuCalendarTests` (4: lịch + giá vốn + FC%, cảnh báo món chưa có công thức, thứ ngoài 1–7; Approve / Publish / Expire / Clone; phân công + Resolve + chồng hiệu lực; file in mở được bằng ClosedXML), `Sales/OrderSqlServerTests` (1: SQL Server dev, create → submit → confirm → lock trong transaction ngoài rồi rollback – đã chạy, không bị bỏ qua).
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`.

AC:
- [x] T13: test các nhánh cutoff và deviation; số tổng của đơn đúng; Submit / Confirm / Cancel (+ Lock) theo máy trạng thái; giá theo PricePolicie / ContractMeal.
- [x] T46B.1 (Pilot): thực đơn tuần buổi × lựa chọn + giá vốn / FC%; đơn theo khách → điểm giao; mẫu in Thực đơn gửi khách có test sinh file mở được.
- [ ] `DELICARE_API_Catalog.xlsx` chưa cập nhật (máy chưa có `openpyxl`) – các dòng spec 6.2, 6.3, 6.6 cần ghi "Đã xong" / "Một phần" + action thực tế (xem `docs/frontend-handoff/P3.md`).

DEFERRED(PILOT):
- Job 20:00 tự khóa đơn (dùng nút `Order/LockByMealDate`), job hết hạn hợp đồng.
- Sửa đơn sau giờ chốt qua duyệt `ORDER_CHANGE_AFTER_CUTOFF`; lệch suất so với trung bình 4 tuần cùng thứ của điểm giao.
- Dự báo (Forecast): số lượng gợi ý trong mẫu tạo đơn (= 0), lệch dự báo > 10% khi xác nhận; danh sách dạng kanban (`view=board`, `statusCounts`), `orderedMeals` / `assignedCustomerCount` trong danh sách thực đơn.
- Hợp đồng: Create kèm suất, Submit duyệt, Terminate (spec 6.1) – Pilot dùng CRUD Contract / ContractMeal, hợp đồng phải `Status = Active`.
- Yêu cầu đổi món (MenuChangeRequest, spec 6.4), đối soát (6.7 – T19).
- Đẩy thực đơn đã công bố qua Zalo cho khách.
- In thực đơn: logo (chưa có file), bản PDF.

Câu hỏi cho anh (DECISIONS GAP-MENU-01, GAP-MENU-02, GAP-ORD-01…03):
1. "Lựa chọn 1/2/3" lưu ở `MenuLine.SequenceNo` được không? Số suất dự kiến theo lựa chọn có cần lưu (chưa có cột)?
2. 1 đơn = 1 điểm giao (OrderLine không có cột điểm giao) – anh đồng ý?
3. Sau 20:00 D-1: chặn tạo / sửa / gửi / xác nhận / hủy đơn đã xác nhận; vẫn cho chốt và hủy đơn chưa xác nhận – đúng ý anh? Đơn chưa xác nhận lúc chốt thì xử lý thế nào (hiện chỉ báo danh sách)?
4. Lệch suất Pilot = so với số đã xác nhận (không phải trung bình 4 tuần) – tạm được không? Ai duyệt (seed hiện chỉ CEO theo ma trận; COO có cần)?
5. FC% tính trên giá bán chưa VAT từ chính sách giá / ContractMeal; `PortionQty` của dòng thực đơn có nhân vào giá vốn không?
6. Xác nhận đơn (Confirm) và Chốt (Lock) dùng quyền Sửa trên `crm-order` – hay cần tách quyền riêng?

Cần anh: chạy lại `DB/seed/ObjectMenu_seed.sql` + `UserRole_seed.sql` trên DB dev / staging (chỉ thêm mục "Duyệt – Đơn đặt suất ăn" và quyền CEO); cho phép cài `openpyxl` để cập nhật Catalog.

### T16 – Sổ kho (Pilot P4) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T16-inventory` (worktree `wt-T11`, từ T13).

Kế hoạch:
1. Rà soát InventoryTransaction / InventoryBalance / MaterialLot / MaterialIssue(+Line) / StockCount(+Line) / MaterialReservation / InventoryPolicie / Warehouse / StorageLocation → LEGACY_REVIEW mục (d).
2. Quy tắc lô dùng chung (chặn hết hạn / QC / tạm giữ, thứ tự FEFO / FIFO, cảnh báo) – lớp tĩnh `InventoryLotRules`, dùng chung transaction của service gọi.
3. Sổ cái: điều chỉnh tay, chuyển kho, sổ cái lọc khoảng ngày (SP mới), tính lại tồn; chặn CRUD ghi thẳng sổ / tồn.
4. Tồn: tổng hợp theo kho × nguyên liệu (xuất được, sắp hết hạn, mức tồn), theo lô (thứ tự FEFO, lý do chặn).
5. Phiếu xuất: gợi ý lô, Create (PH-), Post (ISSUE qua `InventoryLedgerService`), Cancel (đảo).
6. Kiểm kê: Create (dòng từ tồn), RecordCounts, Post (ADJUSTMENT; lệch > 2% chỉ admin).
7. Test InMemory + SQL Server (rollback), tài liệu bàn giao P4, `.http`.

Rà soát: cả 9 bảng chỉ có CRUD sinh mã; nghiệp vụ duy nhất là `InventoryLedgerService` (T08) + `InventoryTransaction/Reverse`. Lỗ hổng: `InventoryTransaction/Insert` ghi thẳng vào sổ (không cập nhật tồn); `InventoryBalance` CRUD sửa thẳng tồn; `MaterialLot/Update` đổi được QC KHONG_DAT → DAT và trạng thái; MaterialIssue / StockCount CRUD không số, không dòng, không ghi sổ, đổi trạng thái tùy ý; dòng phiếu sửa tùy ý. Chi tiết: LEGACY_REVIEW (d).

Đã làm (đều trong file cũ, đúng mẫu 4.2; thêm 1 lớp tĩnh dùng chung, không có service / controller mới):
- **`InventoryLotRules`** (`Services/Implement`, lớp tĩnh – không đăng ký DI): `BlockReason` (HSD < hôm nay giờ VN, QC `KHONG_DAT` / `REJECT` / `HOLD`, lô OnHold / Expired), `UseFefoAsync` (InventoryPolicie, không có → FEFO – D-24), `LoadAsync` (tồn theo dòng + lô + thứ tự xuất), `Suggest`, `IsEarlier` (cảnh báo), kiểm kho / vị trí / lô / đơn vị, phân bổ trên các vị trí.
- **Sổ cái (`InventoryLedgerService`)**: `RebuildBalancesAsync` (tính lại OnHand từ sổ theo (kho, vị trí, NL, lô), giữ Reserved, ghi song song cột cũ); chặn bút toán khác đơn vị dòng tồn (`UNIT_MISMATCH`). T11: thêm `InventoryTransaction.TransactionNumber / TransactionNo` vào nguồn MAX của mã `PH-`.
- **`InventoryTransactionService` / `Controller`**: `GetLedger` (SP mới `DB/sp/uspInventoryTransaction_GetLedger.sql` – đã chạy trên DB dev), `Adjust` (ADJUSTMENT, số PH-, lý do bắt buộc, ReferenceType ManualAdjustment), `Transfer` (TRANSFER_OUT / TRANSFER_IN cùng ReferenceId). CRUD Insert / Update / Delete → lỗi nghiệp vụ.
- **`InventoryBalanceService` / `Controller`**: `GetSummary`, `GetByLot`, `RebuildBalance` (`[AdminOnly]`). CRUD → lỗi nghiệp vụ.
- **`MaterialLotService` / `Controller`**: Insert (số lô bắt buộc hoặc sinh `NL-` theo mã NCC, Status Available / OnHold, ghi song song LotNo / QualityStatus), Update (giữ Status, chặn sửa QC không đạt, chặn đổi NL khi có giao dịch), Delete (chặn khi có giao dịch / tồn); mới `Hold`, `Release`.
- **`MaterialIssueService` / `Controller`**: `SuggestLots`, `Create` (JSON; chặn lô hết hạn / QC / tạm giữ / vượt tồn; cảnh báo lô nên xuất trước; dòng không chọn lô → tự phân bổ), `Post` (kiểm lại toàn bộ rồi ghi ISSUE, 1 transaction), `Cancel` (Draft → Hủy; Issued → bút toán đảo), `GetDetail`. CRUD: Insert → Create; Update chỉ Note / IssueDate phiếu Nháp; Delete chỉ Nháp. `MaterialIssueLine` CRUD → lỗi nghiệp vụ.
- **`StockCountService` / `Controller`**: `Create` (form; dòng từ tồn, Physical mặc định = System), `RecordCounts` (JSON; thêm dòng hàng ngoài sổ), `Post` (dòng lệch phải có lý do; lệch dòng > `StockCountVariancePctNeedApproval` → chỉ admin; ADJUSTMENT `ReferenceType = StockCount`; ApprovedBy / ApprovedAt = người ghi sổ), `GetDetail` (% lệch, vượt ngưỡng). CRUD: Insert → Create; Update chỉ Note / người phụ trách; Delete chỉ Draft / Counting. `StockCountLine` CRUD → lỗi nghiệp vụ.
- Hằng trạng thái `Model/Helper/InventoryStatus.cs` (MaterialLot, QC, MaterialIssue, StockCount, ReferenceType); request `Model/Request/Custom/InventoryCustomRequest.cs`; model `Model/Model/InventoryModels.cs`; thông báo tiếng Việt trong `ConstantHelper.Message`; `Results<T>.Success / Fail` (giống `Result<T>`).
- Quyền theo `PermissionConvention` (Get* = View, Create = Add, còn lại Edit); `InventoryBalance/RebuildBalance` = AdminOnly. Không thêm mục menu / seed.

File chính: `Services/Implement/{InventoryLotRules, InventoryLedgerService, InventoryTransactionService, InventoryBalanceService, MaterialLotService, MaterialIssueService, MaterialIssueLineService, StockCountService, StockCountLineService, DocumentNumberService}.cs`, `Services/Interface/I{InventoryLedger, InventoryTransaction, InventoryBalance, MaterialLot, MaterialIssue, StockCount}Service.cs`, `WebApi/Controllers/{InventoryTransaction, InventoryBalance, MaterialLot, MaterialIssue, StockCount}Controller.cs`, `Data/Repositories/Results.cs`, `Model/Helper/{InventoryStatus, ConstantHelper}.cs`, `Model/Request/Custom/InventoryCustomRequest.cs`, `Model/Model/InventoryModels.cs`, `DB/sp/uspInventoryTransaction_GetLedger.sql`, test `Tests/Inventory/{InventoryTests, InventorySqlServerTests}.cs`, tài liệu `docs/frontend-handoff/P4.md`, `docs/api-tests/p4-inventory.http`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline worktree).
- `dotnet test`: **131/131** xanh (+8):
  - `Inventory/InventoryTests` (7): gợi ý FEFO bỏ lô hết hạn / KHONG_DAT / HOLD / tạm giữ + GetByLot + GetSummary; FIFO theo ngày nhận và FEFO theo HSD; phiếu xuất chặn lô hết hạn / REJECT / vượt tồn, cảnh báo FEFO, tự phân bổ, Post ghi ISSUE, Post thiếu tồn không ghi gì, lô bị tạm giữ sau khi lập phiếu; hủy phiếu đã xuất → đảo, tồn trở lại; chuỗi điều chỉnh / chuyển kho / xuất / đảo khớp sổ + RebuildBalance sửa tồn lệch; kiểm kê (lý do bắt buộc, lệch 20% người thường bị chặn – admin ghi được, lệch ~1% người thường ghi được, ADJUSTMENT đúng); CRUD cũ không phá sổ / lô.
  - `Inventory/InventorySqlServerTests` (1, SQL Server dev): nhập 3 lô, lô hết hạn bị chặn, Create FEFO với số PH- qua sp_getapplock, Post ghi ISSUE + cột cũ song song, thiếu tồn bị chặn, rollback không để lại dữ liệu, gọi SP GetLedger – đã chạy, không bị bỏ qua.
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`.

AC (T16, bản Pilot):
- [x] Post IN / OUT / ADJUSTMENT / REVERSAL (+ TRANSFER) qua `InventoryLedgerService`; InventoryBalance tính lại (theo từng bút toán + `RebuildBalance`).
- [x] Gợi ý lô FEFO (mặc định) / FIFO theo `InventoryPolicie.UseFefo`; luôn chặn lô hết hạn, KHONG_DAT / REJECT / HOLD, tạm giữ; cảnh báo khi còn lô nên xuất trước.
- [x] Test FEFO, chặn, tồn sau chuỗi giao dịch, đảo bút toán.
- [~] Kiểm kê: ghi nhận + ghi sổ điều chỉnh xong; **duyệt lệch > 2% → DEFERRED(PILOT)** (Pilot: chỉ admin ghi sổ).
- [ ] Giữ chỗ (MaterialReservation) → DEFERRED(PILOT).
- [ ] `DELICARE_API_Catalog.xlsx` chưa cập nhật (không cài được `openpyxl`). Gợi ý: 7.5 lots = Một phần (thiếu trace); 7.6 summary / by-lot / transactions / rebuild / transfer / adjust = Đã xong; 7.7 = Một phần (suggest theo nguyên liệu, create / post / cancel; reservations chưa); 7.8 = Một phần (create / lines / post; submit duyệt chưa). Action thực tế: `docs/frontend-handoff/P4.md`.

DEFERRED(PILOT):
- Duyệt kiểm kê lệch > 2% (`PendingApproval`, ApprovalRequest STOCK_COUNT) và duyệt điều chỉnh tay (STOCK_ADJUSTMENT) qua engine T12.
- MaterialReservation (giữ chỗ theo lệnh SX, giảm ReservedQuantity khi xuất, Reserved → Issued) – làm cùng T14 / T17; gợi ý theo `productionOrderId` (spec 7.7) – T17.
- Job hết hạn lô hằng ngày (LotExpiryJob: Status Expired + WASTE) – T23. Pilot: lô hết hạn đã bị chặn theo HSD, hủy hàng bằng `Adjust` âm.
- `MaterialLot.Status = Consumed` khi hết tồn; truy xuất lô (`trace`, T18); quy đổi đơn vị khi xuất; phân trang `GetSummary`.

Câu hỏi cho anh (DECISIONS GAP-INV-02 … GAP-INV-11):
1. Phiếu xuất chưa gắn lệnh SX ghi `ProductionOrderId` = Guid rỗng – tạm được trong Pilot? (T17 sẽ gắn mẻ / lệnh.)
2. Lô chưa có kết quả QC (nhập tay / tồn đầu kỳ) cho xuất – đúng ý anh? `HOLD` có chặn xuất không (BUSINESS_RULES ánh xạ DAT_MOT_PHAN = HOLD, phần đạt được nhập)?
3. Không có chính sách tồn → FEFO; DB mặc định `UseFefo = 0` → khi tạo chính sách cho NL có hạn dùng có cần tự bật FEFO?
4. Kiểm kê: ngưỡng 2% xét từng dòng (lô) hay tổng phiếu / giá trị? Dòng chưa đếm coi như khớp – được không?
5. Điều chỉnh tay (tồn đầu kỳ, hủy hàng) Pilot chỉ cần quyền Sửa `scm-inv-txn`, chưa duyệt – được không?

Cần anh: chạy `DB/sp/uspInventoryTransaction_GetLedger.sql` trên staging (DB dev đã chạy); cho phép cài `openpyxl` để cập nhật Catalog.

### T14 – Tính nhu cầu NL + đề nghị mua nháp (Pilot P5) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T14-mrp` (worktree `wt-T11`, từ T16).

Kế hoạch:
1. Rà soát MaterialRequirement / ProductionPlan(+Line) / PurchaseRequest(+Line) / SupplierMaterial / Recipe / InventoryBalance → LEGACY_REVIEW mục (e).
2. Đơn Locked × thực đơn (MenuLine theo loại suất + thứ) → món × suất; món × công thức khóa (snapshot) × hao hụt → nhu cầu theo nguyên liệu (quy đổi về đơn vị cơ sở).
3. Trừ tồn xuất được (InventoryLotRules, tại ngày SX), cộng SafetyStock (D-12), NCC gợi ý + MOQ.
4. Ghi trong 1 transaction: kế hoạch neo KH-, dòng kế hoạch, MaterialRequirement (cập nhật tại chỗ), PurchaseRequest Draft PH- + dòng.
5. Đọc: GetByDate / GetDetail. Test InMemory (bộ số tính tay) + SQL Server rollback. Tài liệu P5, `.http`.

Đã làm (đúng mẫu 4.2, trong file cũ):
- **`MaterialRequirementService` / `MaterialRequirementController`**: `Calculate` (POST form `ProductionDate, WarehouseId?, OrganizationUnitId?, Note?`), `GetByDate` (GET `productionDate, organizationUnitId?`; chưa tính → `data = null`), `GetDetail` (GET `productionPlanId`). CRUD Insert / Update / Delete → lỗi nghiệp vụ.
- Thuật toán (PROPOSED, DECISIONS GAP-MRP-01…06): đơn **Locked** của ngày (đơn chưa chốt → cảnh báo); suất dòng đơn chia đều các lựa chọn (SequenceNo); công thức Locked / Obsolete hiệu lực tại ngày (SnapshotJson) → Locked + IsActive → Approved mới nhất (cảnh báo); nhu cầu = định lượng × suất ÷ suất chuẩn × (1 + hao hụt %) (WastePercent, trống = Ingredient.StandardWastePercent), quy đổi về `BaseUnitId` (thiếu quy đổi → cảnh báo + `incomplete`); khả dụng = AvailableQuantity của lô không bị chặn tại ngày SX (lô hết hạn trước ngày SX, QC không đạt, tạm giữ → `blockedQuantity`); thiếu = max(0, nhu cầu + SafetyStock − khả dụng − hàng sắp về (0)); mua = max(thiếu, MOQ) làm tròn lên; NCC ưu tiên → giá → lead time (cảnh báo không kịp ngày).
- Ghi: `ProductionPlan` Draft `KH-YYMMDD` (sp_getapplock của mã KH- tuần tự hóa 2 lần tính cùng ngày) + `ProductionPlanLine` theo món; `MaterialRequirement` theo nguyên liệu (`Shortage / Sufficient / Requested`, ghi song song cột cũ *Qty); `PurchaseRequest` Draft `PH-` + dòng (ghi song song `RequestNo`, `UomId`), NCC gợi ý trong Note. Chi tiết không có cột → JSON trong Note (GAP-MRP-02). Tính lại: cập nhật tại chỗ, dòng thừa xóa mềm; PR Draft giữ số; PR đã gửi duyệt / duyệt / lập PO → chặn; kế hoạch đã duyệt → chặn.
- Cấu hình `BusinessRules:MrpIncludeSafetyStock` (mặc định true, D-12). Trạng thái `Model/Helper/PurchasingStatus.cs`; model `Model/Model/MaterialRequirementModels.cs`; request `Model/Request/Custom/MaterialRequirementCustomRequest.cs`; thông báo tiếng Việt trong `ConstantHelper.Message`.
- Quyền theo `PermissionConvention` (`scm-requirement`: Get* = View, Calculate = Edit). Không thêm mục menu / seed. Không SP mới cho T14.

File T14: `Services/Implement/MaterialRequirementService.cs`, `Services/Interface/IMaterialRequirementService.cs`, `WebApi/Controllers/MaterialRequirementController.cs`, `Model/Helper/{PurchasingStatus, ConstantHelper}.cs`, `Model/Model/MaterialRequirementModels.cs`, `Model/Request/Custom/MaterialRequirementCustomRequest.cs`, `Model/Option/BusinessRulesOptions.cs`, `WebApi/appsettings.json`, test `Tests/Purchasing/{MrpTests, MrpSqlServerTests}.cs`, tài liệu `docs/frontend-handoff/P5.md`, `docs/api-tests/p5-mrp.http`.

Sửa nhỏ backend theo FE-2 (cùng nhánh, file riêng để commit riêng):
- Lọc cha trống: 7 SearchRequest `Guid` → `Guid?` + service đổi `Guid.Empty` → NULL (CustomerContact, CustomerDeliveryLocation, Contract, ContractMeal, SupplierMaterial ×2, InventoryPolicy, StorageZone). SP đã xử lý NULL – không sửa SP.
- Form đọc số theo InvariantCulture: `WebApi/Infrastructure/InvariantFormValueProviders.cs` + 1 dòng `Startup.cs` (GAP-FORM-01). Test giả lập máy chủ vi-VN: "12.5" → 12.5 (bỏ sửa thì test ra 125 – đã thử).
- `DB/sp/uspSupplier_GetAll.sql`, `DB/sp/uspUnitMeasure_GetAll.sql`: đủ cột ViewModel + lọc Status (+ CompanyId); `SupplierService` / `UnitMeasureService` truyền tham số; `UnitMeasureSearchRequest.Status` mới. **Đã chạy trên DB dev.**
- UserRole/SaveAccessAll cấp 1, User/GetAll IsActive, chặn menu cấp 3: **không làm ở nhánh này** – chuyển sang T45R.8 (theo điều phối).

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **137/137** xanh (+6): `Purchasing/MrpTests` (3: bộ số tính tay – Thịt 24,75 kg / tồn 10 + 5 bị chặn / an toàn 2 / MOQ 20 → mua 20; Rau 18 kg, lô KHONG_DAT bị chặn, CT chỉ Approved → cảnh báo; Gạo 36 kg, tồn 50 000 g quy đổi 50 kg, an toàn 20 → mua 6, chưa có NCC; PR PH- 3 dòng, 2 270 000; tính lại sau nhập thêm gạo → cùng kế hoạch / PR, dòng gạo Sufficient, dòng PR gạo xóa mềm; PR PendingApproval → chặn; không có đơn chốt / kho sai / CRUD → 400; kế hoạch Approved → chặn), `Purchasing/MrpSqlServerTests` (1, SQL Server dev: Calculate + tính lại với KH- / PH- qua sp_getapplock trong transaction ngoài rồi rollback – đã chạy, không bị bỏ qua), `Common/FormCultureTests` (1), `MasterData/SearchFilterSqlServerTests` (1, SQL Server dev, chỉ đọc).
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`.

AC (T14, bản Pilot):
- [x] MaterialRequirement từ đơn Locked × công thức đã khóa (snapshot) × hao hụt, quy đổi đơn vị.
- [x] Thiếu hụt so với tồn khả dụng (đã trừ giữ chỗ trên InventoryBalance, bỏ lô bị chặn) + SafetyStock (D-12).
- [x] PurchaseRequest nháp + gợi ý NCC (SupplierMaterial ưu tiên / giá / MOQ).
- [x] Test số liệu với bộ dữ liệu nhỏ tính tay được (+ SQL Server rollback).
- [ ] `DELICARE_API_Catalog.xlsx` chưa cập nhật (máy không có python / openpyxl). Gợi ý: 7.1 GET material-requirements = Đã xong (`MaterialRequirement/GetByDate`, `GetDetail`); calculate = Đã xong (`MaterialRequirement/Calculate`, theo ngày thay vì productionPlanId); create-purchase-request = Một phần (gộp vào Calculate, chưa bufferPercent / duyệt).

DEFERRED(PILOT):
- Job 20:30 (MRP) / 21:00 (đề nghị mua) – T23; Pilot bấm tay `Calculate`.
- Hàng sắp về từ PO (`ExpectedIncomingQuantity`) – sau T15; giữ chỗ `MaterialReservation` theo lệnh SX – T17.
- `bufferPercent`, chọn dòng khi lập đề nghị, duyệt PR 2 cấp, PR → PO – T15. Hao hụt 2 tầng, quy cách đóng gói.
- Lọc đơn theo chi nhánh (Order không có cột chi nhánh).

Câu hỏi cho anh (DECISIONS GAP-MRP-01…06, D-12):
1. MRP tự tạo kế hoạch sản xuất Draft `KH-` theo ngày (chưa theo ca) làm chỗ neo – được không? T17 sẽ tách ca từ kế hoạch này.
2. Suất chia đều cho các lựa chọn món (chưa có số suất theo lựa chọn) – tạm được? Hay tính đủ suất cho mọi lựa chọn (mua dư)?
3. Hao hụt: dùng (1 + WastePercent %) theo BUSINESS_RULES §3 hay chia cho (1 − hao hụt) / YieldPercent như spec 7.1 và Phiếu trước SX?
4. Chỉ đơn Đã chốt được tính; đơn Đã xác nhận chưa chốt chỉ cảnh báo – đúng ý anh?
5. Đề nghị mua đã gửi duyệt thì không cho tính lại – hay cho tính và tạo đề nghị bổ sung phần chênh?

Cần anh: chạy `DB/sp/uspSupplier_GetAll.sql`, `DB/sp/uspUnitMeasure_GetAll.sql` trên staging (DB dev đã chạy).

### T15 – Mua hàng → nhận hàng → QC đầu vào (Pilot P6) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T15-purchasing` (worktree `wt-T11`, từ T14). Chỉ backend (D-40).

Kế hoạch:
1. Rà soát PurchaseRequest / PurchaseOrder / GoodsReceipt / QCInspection (+Line), SupplierIssue, TemperatureLog, SupplierMaterial → LEGACY_REVIEW mục (f).
2. Đề nghị mua → PO: `ConvertToPurchaseOrder` theo NCC gợi ý (PO-), chặn CRUD phá trạng thái.
3. PO: UpdateLines, Submit → duyệt 1 cấp qua engine T12 (handler mới PURCHASE_ORDER), Send / Cancel / Close.
4. Phiếu nhập theo PO đã duyệt (PH-, nhiệt độ nhận) + phiếu QC; kết luận QC DAT / DAT_MOT_PHAN / KHONG_DAT.
5. Ghi sổ: lô NL-, nhập kho phần đạt (InventoryLedgerService), TemperatureLog RECEIVING, SupplierIssue, trạng thái PO.
6. In Đơn đặt hàng NCC + Phiếu trước sản xuất (.xlsx, bố cục file thật – chỉ cấu trúc).
7. Test InMemory + SQL Server (rollback); tài liệu P5.md, `.http`, DECISIONS (GAP-PUR-01…08).

Rà soát: cả 8 bảng chỉ có CRUD sinh mã – CRUD đổi được trạng thái / số / tổng tiền, dòng sửa tùy ý, phiếu nhập không ghi sổ. Lọc `SupplierId` của GetAll PO / phiếu nhập là `Guid` → danh sách luôn rỗng khi không lọc (đã sửa). Mục duyệt `#/wf-inbox?type=scm-po` đã có trong `ObjectMenu_seed.sql` và DB dev (CEO, COO) → **không cần seed mới**. Chi tiết: LEGACY_REVIEW (f).

Đã làm (đều trong file cũ, đúng mẫu 4.2; thêm 1 lớp tĩnh `PurchasingRules` + 1 handler duyệt, không có service / controller mới):
- **Đề nghị mua (`PurchaseRequestService` / `Controller`)**: `GetDetail` (dòng + NCC gợi ý + tiền ước tính + PO đã lập), `ConvertToPurchaseOrder` (form; Nháp / Đã duyệt; mỗi nguyên liệu chọn bảng giá đang hiệu lực tại ngày cần: ưu tiên → giá → lead time; thiếu NCC → lỗi kèm tên; mỗi NCC 1 PO Nháp `PO-YYMMDD-NNNN` (T11, ghi song song PONo / ExpectedDate / UomId / Amount); quy đổi sang đơn vị mua; dưới MOQ / chưa có giá → cảnh báo; đề nghị → Converted), `Cancel`. CRUD: Insert luôn Draft + số PH-; Update / Delete chỉ Draft. `PurchaseRequestLineService`: chỉ ghi khi đề nghị Draft, số lượng > 0, ghi song song UomId.
- **Đơn mua (`PurchaseOrderService` / `Controller`)**: `GetDetail` (dòng + đã nhận / đang chờ / còn lại, phiếu nhập, yêu cầu duyệt đang chờ, `needsDirectorApproval`, `allowedActions`), `UpdateLines` (JSON, Draft, mỗi nguyên liệu 1 dòng, đơn vị / giá mặc định theo bảng giá NCC, tính lại tổng), `Submit` (Draft → PendingApproval + `ApprovalRequest` PURCHASE_ORDER; tổng ≤ `PurchaseOrderAutoApproveMaxAmount` > 0 → tự Approved), `Approve` / `Reject` (gọi `ApprovalRequestService.Decide`; quyền Approve trên `scm-po` = mục "Duyệt – Đơn mua hàng"; hash nội dung = trạng thái + NCC + tổng + dòng; từ chối → về Draft), `Send`, `Cancel` (lý do; chưa có phiếu nhập), `Close`, `ExportOrder` (.xlsx theo sheet "2. Form_PO" của file thật). CRUD: Insert = Draft + PO- + tổng 0 (kiểm NCC), Update chỉ Draft, Delete chỉ PO Draft lập tay. `PurchaseOrderLine` CRUD ghi → lỗi. GetAll truyền thêm Status / PurchaseRequestId; `SupplierId` trống = không lọc.
- **Handler duyệt**: `Services/Implement/Approval/PurchaseOrderApprovalHandler` (BusinessType `PURCHASE_ORDER`, ScreenId `scm-po`, tiêu đề hộp duyệt "PO … – NCC – tổng tiền" + cảnh báo > 50 triệu theo D-11).
- **Phiếu nhập (`GoodsReceiptService` / `Controller`)**: `Create` (JSON; PO Approved / Sent / PartiallyReceived – PO chưa duyệt bị chặn; nhận ≤ còn lại + 10%; số `PH-YYMMDD-NNNN`; dòng: số nhận, đơn vị + đơn giá theo PO, số lô NCC, NSX / HSD (trống = nhận + ShelfLifeDays), nhiệt độ nhận; tạo QCInspection cùng số + QCInspectionLine; nguyên liệu `QcRequired = false` → tự DAT; cảnh báo nhiệt độ), `Post` (form; chỉ khi đã kết luận QC; mỗi dòng 1 lô `NL-YYMMDD-<mã NCC>-NN`, QcStatus = kết quả, KHONG_DAT → OnHold; RECEIPT qua `InventoryLedgerService` **chỉ số đạt**; TemperatureLog RECEIVING gắn lô, IsWithinRange theo ngưỡng; 1 SupplierIssue nếu có dòng không đạt / đạt một phần; phiếu → Posted; PO → PartiallyReceived / Received), `Cancel`, `GetDetail` (dòng + kết quả QC + nhiệt độ / ngưỡng / kết quả gợi ý + lô). CRUD Insert / Delete → lỗi, Update chỉ Note. `GoodsReceiptLine` CRUD ghi → lỗi. GetAll: NCC / PO / kho trống = không lọc.
- **QC đầu vào (`QcInspectionService` / `Controller`)**: `Conclude` (JSON; mọi dòng phải có kết quả; DAT_MOT_PHAN cần 0 < số đạt < số nhận + nhận xét; KHONG_DAT cần nhận xét; HSD trước ngày nhận → chỉ KHONG_DAT; nhiệt độ vượt ngưỡng mà chấp nhận → bắt buộc lý do; nhận cả PASS / HOLD / REJECT; ghi QC line + GoodsReceiptLine.QualityStatus / Accepted / Rejected + QCInspection.Result; phiếu → Accepted / PartiallyAccepted / Rejected; kết luận lại được khi chưa ghi sổ), `GetDetail`. CRUD Insert / Delete → lỗi, Update chỉ Note / Remark. `QcInspectionLine` CRUD ghi → lỗi.
- **In Phiếu trước sản xuất**: `MaterialRequirement/ExportPreProduction?id=<planId>` (.xlsx theo `Phieu_Truoc sx.xlsx`: số suất order, món × suất, nguyên liệu cần / tồn xuất được / bị chặn / tồn an toàn / Đủ – Thiếu / SL đề nghị mua / NCC gợi ý, cột ghi tay sơ chế + chất lượng). Chỉ đọc dòng tiêu đề của file thật, không chép dữ liệu.
- Cấu hình `BusinessRules` (PROPOSED): `PurchaseOrderAutoApproveMaxAmount` 0, `PurchaseOrderDirectorApprovalAmount` 50 000 000, `GoodsReceiptOverReceivePct` 10, `ReceivingTempMaxC` { CHILLED 5, FROZEN −18 }, `SupplierIssueDueDays` 2. `ReferenceLists:QcResult` đổi sang DAT / DAT_MOT_PHAN / KHONG_DAT.
- Hằng trạng thái `PurchaseOrderStatus`, `GoodsReceiptStatus`, `IncomingQcResult`, `ReceivingCode` (`Model/Helper/PurchasingStatus.cs`); request `Model/Request/Custom/PurchasingCustomRequest.cs`; model `Model/Model/PurchasingModels.cs`; thông báo tiếng Việt trong `ConstantHelper.Message`.
- Quyền theo `PermissionConvention`: Get* = View; `GoodsReceipt/Create` = Add; `ExportOrder`, `ExportPreProduction` = Export; còn lại (ConvertToPurchaseOrder, UpdateLines, Submit, Send, Cancel, Close, Post, Conclude) = Edit trên màn tương ứng; `PurchaseOrder/Approve`, `Reject` = Approve `scm-po` (khai báo rõ). Không thêm mục menu / seed / SP.

File chính: `Services/Implement/{PurchaseRequestService, PurchaseRequestLineService, PurchaseOrderService, PurchaseOrderLineService, GoodsReceiptService, GoodsReceiptLineService, QCInspectionService, QcInspectionLineService, MaterialRequirementService, PurchasingRules}.cs`, `Services/Implement/Approval/PurchaseOrderApprovalHandler.cs`, `Services/Interface/I{PurchaseRequest, PurchaseOrder, GoodsReceipt, QcInspection, MaterialRequirement}Service.cs`, `WebApi/Controllers/{PurchaseRequest, PurchaseOrder, GoodsReceipt, QcInspection, MaterialRequirement}Controller.cs`, `Model/Helper/{PurchasingStatus, ConstantHelper}.cs`, `Model/Option/BusinessRulesOptions.cs`, `Model/Request/Custom/PurchasingCustomRequest.cs`, `Model/Model/PurchasingModels.cs`, `Model/SearchRequest/{PurchaseOrder, GoodsReceipt}SearchRequest.cs`, `WebApi/appsettings.json`, test `Tests/Purchasing/{PurchasingTests, PurchasingSqlServerTests}.cs`, tài liệu `docs/frontend-handoff/P5.md` (mục 4–9), `docs/api-tests/p6-purchasing.http`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **143/143** xanh (+6):
  - `Purchasing/PurchasingTests` (5, InMemory): lập PO theo NCC ưu tiên (thắng NCC rẻ hơn), số `PO-261220-NNNN`, tổng 2 150 000 / 450 000, cảnh báo MOQ, đề nghị → Converted, không lập lại / không sửa dòng, PO từ đề nghị không xóa, UpdateLines tính lại tổng + chặn trùng nguyên liệu, nguyên liệu chưa có NCC → lỗi kèm tên; duyệt PO: phiếu nhập trên PO Nháp / Chờ duyệt bị chặn, yêu cầu `XN-` PURCHASE_ORDER, người không có quyền không thấy / không duyệt (Decide 400, Approve 403), người có quyền mục `#/wf-inbox?type=scm-po` thấy tiêu đề và duyệt → Approved + ApprovalDecision (hash 64 ký tự), PO bị sửa sau khi gửi → không duyệt được, từ chối cần lý do → về Nháp, in Đơn đặt hàng mở được, Send, Cancel; nhận hàng: vượt 10% bị chặn, số `PH-`, cảnh báo + gợi ý KHONG_DAT khi vượt nhiệt độ, ghi sổ trước QC bị chặn, 5 nhánh lỗi kết luận QC, ghi sổ → 3 lô `NL-261220-SAPUR3-NN`, chỉ nhập Thịt 10 + Rau 15 (DAT_MOT_PHAN), Cá KHONG_DAT không nhập + lô OnHold, TemperatureLog RECEIVING ×3 (Rau ngoài ngưỡng, có hành động khắc phục), SupplierIssue High / TEMPERATURE / hạn +2 ngày, PO Nhận một phần, **xuất kho lô KHONG_DAT bị chặn, lô DAT_MOT_PHAN chỉ xuất được 15**, lô KHONG_DAT không sửa được thành DAT, nhận bù → Received → Close → không nhận thêm; CRUD cũ không phá máy trạng thái; MRP → đề nghị → PO 2 000 000 + in Phiếu trước sản xuất (SL cần 40, thiếu 40, NCC gợi ý) + MRP không tính lại khi đề nghị đã lập PO.
  - `Purchasing/PurchasingSqlServerTests` (1, SQL Server dev): PR → PO (PO- qua sp_getapplock) → Submit (XN-) → Approve qua engine → phiếu nhập PH- → QC DAT_MOT_PHAN 8 / 10 → ghi sổ: lô NL-, RECEIPT 8, tồn 8, TemperatureLog 1, PO PartiallyReceived, ApprovalDecision APPROVE – trong transaction ngoài rồi rollback, không để lại dữ liệu (đã chạy, không bị bỏ qua).
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không SP / seed mới (không cần chạy script nào trên DB dev / staging).

AC (T15, bản Pilot):
- [x] PR → PO (gộp theo NCC, giá từ SupplierMaterial) → GoodsReceipt (PH-) → QC DAT / DAT_MOT_PHAN / KHONG_DAT → MaterialLot `NL-…` → InventoryTransaction IN (chỉ phần đạt).
- [x] PO duyệt 1 cấp qua engine (quyền qua mục menu, hash nội dung); phiếu nhập trên PO chưa duyệt bị chặn; mã PO- / PH- / NL- đúng định dạng.
- [x] Lô KHONG_DAT không xuất được (MaterialIssue bị chặn); DAT_MOT_PHAN chỉ nhập phần đạt.
- [x] SupplierIssue khi không đạt / đạt một phần; TemperatureLog RECEIVING (ngưỡng mát / đông lạnh PROPOSED).
- [x] In Đơn đặt hàng NCC + Phiếu trước sản xuất (.xlsx, test mở được).
- [x] 1 test SQL Server rollback cho toàn luồng PR → PO → GR → QC → tồn.
- [ ] `DELICARE_API_Catalog.xlsx` không sửa (theo điều phối). Gợi ý cột J / L (cột M = T15): 7.2 GET purchase-requests = Đã xong (`PurchaseRequest/GetAll`, `GetDetail`); POST = Đã xong (`PurchaseRequest/Insert` + `PurchaseRequestLine/Insert`); submit = Không làm (Pilot – GAP-PUR-06); convert-to-po = Đã xong (`PurchaseRequest/ConvertToPurchaseOrder`). 7.3 approve = Đã xong (`PurchaseOrder/Submit` + `Approve` / `Reject`, 1 cấp); send = Một phần (`PurchaseOrder/Send`, chưa gửi email / Zalo); receive = Đã xong (`GoodsReceipt/Create`); close / cancel = Đã xong (`PurchaseOrder/Close`, `Cancel`). 7.4 GET goods-receipts = Đã xong (`GoodsReceipt/GetAll`, `GetDetail`); post = Đã xong (`GoodsReceipt/Post`). 9.1 GET inspections = Đã xong (`QcInspection/GetAll`, `GetDetail`); conclude = Đã xong (`QcInspection/Conclude`). 9.3 temperature-logs = Một phần (ghi RECEIVING khi nhập hàng; series / IoT chưa). 9.5 supplier-issues = Một phần (CRUD + tự tạo khi QC; transition chưa).

DEFERRED(PILOT):
- Duyệt đề nghị mua 2 cấp (spec 7.2 submit), duyệt PO 2 cấp theo ngưỡng D-11 (> 50 triệu GĐ) – engine T12 mới 1 cấp.
- Gửi PO cho NCC qua email / Zalo (OutboundMessage), bản PDF; VAT trên mẫu in (GAP-PUR-07); logo.
- Trả hàng NCC / hủy lô KHONG_DAT (chứng từ trả hàng, bút toán RETURN), đảo phiếu nhập đã ghi sổ; chuyển trạng thái sự cố NCC (`transition`, spec 9.5).
- Hàng sắp về từ PO cho MRP (`ExpectedIncomingQuantity` – T14 vẫn = 0); vị trí kho theo `Ingredient.StorageType` khi ghi sổ (Pilot: chọn `StorageLocationId` hoặc trống); mã thiết bị đo nhiệt độ; so sánh báo giá nhiều NCC (sheet `Price_Compare` của file thật).

Câu hỏi cho anh (DECISIONS GAP-PUR-01…08, D-11):
1. Pilot không bắt buộc duyệt đề nghị mua (chỉ duyệt PO 1 cấp) – được không? PO > 50 triệu có cần GĐ duyệt thêm ngay trong Pilot?
2. PO bị từ chối → về Nháp để sửa gửi lại (thay vì trạng thái Từ chối) – đúng ý anh? Cho nhận hàng khi PO mới "Đã duyệt" (chưa bấm "Đã gửi NCC") – được không?
3. Hàng không đạt (KHONG_DAT / phần trả của DAT_MOT_PHAN) không nhập kho, chỉ tạo lô tạm giữ để truy xuất và ghi sự cố NCC – hay anh muốn nhập vào kho "chờ trả" rồi xuất trả?
4. Ngưỡng nhiệt độ nhận: mát ≤ 5 °C, đông lạnh ≤ −18 °C, hàng khô / thường không kiểm – đúng chưa? Vượt ngưỡng mà vẫn nhận (ghi lý do) có được không hay bắt buộc KHONG_DAT?
5. Dung sai nhận vượt 10% / dòng PO có phù hợp (rau, thịt cân lệch)?
6. In Đơn đặt hàng: có cần cột VAT (mẫu thật có % VAT theo NCC – chưa có cột trong DB)?

### T14.1 – MRP: dự phòng lựa chọn + tính bổ sung (anh chốt 2026-11-16) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T14.1-mrp-buffer` (worktree `wt-T11`, từ T15). Chỉ backend + tài liệu.

Anh chốt 2026-11-16 (ghi ở DECISIONS mục "Anh chốt 2026-11-16", GAP-MRP-01 / 03 / 05 / 06 đánh dấu "Anh chốt 2026-11-16"): kế hoạch nháp `KH-` theo ngày; chia đều lựa chọn + dự phòng 15% / lựa chọn; hao hụt giữ (1 + %); chỉ đơn Locked; đề nghị mua đã đi tiếp → tính lại vẫn chặn + thêm tính bổ sung; T45R.8: IsActive mặc định không hoạt động, ChangePassword giữ hành vi cũ.

Đã làm (trong file cũ, đúng mẫu 4.2; không bảng / cột / SP / seed mới):
- **Dự phòng lựa chọn:** `BusinessRules:MrpOptionBufferPct` (mặc định 15, 0 = tắt; `BusinessRulesOptions` + `appsettings.json`). Thực đơn có ≥ 2 lựa chọn cho loại suất: mỗi món = suất ÷ số lựa chọn × (1 + 15%); 1 lựa chọn không cộng. PROPOSED: món có trong nhiều lựa chọn (cơm chung) không vượt số suất dòng đơn (món chung mọi lựa chọn không cộng dự phòng). `listDish[].bufferPortions` mới (phần dự phòng, đã nằm trong `portions`).
- **`MaterialRequirement/CalculateTopUp`** (POST form `ProductionDate, WarehouseId?, OrganizationUnitId?, Note?`; quyền Edit `scm-requirement` theo PermissionConvention): cùng khóa ngày với Calculate; kế hoạch phải có + còn Nháp; cần ≥ 1 đề nghị của kế hoạch đã Chờ duyệt / Duyệt / Lập PO (còn Nháp → dùng Calculate). Tính lại nhu cầu; đã đề nghị = Σ dòng các đề nghị chưa Hủy / Từ chối (chính + bổ sung trước) − đã nhận đạt (phiếu nhập Đã ghi sổ của PO lập từ các đề nghị đó); chênh lệch dương → **1 đề nghị mua Nháp `PH-` mới** chỉ cho phần chênh (MOQ + NCC gợi ý như Calculate; Note "BỔ SUNG – … bổ sung cho PH-…"); không thiếu thêm → không lập, thông báo; âm → cảnh báo. Cập nhật tại chỗ dòng nhu cầu (`PurchaseQuantity` = đã đề nghị + bổ sung; nguyên liệu hết nhu cầu giữ dòng nhu cầu 0) + dòng kế hoạch; liên kết đề nghị bổ sung trong `ProductionPlan.Note` (`TopUpPurchaseRequestIds`) – GAP-MRP-08 (PROPOSED). 1 transaction, `RequireActor`.
- Calculate: thông báo chặn chỉ hướng sang CalculateTopUp; kế hoạch đã có đề nghị bổ sung còn hiệu lực → chặn (tránh lập trùng). Kết quả GetByDate / GetDetail / Calculate thêm `listTopUpPurchaseRequest[]`; CalculateTopUp thêm `topUp` (`purchaseRequest`, `listComparedRequestNumber`, `listLine[]` thiếu / đã đề nghị / chênh lệch / bổ sung / NCC, `listWarning`).
- Tách phần tính chung của Calculate thành `ComputeAsync` / `PriceAsync` / `UpsertPlanLines` / `SoftDeleteAsync` (cùng service) để Calculate và CalculateTopUp dùng chung – số liệu Calculate không đổi khi dự phòng = 0.

File: `Services/Implement/MaterialRequirementService.cs`, `Services/Interface/IMaterialRequirementService.cs`, `WebApi/Controllers/MaterialRequirementController.cs`, `Model/Model/MaterialRequirementModels.cs`, `Model/Helper/ConstantHelper.cs`, `Model/Option/BusinessRulesOptions.cs`, `WebApi/appsettings.json`, test `Tests/Purchasing/MrpTests.cs`, tài liệu `docs/claude/{DECISIONS, BUSINESS_RULES, PROGRESS}.md`, `docs/frontend-handoff/P5.md`, `docs/api-tests/p5-mrp.http`. Không đụng MaterialIssue* / ProductionPlan* / ProductionBatch* / ProductionOrder* service (T17 song song).

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **145/145** xanh (+2, `Purchasing/MrpTests`): (1) 2 lựa chọn 100 suất → món A = B = 50 × 1,15 = 57,5 (dự phòng 7,5, NL 5,75 kg), cơm chung = 100, thực đơn 1 lựa chọn TOI 40 không dự phòng; cấu hình 0 → 50; (2) Calculate → đề nghị 1 thịt 10 → bổ sung khi còn Nháp bị chặn → gửi duyệt + tăng đơn 100 → 130 → Calculate chặn, CalculateTopUp lập đề nghị Nháp mới chỉ thịt 3 (300 000), đề nghị 1 không đổi, dòng nhu cầu 13 / mua 13 → gọi lại không lập → giảm còn 110 → chỉ cảnh báo thừa 2 → hủy đề nghị 1 nhưng còn đề nghị bổ sung → Calculate chặn. Test T14 cũ chạy với dự phòng 0 (giữ bộ số tính tay).

PROPOSED (chờ anh xác nhận – GAP-MRP-03 / 08):
1. Món chung nhiều lựa chọn không vượt số suất dòng đơn (không cộng dự phòng cho cơm / canh chung).
2. "Đã đề nghị" trừ phần đã nhận đạt (đã nằm trong tồn); đề nghị Hủy / Từ chối không tính; PO bị hủy không tự trừ.
3. Calculate bị chặn khi kế hoạch đã có đề nghị bổ sung còn hiệu lực; bổ sung chỉ khi kế hoạch còn Nháp.
4. Thừa do làm tròn MOQ cũng hiện cảnh báo "thừa" (theo đúng ý "âm → cảnh báo").
Release 2: suất theo từng lựa chọn (đặt trước / tỉ lệ 4 tuần) cần cột mới – GAP-MRP-07.

### T15.1 – Quy tắc mua hàng anh chốt 2026-11-16 (nhiệt độ 3 vùng, GĐ duyệt PO ≥ 50 triệu, VAT phương án A) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T15.1-po-rules` (worktree `wt-T11`, từ T15 + T14.1). Chỉ backend + seed + tài liệu. Không đụng MaterialIssue* / Production* / MaterialRequirementService.

Kế hoạch: (1) cấu hình `ReceivingTemp` 3 vùng thay `ReceivingTempMaxC`, kiểm ở QcInspection/Conclude, đánh dấu + sự cố NCC ở GoodsReceipt/Post; (2) engine duyệt: mục quyền theo từng yêu cầu (hook mặc định trong `IApprovalHandler`), PO ≥ ngưỡng → `scm-po-director`; seed mục menu + quyền CEO (CodeGen + seed khớp nhau), chạy seed DB dev; (3) VAT tính khi đọc / in; (4) test; (5) tài liệu.

Đã làm (trong file cũ, đúng mẫu 4.2; không bảng / cột / SP mới):
- **Nhiệt độ nhận 3 vùng** – `BusinessRulesOptions.ReceivingTemp` (`ReceivingTempRule { AcceptMax, ConditionalMax, RequireNoThawConfirmation }`: CHILLED 5 / 8, FROZEN −18 / −15 + xác nhận rã đông; `ReceivingTempMaxC` bỏ hẳn, appsettings đổi theo). `PurchasingRules`: `ReceivingTempRuleOf`, `TempZone` (ACCEPT / CONDITIONAL / REJECT / MISSING / NOT_CHECKED – hằng `ReceivingTempZone`), `SuggestResult(zone, …)`, `ConditionalNote`. `QcInspection/Conclude`: khi kết luận DAT / DAT_MOT_PHAN – chưa đo (mát / đông lạnh) → lỗi; vùng loại → lỗi (chỉ KHONG_DAT); vùng có điều kiện → bắt buộc `remark`, đông lạnh thêm `noThawConfirmed` (request mới); ghi `QCInspectionLine.Note` = `[UU_TIEN_DUNG_TRUOC] Nhận có điều kiện …`. `GoodsReceipt/Create`: cảnh báo theo vùng; `QcRequired = false` chỉ tự DAT khi vùng nhận / không kiểm. `GoodsReceipt/Post`: lô nhận có điều kiện → Note thêm cờ ưu tiên dùng trước; dòng có điều kiện (kể cả DAT) luôn vào `SupplierIssue` (TEMPERATURE). Chi tiết phiếu nhập thêm `temperatureZone(Name)`, `conditionalMaxTemperature`, `noThawConfirmationRequired`, `useFirst`.
- **GĐ duyệt PO ≥ 50 triệu** – `IApprovalHandler` thêm 2 member mặc định: `ApprovalScreenIds` (= [ScreenId]) và `RequiredScreenIdAsync(businessId)` (= ScreenId) → handler cũ (công thức, đơn lệch) không đổi. `ApprovalRequestService.GetMyPending` lọc theo mục quyền của **từng yêu cầu** (type = mục đó), `Decide` kiểm Approve trên mục đó (ApprovalNoRight). `PurchaseOrderApprovalHandler`: mục `scm-po` / `scm-po-director` theo `PurchaseOrderService.ApprovalScreenIdAsync` (tổng ≥ `PurchaseOrderDirectorApprovalAmount`). `PurchaseOrder/Approve` / `Reject`: bỏ `[Permission("scm-po", Approve)]` → `[SkipPermission]` (engine kiểm, như `ApprovalRequest/Decide`) – người không quyền nhận 400 thay vì 403. Submit: PO lớn không tự duyệt, thông báo "cần Giám đốc duyệt". GetDetail: `needsDirectorApproval` (≥), `approvalScreenId`.
- **Seed**: `tools/AppDelicare.CodeGen/Seeds.cs` thêm `ExtraApprovals` (`scm-po-director`, "Duyệt – Đơn mua hàng (Giám đốc)", P4, nhóm CEO) → sinh lại `DB/seed/ObjectMenu_seed.sql` (+1 mục OrderShow 9916) và `UserRole_seed.sql` (+ CEO AllowAccess / AllowEdit) bằng `dotnet run --project tools/AppDelicare.CodeGen -- --layers security` (chỉ 2 file seed đổi, đúng phần thêm). **Đã chạy 2 seed trên DB dev `(localdb)\MSSQLLocalDB` / `AppDelicare`** (insert-only, idempotent): có mục `#/wf-inbox?type=scm-po-director`, CEO = 1 / 1.
- **VAT phương án A** – `BusinessRulesOptions.PurchaseVat { DefaultPct = 8, ByIngredientCategoryCode = {} }` (appsettings có ví dụ PROPOSED `RAU: 5` trong chú thích). `PurchasingRules.VatPctByIngredientAsync` (mã nhóm NL → nhóm cha ≤ 5 cấp → mặc định), `VatAmount` (làm tròn đồng). `PurchaseOrder/GetDetail`: `listLine[].vatPct / vatAmount / amountInclVat`, `subtotalAmount / vatAmount / totalAmountInclVat`. `ExportOrder`: cột % VAT + Tiền VAT, chân bảng chưa VAT / thuế VAT / gồm VAT. Không ghi DB.
- Tài liệu: DECISIONS (mục "Anh chốt 2026-11-16 (T15 mua hàng → T15.1)", ý 7 T14.1, D-11 đã chốt, GAP-PUR-02/03/04/07/08 cập nhật, **GAP-PUR-09** (cột VAT → R2, cần anh duyệt migration), **GAP-PUR-10** (cờ ưu tiên dùng trước trong Note), GAP-MRP-03 / 08 chốt), BUSINESS_RULES §4, `docs/frontend-handoff/P5.md` mục 10, `docs/api-tests/p6-purchasing.http` (21–24).

File: `Model/Option/BusinessRulesOptions.cs`, `Model/Helper/{PurchasingStatus, ConstantHelper}.cs`, `Model/Model/PurchasingModels.cs`, `Model/Request/Custom/PurchasingCustomRequest.cs`, `Services/Interface/{IApprovalHandler, IPurchaseOrderService}.cs`, `Services/Implement/{ApprovalRequestService, PurchaseOrderService, GoodsReceiptService, QCInspectionService, PurchasingRules}.cs`, `Services/Implement/Approval/PurchaseOrderApprovalHandler.cs`, `WebApi/Controllers/PurchaseOrderController.cs`, `WebApi/appsettings.json`, `tools/AppDelicare.CodeGen/Seeds.cs`, `DB/seed/{ObjectMenu, UserRole}_seed.sql`, test `Tests/Purchasing/{PurchasingOwnerRulesTests (mới), PurchasingTests, MrpTests}.cs`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **149/149** xanh (+4):
  - `PurchasingOwnerRulesTests.Receiving_temperature_three_zones…`: mát 4 nhận / 7 có điều kiện (thiếu lý do → lỗi; lô `[UU_TIEN_DUNG_TRUOC]`, sự cố NCC) / 9 loại (DAT hoặc DAT_MOT_PHAN kèm lý do → lỗi; KHONG_DAT → lô OnHold, không tồn); đông lạnh −20 nhận / −16 (thiếu `noThawConfirmed` → lỗi) / −12 loại; DRY không nhiệt độ OK; mát chưa đo → lỗi, gửi `temperature` lúc QC → OK; 6 dòng nhập kho, 7 TemperatureLog; phiếu 2 chỉ 1 dòng có điều kiện DAT → sự cố TEMPERATURE / Medium.
  - `…Po_at_or_above_director_threshold…`: PO 60 triệu – người duyệt `scm-po` không thấy (GetMyPending không type / `scm-po`), Approve / Reject / Decide → "không có quyền"; GĐ (`scm-po-director`) thấy trong `type=scm-po-director` (screenId đúng, tiêu đề "Giám đốc"), duyệt → Approved; PO 10 triệu – người duyệt `scm-po` duyệt, GĐ không duyệt được; PO đúng 50 triệu → `needsDirectorApproval`.
  - `…Vat_is_computed…`: Thịt 1 000 000 × 8% = 80 000; Rau (nhóm con của "RAUTUOI" = 5%) 400 000 → 20 000; Muối 37 035 × 8% = 2 962,8 → 2 963; tổng 1 437 035 / 102 963 / 1 539 998 – GetDetail và file in; `purchaseOrder.totalAmount` không đổi.
  - `MrpTests.CalculateTopUp_subtracts_received_qc_passed_quantity…` (GAP-MRP-08 ý 1 anh chốt): đề nghị 10 → PO → nhận 6 DAT ghi sổ → bổ sung: thiếu 4, đã đề nghị 4, chênh 0, không cảnh báo thừa; tăng 130 suất → bổ sung đúng 3.
  - Test T15 cũ chỉnh theo quy tắc mới: Rau 8 °C gợi ý DAT (vùng có điều kiện); Approve không quyền → 400 thay 403.
- `git status`: không thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`.

PROPOSED (chờ anh xác nhận):
1. Bảng % VAT theo nhóm nguyên liệu (vd. rau củ tươi 5%) – đang để trống, mọi dòng 8%; cần mã nhóm thật (GAP-PUR-09).
2. Cờ "ưu tiên dùng trước" chỉ nằm trong Note (GAP-PUR-10); xuất kho FEFO chưa tự ưu tiên lô này (MaterialIssue – T17).
3. Hàng mát / đông lạnh chưa đo nhiệt độ: chặn khi kết luận DAT / DAT_MOT_PHAN (KHONG_DAT vẫn cho) – không chặn ở bước lập phiếu nhập (chỉ cảnh báo).
4. Hộp duyệt: `type=scm-po` chỉ trả PO dưới ngưỡng, PO lớn chỉ ở `type=scm-po-director` (người có cả 2 quyền xem 2 hộp).
5. Phiếu nhập chỉ có dòng nhận có điều kiện (đều DAT) → sự cố NCC mức Medium.
### T45R.8 – Hoàn thiện quản trị người dùng (D-37, D-38, D-39) · 2026-11-16 · Backend DONE · FE TODO (D-40)
Nhánh `feat/T45R.8-admin` (worktree `wt-T45R8`). Chỉ backend; phần giao diện (`adm-user` 1 form, popup mật khẩu tạm, chuyển màn Đổi mật khẩu) làm cùng đợt FE.

Rà soát code cũ (đã có – bổ sung vào đúng file):
- `Person/Insert` → `PersonService.Insert` → `UserService.InsertUserAfter`: tự sinh tên (lỗi khi họ tên có 2 dấu cách hoặc > 6 từ, không bỏ dấu chữ cái đầu), mật khẩu = tên đăng nhập, không gán nhóm, User `IsActive = false`.
- `User/Insert` không chặn Person đã có User; `User/ResetPassword` đặt mật khẩu = tên đăng nhập, trả `Result<bool>`.
- `PermissionFilter` không có khái niệm "phải đổi mật khẩu"; `UserRole/SaveAccessAll` chỉ cấp menu cấp 2; `uspUser_GetAll` không lọc IsActive; `ObjectMenu/Insert|Update` cho tạo cấp 3.

Đã làm:
- **D-37** `DB/seed/ObjectMenu_disable_legacy.sql` (idempotent, liệt kê 26 Id + MetaLink): `IsActive = 0` cho 21 mục cấp 2 kiểu cũ (`benh-nhan`, `quan-ly-…`, `kho-thuoc-…`…) và 5 mục cấp 1 (`trang-chu` + 4 nhóm `#` theo Id: Danh mục, Khám bệnh, Kho thuốc, Hệ thống) – nhóm chỉ tắt khi không còn con đang bật. Không xóa, không đụng UserRole. **DB dev: chạy 2 lần – lần 1 tắt 21 + 5, lần 2 tắt 0, không lỗi.** Đã thêm vào `deploy/db/Deploy-Database.ps1` và `deploy/docker/db-init.sh` (sau `ObjectMenu_seed.sql`). Staging: anh chạy.
- **D-38** `PersonCustomRequest` + `UserName`, `ListUserGroupId`, `IsActive` (chỉ trường request). `PersonService.InsertPersonUser` (action `Person/Insert`): kiểm tài khoản **trước** khi ghi (tên đăng nhập / nhóm) → Person → `UserService.InsertUserAfter(personId, fullName, typeSave, userName?, listUserGroupId?, isActive)` → `UserClaimService.SaveAfterUser`, tất cả trong 1 transaction; trả `Result<PersonUserCreatedViewModel>` { PersonId, UserId, UserName, TempPassword }. `UserService.ValidateNewAccount`: tên nhập vào → chữ thường, `^[a-z0-9][a-z0-9._-]{1,49}$`, không trùng (LOWER, bỏ bản xóa mềm); trống → sinh như cũ (tên + chữ cái đầu, bỏ dấu cả chữ đầu, trùng thêm 1, 2…). `User/Insert` → `UserService.InsertAccount`: chặn Person đã có User, Person không tồn tại, trùng tên; mật khẩu tạm; trả `Result<PersonUserCreatedViewModel>`. `Person/GetAllDataByKey?isEdit=false` loại mọi hồ sơ đã có User (kể cả đang khóa).
- **D-39** `EncriptFunctionsHelper`: `GenerateTemporaryPassword` (12 ký tự, `RandomNumberGenerator`, không 0 O 1 l I, ≥ 1 hoa / thường / số, xáo Fisher–Yates), `GenerateTemporaryPasswordHash` → `PBKDF2T$<hết hạn unix>$100000$<salt>$<hash>`, `IsTemporary`, `IsTemporaryExpired`; `VerifyPassword` hiểu dạng tạm. Hạn = `IClock.UtcNow` + 24 giờ. `User/ResetPassword` → `Result<string>` (Data = mật khẩu tạm, một lần; log chỉ ghi user + người thao tác; AuditChange tự ghi Actor / CreatedBy = admin, IdChange = user, "PassWord: (đã thay đổi – không lưu giá trị)"). `LoginService`: mật khẩu tạm quá hạn → `code 400` "Mật khẩu tạm đã hết hạn, liên hệ quản trị"; còn hạn → `data.mustChangePassword = true` + claim `pwd_temp`. `PermissionFilter`: token `pwd_temp` mà DB vẫn là mật khẩu tạm → 403 `{ responseCode: 403, responseMessage: "Cần đổi mật khẩu" }` cho mọi action trừ `User/ChangePassword`, `User/GetCurrent`, `Authentication/*` (kiểm trước cả SkipPermission). `ChangePassword` lưu PBKDF2 thường (chặn khi mật khẩu tạm đã hết hạn); đổi xong token cũ dùng bình thường. `User/GetCurrent` thêm `mustChangePassword`.
- **FE-1 follow-ups:** `UserRole/SaveAccessAll` gồm cả menu cấp 1; `User/GetAll` lọc `IsActive` (`UserSearchRequest.IsActive`, SP `uspUser_GetAll` thêm `@IsActive BIT = NULL` – đã chạy trên DB dev); `ObjectMenu/Insert|Update` chặn cấp 3 (cha phải là mục cấp 1, mục có con không được có cha), `LevelShow` chuẩn hóa theo `ParentId`.
- Docs: `docs/frontend-handoff/P0-P2.md` (mục 1.1, bảng adm-person / adm-user / adm-permission / adm-objectmenu, **mục 6** mới), `docs/api-tests/t45r8-admin-users.http`, UAT_CHECKLIST (T45R.8), DECISIONS (GAP-ADM-02 cập nhật, GAP-ADM-04, GAP-ADM-05), LEGACY_REVIEW.

Thay đổi hợp đồng API (FE cần biết):
- `Person/Insert`: `data` từ `true` → `{ personId, userId, userName, tempPassword }`.
- `User/Insert`: `data` từ `true` → cùng dạng trên; mật khẩu ban đầu không còn = tên đăng nhập.
- `User/ResetPassword`: `Result<bool>` → `Result<string>` (Data = mật khẩu tạm). Route / method (GET) giữ nguyên.
- `Authentication/Login`: thêm `data.mustChangePassword` (trường cũ giữ nguyên); `User/GetCurrent`: thêm `mustChangePassword`.
- Mọi API: có thể trả 403 kèm body "Cần đổi mật khẩu" khi token là mật khẩu tạm.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **140/140** xanh (+9; baseline 131):
  - `Admin/UserAccountTests` (7): tạo 1 bước (1 Person + 1 User + 2 nhóm, tên chuẩn hóa, PBKDF2T$, đăng nhập được + mustChangePassword); tự sinh tên `hadt` / `hadt1`, không gửi IsActive → false; trùng tên (khác hoa thường) / tên có dấu / nhóm sai bị chặn và không để lại Person; `User/Insert` cho Person đã có User / trùng tên bị chặn, hồ sơ chưa có User thì được; 300 mật khẩu tạm đúng 12 ký tự, không ký tự dễ nhầm, đủ hoa / thường / số + định dạng hash; reset trả mật khẩu tạm, AuditChange có "ai reset cho ai", không chứa mật khẩu (và không chứa hash tạm), log (Serilog sink bắt tạm) không chứa mật khẩu; đăng nhập tạm → claim `pwd_temp`, GetCurrent được, `UserGroup/GetAllData` / `Person/GetProfile` / `User/GetById` 403 "Cần đổi mật khẩu" (kể cả admin), đổi xong token cũ dùng được, đăng nhập lại bình thường; TestClock: +23 giờ đăng nhập được, +24 giờ 1 phút bị từ chối đúng thông báo, ChangePassword bằng mật khẩu tạm hết hạn bị chặn.
  - `Admin/AdminServiceTests`: + `Object_menu_insert_update_reject_a_third_level`; `SaveAccessAll` kiểm thêm menu cấp 1.
  - `Admin/UserAccountSqlServerTests` (1, SQL Server dev – đã chạy, không skip): `User/GetAll?IsActive=true|false`.
  - `Security/PasswordHashTests`: sửa kịch bản tạo / reset theo D-39 (mật khẩu ban đầu và mật khẩu reset là mật khẩu tạm trả về, lưu PBKDF2T$). MD5 cũ / PBKDF2 thường vẫn đăng nhập và nâng cấp như cũ.
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`.

AC (phần backend): [x] test tạo 1 bước / chặn; [x] test mật khẩu tạm (12 ký tự, chỉ ChangePassword / GetCurrent, đổi xong bình thường, quá 24h, không trong AuditChange / log); [x] script D-37 chạy 2 lần (DB dev); [x] build / test xanh. Còn: `npm run build` + UAT PILOT-FE (phần FE), Catalog.

`DELICARE_API_Catalog.xlsx` **chưa cập nhật** (không có openpyxl). Gợi ý:
- Dòng 35 (`POST /admin/users`): J = `Đã xong`, L = `POST api/Person/Insert` (form: hồ sơ + UserName, ListUserGroupId, IsActive → PersonUserCreatedViewModel); hồ sơ có sẵn: `POST api/User/Insert`; M = `T45R.8`; K = "Mật khẩu tạm thay cho password; IsActive mặc định false (GAP-ADM-05)".
- Dòng 38 (`POST /admin/users/{id}/reset-password`): J = `Đã xong`, L = `GET api/User/ResetPassword?id=` (Result<string> = mật khẩu tạm), M = `T45R.8`.
- (Dòng 34 – danh sách user lọc isActive: có thể ghi thêm L = `GET api/User/GetAll?IsActive=`, M = `T45R.8`.)

PROPOSED – BA/PO CONFIRM: quy tắc tên đăng nhập (GAP-ADM-04); `IsActive` mặc định false khi không gửi (GAP-ADM-05); `User/Insert` dùng mật khẩu tạm + đổi `data`; giữ `ResetPassword` là GET.

Cần anh: chạy trên staging `DB/seed/ObjectMenu_disable_legacy.sql` và `DB/sp/uspUser_GetAll.sql` (hoặc chạy lại `Deploy-Database.ps1`); chốt GAP-ADM-04 / 05.
### T17 – Sản xuất theo mẻ (Pilot P7) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T17-production` (worktree `wt-T17`, từ T15). Chỉ backend (D-40).

Kế hoạch:
1. Rà soát ProductionPlan(+Line) / ProductionOrder / ProductionBatche / ProductionOutput / MealWasteRecord / TemperatureLog / FoodSafetyChecklist(+Item, +Result) / ProductionLine / Shift → LEGACY_REVIEW mục (g). Đọc (không sửa) `MaterialRequirementService` (T14 tạo KH- + dòng món).
2. Duyệt kế hoạch (1 cấp) → phát hành: lệnh SX PH- theo món (công thức khóa + ảnh chụp định lượng), mẻ ME-YYMMDD-<CA>-NN chia theo dây chuyền, lịch ngược từ giờ xuất bếp.
3. Xuất NL cho mẻ: tính theo định lượng × phần mẻ, gọi `MaterialIssueService.Create` / `Post` (FEFO, chặn lô) gắn lệnh SX thật.
4. Sự kiện mẻ: Start → Finish; nhiệt độ lõi (TemperatureLog CORE), lưu mẫu (SAMPLE_RETAINED), sản lượng / hao hụt; QC đạt / giữ / loại; đóng gói; hoàn thành lệnh / kế hoạch.
5. In Phiếu sản xuất (.xlsx, theo tiêu đề file thật `Phieu san xuat.xlsx`).
6. Chặn CRUD cũ phá máy trạng thái. Seed checklist SAMPLE_RETAINED.
7. Test InMemory (bộ số tính tay) + 1 test SQL Server rollback; tài liệu P7.md, `.http`, DECISIONS (GAP-PRD), LEGACY_REVIEW, BUSINESS_RULES.

Rà soát: cả 10 bảng chỉ có CRUD sinh mã – CRUD tạo lệnh / mẻ không số, đổi trạng thái / QC tùy ý (đóng gói được khi chưa đo nhiệt độ, chưa lưu mẫu). DB dev chưa có `ProductionLine`, chưa có checklist `SAMPLE_RETAINED`. Chi tiết: LEGACY_REVIEW (g).

Đã làm (đều trong file cũ, đúng mẫu 4.2; thêm 1 lớp tĩnh dùng chung `ProductionRules`, không có service / controller mới):
- **Kế hoạch (`ProductionPlanService` / `ProductionPlanController`, `prod-plan`)**: `GetDetail`; `Approve` (Nháp → Đã duyệt, `[Permission("prod-plan", Approve)]` = mục "Duyệt – Kế hoạch sản xuất" đã có trong seed); `Release` (form `Id, ShiftId?, ProductionLineId?, DispatchTime?, Note?`, 1 transaction): ca (mặc định `ProductionDefaultShiftCode` = TRUA), giờ xuất bếp (giờ giao sớm nhất `CustomerDeliveryLocation.DeliveryWindowFrom` của đơn Đã chốt, dòng thuộc ca − 60′; không có → `Shift.StartTime` − 60′; hoặc nhập tay), mẻ cuối xong trước xuất bếp 30′ (`ProductionPackingMinutes`), mỗi món → 1 lệnh SX `PH-` (Released, công thức chọn như MRP: khóa hiệu lực → khóa đang dùng → Đã duyệt + cảnh báo; không có → chặn; `RecipeSnapshotJson`), chia mẻ ⌈phần ÷ `CapacityMealPerBatch`⌉ (mẻ đầy trước) × `MinutesPerBatch` (trống 60′), các mẻ của món nối tiếp, món song song; mã `ME-YYMMDD-<CA>-NN` (T11, sinh mã đầu rồi cộng dần trong cùng khóa `sp_getapplock`) theo giờ bắt đầu rồi tên món; StartDate / EndDate = giờ kế hoạch; dòng kế hoạch gắn ca → Released; kế hoạch → Released; đơn Đã chốt của ngày (dòng thuộc ca) → `InProduction`. `ExportProductionSheet` (.xlsx). CRUD: Insert / Update → lỗi (tạo bằng MRP, Note là JSON MRP); Delete chỉ Nháp.
- **Dòng kế hoạch (`ProductionPlanLineService`)**: Insert / Update / Delete chỉ khi kế hoạch Nháp, 1 món 1 dòng, số phần > 0, ghi song song PlannedQty.
- **Lệnh SX (`ProductionOrderService` / `Controller`, `prod-order`)**: `GetDetail` (ảnh chụp định lượng, mẻ, phiếu xuất). CRUD: Insert / Delete → lỗi; Update chỉ Note / ResponsibleEmployeeId. Trạng thái tự đổi: Released → InProgress (mẻ đầu bắt đầu) → Completed (mọi mẻ Packed / Failed; ActualQty = Σ số đạt mẻ đóng gói); mọi lệnh xong → kế hoạch Completed.
- **Mẻ (`ProductionBatchService` / `Controller`, `prod-batch`)**: `GetBoard` (mẻ của ngày + statusCounts + phần kế hoạch / đạt), `GetDetail` (nguyên liệu cần / đã xuất, phiếu xuất, nhiệt độ, lưu mẫu, sản lượng, hao hụt, `listBlocker`, `allowedActions`); `CreateMaterialIssue` (form `Id, WarehouseId, Post`; định lượng × phần mẻ ÷ suất chuẩn × (1 + hao hụt %) quy đổi sang đơn vị tồn → `MaterialIssueService.Create` (FEFO / FIFO, chặn lô hết hạn / KHONG_DAT / tạm giữ / thiếu tồn) + `Post` trong 1 transaction; `ProductionOrderId` thật + "Mẻ ME-…" trong Note; 1 phiếu tự động / mẻ); `Start`; `Finish`; `RecordCoreTemperature` (TemperatureLog CORE, Min 70 °C, IsWithinRange ở server, dưới ngưỡng bắt buộc hành động khắc phục, RecordedBy = người thao tác); `RecordSampleRetained` (FoodSafetyChecklistResult SAMPLE_RETAINED, 1 lần / mẻ); `RecordOutput` (ProductionOutput 1 dòng / mẻ, ghi song song cột cũ; số đạt / loại của mẻ); `RecordWaste` (MealWasteRecord, WasteQty mẻ = Σ); `Release` (Chờ QC → Đạt – **chặn** khi nhiệt độ lõi gần nhất < 70 °C / chưa đo, chưa lưu mẫu, chưa ghi sản lượng); `Hold`; `Reject` (lý do; toàn bộ thành hao hụt QC_REJECT); `Pack` (Đạt → Đã đóng gói – kiểm lại nhiệt độ lõi gần nhất ≥ 70 °C); `ExportProductionSheet`. CRUD: Insert / Delete → lỗi; Update chỉ trạm bếp.
- **`ProductionOutput`**: CRUD ghi → lỗi (qua RecordOutput). **`MealWasteRecord`**: dòng gắn mẻ chỉ qua RecordWaste; gỡ header auto-generated.
- **`ProductionRules`** (lớp tĩnh): chọn công thức, ảnh chụp, lịch ngược `Schedule`, giờ xuất bếp, nguyên liệu mẻ, điều kiện ATTP, tóm tắt lệnh / mẻ, mẫu in Phiếu sản xuất (bố cục theo tiêu đề file thật: tên pháp nhân, Số suất order / thực tế, PHIẾU SẢN XUẤT, Thứ – ngày; Buổi | Tên món | Tên NL | ĐVT | Số lượng (Định mức / SL cần / Thực xuất / Xuất thêm / Lý do) | SC ký nhận | Sơ chế (SL sau SC / Tiêu hao / Trả về) | Bếp ký nhận | Ghi chú – chỉ đọc tiêu đề, không chép dữ liệu).
- Cấu hình `BusinessRules` (PROPOSED): `ProductionDefaultShiftCode` TRUA, `ProductionPackingMinutes` 30, `ProductionDefaultMinutesPerBatch` 60, `ProductionRequireSampleBeforeRelease` true (dùng sẵn `TransportMinutesBeforeCutoff` 60, `CoreTempMinC` 70).
- Hằng `Model/Helper/ProductionStatus.cs` (ProductionOrderStatus, ProductionBatchStatus, BatchQcStatus, ProductionCode); request `Model/Request/Custom/ProductionCustomRequest.cs`; model `Model/Model/ProductionModels.cs`; thông báo tiếng Việt trong `ConstantHelper.Message`.
- Seed mới `DB/seed/FoodSafetyChecklist_seed.sql` (bộ `PRD_BATCH`: SAMPLE_RETAINED, CORE_TEMP – cấu hình PROPOSED, chỉ thêm khi chưa có) – **đã chạy trên DB dev 2 lần** (lần 2 không thêm dòng).
- Quyền theo `PermissionConvention`: Get* = View; `CreateMaterialIssue` = Add; `Export*` = Export; còn lại Edit (`prod-plan`, `prod-order`, `prod-batch`); `ProductionPlan/Approve` = Approve `prod-plan` (khai báo rõ). Không thêm mục menu / seed quyền / SP.

File chính: `Services/Implement/{ProductionRules, ProductionPlanService, ProductionPlanLineService, ProductionOrderService, ProductionBatchService, ProductionOutputService, MealWasteRecordService}.cs`, `Services/Interface/I{ProductionPlan, ProductionOrder, ProductionBatch}Service.cs`, `WebApi/Controllers/{ProductionPlan, ProductionOrder, ProductionBatch}Controller.cs`, `Model/Helper/{ProductionStatus, ConstantHelper}.cs`, `Model/Option/BusinessRulesOptions.cs`, `Model/Request/Custom/ProductionCustomRequest.cs`, `Model/Model/ProductionModels.cs`, `WebApi/appsettings.json`, `DB/seed/FoodSafetyChecklist_seed.sql`, test `Tests/Production/{ProductionTests, ProductionSqlServerTests}.cs`, tài liệu `docs/frontend-handoff/P7.md`, `docs/api-tests/p7-production.http`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **148/148** xanh (+5):
  - `Production/ProductionTests` (4, InMemory): lịch ngược tính tay (`Schedule`: 250 phần / 100 / 60′ / xong 10:00 → 07–08, 08–09, 09–10 (50)); phát hành: giao 11:30 → xuất bếp 10:30 → 4 mẻ `ME-261221-<CA>-01…04` đúng giờ / số phần / thứ tự (Thịt#1, Thịt#2, Canh, Thịt#3), lệnh `PH-261220-NNNN` Released + ảnh chụp, đơn → InProduction, dòng kế hoạch gắn ca; CRUD cũ bị chặn (kế hoạch, dòng kế hoạch đã phát hành, mẻ, lệnh, sản lượng, hao hụt gắn mẻ; Update mẻ không đổi trạng thái); in Phiếu sản xuất mở được (SL cần 13,2 kg); xuất NL cho mẻ: 13,2 kg = L1 10 + L2 3,2 (FEFO), bỏ lô hết hạn 50 kg + KHONG_DAT 50 kg, phiếu gắn lệnh SX thật + số mẻ, 1 phiếu / mẻ, mẻ 50 phần cần 6,6 chỉ còn 3,6 xuất được → chặn, không ghi phiếu, lô bị chặn không có bút toán xuất; ATTP: 65 °C không khắc phục → 400, có khắc phục → lưu + cảnh báo, Release bị chặn (65 °C + chưa lưu mẫu + chưa sản lượng), đo lại 75,5 °C → còn chặn vì chưa lưu mẫu, lưu mẫu (1 lần) → Passed; đo lại 68 °C → Pack bị chặn, 72 °C → Packed; sổ nhiệt độ 4 dòng đúng IsWithinRange / Min 70 / người ghi; sản lượng 80 / đạt 78 / loại 2, hao hụt 3; lệnh Canh Completed (ActualQty 78); 3 mẻ Thịt Reject (lý do bắt buộc) → QC_REJECT ×3 → kế hoạch Completed; GetBoard; in phiếu mẻ (Rau 4 kg).
  - `Production/ProductionSqlServerTests` (1, SQL Server dev): kế hoạch → Approve → Release (PH- + ME- qua sp_getapplock, xuất bếp 10:30 → mẻ 09:00) → CreateMaterialIssue Post (5 kg từ lô còn hạn, lô hết hạn 30 kg không đụng) → Start → nhiệt độ 74,5 → lưu mẫu → Finish → sản lượng 50 / loại 1 → Release → Pack → lệnh + kế hoạch Completed – trong transaction ngoài rồi rollback, không để lại dữ liệu (đã chạy, không bị bỏ qua).
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không sửa `MaterialRequirementService`.

AC (T17, bản Pilot):
- [x] Lập kế hoạch đúng giờ (lịch ngược từ giờ xuất bếp, tính tay được) + chia mẻ theo dây chuyền + mã `ME-YYMMDD-<CA>-NN`.
- [x] Mẻ < 70 °C bị chặn đạt QC / đóng gói (lần đo gần nhất; đo lại đạt mới qua).
- [x] Mẻ chưa lưu mẫu không được đạt QC (PROPOSED, cấu hình `ProductionRequireSampleBeforeRelease`).
- [x] Xuất NL cho mẻ dùng FEFO, chặn lô hết hạn / KHONG_DAT; phiếu xuất gắn lệnh SX thật.
- [x] Sản lượng / hao hụt (ProductionOutput / MealWasteRecord); in Phiếu sản xuất (.xlsx).
- [x] 1 test SQL Server rollback cho plan → batch → issue → start → temp → sample → finish → output (→ release → pack).
- [ ] "Không chuyển công đoạn khi công đoạn trước chưa xong" (công đoạn theo tổ) → DEFERRED(PILOT).
- [ ] `DELICARE_API_Catalog.xlsx` không sửa (theo điều phối). Gợi ý cột J / L (M = T17): 8.1 GET plans = Đã xong (`ProductionPlan/GetAll`, `GetDetail`); generate = Một phần (MRP `MaterialRequirement/Calculate` tạo KH- theo ngày); lines = Đã xong (`ProductionPlanLine/Insert / Update / Delete`, chỉ Nháp); approve = Đã xong (`ProductionPlan/Approve`); release = Đã xong (`ProductionPlan/Release`, chưa giữ chỗ). 8.2 GET orders = Đã xong (`ProductionOrder/GetAll`, `GetDetail`); complete = Đã xong (tự động khi đóng gói / loại hết mẻ); close / cancel = Chưa bắt đầu. 8.3 batch-board / batches/{id} = Đã xong (`ProductionBatch/GetBoard`, `GetDetail`); start / finish = Đã xong; qc = Đã xong (`Release` / `Hold` / `Reject`); create-packing-order = Một phần (`Pack`, chưa sinh PackingOrder). 8.4 outputs = Đã xong (`ProductionBatch/RecordOutput` + `ProductionOutput/GetAll`); waste-records = Một phần (`ProductionBatch/RecordWaste`, MealWasteRecord CRUD; summary chưa). 9.3 temperature-logs = Một phần (+ CORE của mẻ).

DEFERRED(PILOT):
- Công đoạn theo tổ (Sơ chế → Nấu → Cơm / Canh → Chia suất, người làm / tổ, API tiến độ bếp theo tổ).
- Nhiều ca / ngày (tách kế hoạch theo `OrderLine.ShiftId`), phân món → dây chuyền theo nhóm món, nhiều dây chuyền song song, phiên bản kế hoạch (GAP-PRD-02).
- Giữ chỗ `MaterialReservation` khi phát hành; quy đổi đơn vị khi xuất ngoài định lượng; xuất thêm / trả về NL cho mẻ (dùng `MaterialIssue/Create` / `Cancel`).
- Đóng / hủy lệnh SX, hủy mẻ, `PackingOrder` khi đóng gói (T19 / T47B.2), giá thành thực tế (spec 8.5), báo cáo hao hụt (summary), checklist ATTP theo ca (T18).

Câu hỏi cho anh (DECISIONS GAP-PRD-01, 03…08):
1. Pilot 1 ca: phát hành gán cả kế hoạch ngày cho 1 ca (mặc định TRUA) – được không? Khi chạy nhiều ca có cần MRP tách kế hoạch theo ca không?
2. Giờ xuất bếp = giờ giao sớm nhất của điểm giao (`DeliveryWindowFrom`) − 60′; mẻ cuối xong trước 30′ để chia suất / đóng gói; các món nấu song song – đúng thực tế bếp? Mẻ đầy trước, mẻ cuối phần lẻ – hay chia đều?
3. Ngưỡng nhiệt độ lõi 70 °C (BUSINESS_RULES) hay 75 °C (spec 8.3)? Đo lại sau khi đạt mà < 70 °C thì chặn đóng gói – đúng ý anh?
4. Bắt buộc lưu mẫu trước khi mẻ "Đạt" (đề xuất) – hay chỉ trước khi giao?
5. Xác nhận kế hoạch (Approve) bấm 1 cấp với quyền mục "Duyệt – Kế hoạch sản xuất" – có cần qua hộp duyệt (ApprovalRequest) như PO không?
6. Phát hành kế hoạch chuyển đơn hàng Đã chốt → Đang sản xuất – đúng không? Lệnh SX dùng mã `PH-` (BUSINESS_RULES §11) – được chứ?

Cần anh: chạy `DB/seed/FoodSafetyChecklist_seed.sql` trên staging (DB dev đã chạy); khai báo `ProductionLine` (năng lực mẻ, phút / mẻ) và giờ giao của điểm giao trước khi chạy thử.

### T17.1 – Sản xuất 4 ca / ngày + chia mẻ đều (anh chốt 2026-11-16) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T17.1-shifts` (từ `release/pilot`). Chỉ backend + tài liệu. Không bảng / cột / SP / seed mới.

Anh chốt 2026-11-16 (DECISIONS mục "Anh chốt 2026-11-16 (T17)"; GAP-PRD-01 / 06 / 07 đánh dấu đã chốt, GAP-PRD-08 phần lưu mẫu = D-42, GAP-PRD-05 giữ nguyên): Pilot 4 ca / ngày, KH- vẫn theo ngày, phát hành tách theo ca; lịch giữ như T17; chia mẻ đều; duyệt 1 cấp; phát hành → đơn Đang SX; lệnh SX `PH-`.

Đã làm (trong file cũ, đúng mẫu 4.2):
- **`ProductionRules.SplitByShiftAsync`** (mới, lớp tĩnh): chia số phần từng dòng kế hoạch theo ca = tỉ lệ suất của món trong ca, tính như MRP (T14 / T14.1: dòng đơn Đã chốt / Đang SX × MenuLine cùng loại suất + thứ, chia đều lựa chọn + `MrpOptionBufferPct`); ca = `OrderLine.ShiftId`, chưa gán → `ProductionDefaultShiftCode` (cảnh báo); món không có trong đơn → ca mặc định; làm tròn 4 số lẻ, ca cuối nhận phần còn lại (tổng = dòng kế hoạch). Ca sắp theo `StartTime`.
- **`ProductionRules.Schedule`**: chia mẻ đều – N = ⌈phần ÷ năng lực⌉, phần nguyên chia đều, các mẻ đầu nhận phần dư, phần lẻ thập phân vào mẻ cuối (250 / 100 → 84, 83, 83; 57,5 / 50 → 29, 28,5).
- **`ProductionRules.ResolveDispatchAsync`**: thêm `fallbackShiftId`; dòng chưa gán ca thuộc ca mặc định (không còn tính cho mọi ca); đơn có dòng ở ≥ 2 ca không dùng khung giờ điểm giao (PROPOSED).
- **`ProductionPlan/Release`**: tách theo ca – `ShiftId` = chỉ ca đó (ca đã phát hành → 400 "đã phát hành", ca không có suất → 400), trống = mọi ca chưa phát hành; `DispatchTime` nhập tay chỉ khi phát hành 1 ca. Mỗi ca: giờ xuất bếp riêng → lệnh SX `PH-` / món (`ShiftId`, `PlannedQty` = phần của ca) + mẻ `ME-yyMMdd-<ca>-NN` (số mẻ riêng từng ca). Ca đã phát hành = có lệnh SX (chưa hủy) của kế hoạch gắn ca. Kế hoạch giữ Đã duyệt tới khi mọi ca có nhu cầu đã phát hành → Đã phát hành; dòng kế hoạch Released khi mọi ca của món đã phát hành (`ShiftId` chỉ khi món thuộc 1 ca); đơn Đã chốt có dòng thuộc ca vừa phát hành → `InProduction`. Hoàn thành kế hoạch (ProductionBatchService) vốn chỉ khi kế hoạch Released → không đổi.
- **`ProductionPlan/GetDetail`**: `listShift[]` (`shiftId, shiftCode, shiftName, startTime, portions, released, dispatchAt, orderCount, batchCount`), `listLine[].listShift[]` (`shiftId, shiftCode, quantity`); `allowedActions` Đã duyệt = `release` (còn ca) + `exportProductionSheet` (đã có lệnh SX); `listWarning` dòng đơn chưa gán ca.
- **`ProductionBatch/GetDetail`**: mẻ Đã đóng gói chưa lưu mẫu → `allowedActions` có `recordSampleRetained` (D-42).
- **Giao (`DeliveryRules.OrderShiftIdsAsync`)**: ca của đơn trên chuyến – dòng chưa gán ca → ca mặc định (cùng quy tắc phát hành); dùng cho `DeliveryRoute/Create` (cảnh báo) và `Dispatch` / chi tiết chuyến. Kiểm mẻ vẫn theo ngày + ca (T19).
- Thông báo mới `ConstantHelper.Message.Release{LinesWithoutShift, ShiftAlready, ShiftNoDemand, NothingPending, DispatchNeedsShift}`; model `ProductionPlanShiftModel`, `ProductionPlanLineShiftModel`; chú thích `BusinessRulesOptions.ProductionDefaultShiftCode`, `ProductionPlanReleaseRequest`.

File: `Services/Implement/{ProductionRules, ProductionPlanService, ProductionBatchService, DeliveryRules, DeliveryRouteService}.cs`, `Model/Model/ProductionModels.cs`, `Model/Helper/ConstantHelper.cs`, `Model/Option/BusinessRulesOptions.cs`, `Model/Request/Custom/ProductionCustomRequest.cs`, test `Tests/Production/{ProductionShiftTests (mới), ProductionTests, ProductionSqlServerTests}.cs`, tài liệu `docs/claude/{DECISIONS, BUSINESS_RULES, PROGRESS}.md`, `docs/frontend-handoff/P7.md`, `docs/api-tests/p7-production.http`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 10 cảnh báo (≤ 11 baseline).
- `dotnet test`: **169/169** xanh (+2, baseline 167):
  - `ProductionShiftTests.Release_splits_the_daily_plan_by_shift…`: đơn TRUA 250 (giao 11:30) + TOI 100 (giao 17:30), kế hoạch Thịt 350 / Canh 80 → listShift TRUA 330 / TOI 100, Thịt 250 + 100; `DispatchTime` khi còn 2 ca → 400; phát hành TOI → 1 mẻ `ME-261221-TOIS1-01` 100 phần 15:00–16:00, xuất bếp 16:30, kế hoạch vẫn Approved (`release` + `exportProductionSheet`), đơn TOI InProduction, đơn TRUA còn Locked; phát hành lại TOI → 400; phát hành phần còn lại → TRUA `ME-261221-TRUAS1-01…04` 84 / 83 / 80 (Canh) / 83, 07:00–10:00; kế hoạch Released; 3 lệnh SX `PH-` khác số (TOI Thịt 100, TRUA Thịt 250, Canh 80); dòng Thịt Released không gắn ca, Canh gắn TRUA.
  - `ProductionShiftTests.Dispatch_of_an_evening_route…`: phát hành cả ngày 1 lần; mẻ TOI đóng gói chưa lưu mẫu → `allowedActions` có `recordSampleRetained`; chuyến TOI (đơn TOI) chỉ liệt kê mẻ TOI, xuất phát bị chặn chỉ vì mẻ TOI chưa lưu mẫu (không nhắc mẻ TRUA); lưu mẫu → xuất phát được trong khi 4 mẻ TRUA còn Kế hoạch.
  - Sửa test T17: lịch tính tay 84 / 83 / 83 (+ 300 → 100×3, 201 → 67×3, 57,5 / 50 → 29 / 28,5); phát hành P1 mẻ 84 / 83 / 80 / 83, Phiếu sản xuất SL cần 11,088; xuất NL mẻ 84 phần = L1 10 + L2 1,088, mẻ 83 cuối cần 10,956 > 7,956 còn → chặn; seed thêm thực đơn cho đơn (phát hành chia theo ca cần dòng đơn × thực đơn). SQL Server test: seed thêm đơn Locked + thực đơn, kiểm `listShift` + đơn → InProduction (đã chạy, không skip).
- `git status`: không thay đổi trong `app-delicare-client/`, `docs/reference/`.

PROPOSED (chờ anh xác nhận – DECISIONS "Anh chốt 2026-11-16 (T17)" mục PROPOSED):
1. Số phần món theo ca = dòng kế hoạch chia theo tỉ lệ suất của món trong từng ca (tính như MRP); món không có trong đơn → ca mặc định.
2. Kế hoạch giữ Đã duyệt tới khi mọi ca có nhu cầu đã phát hành; "ca đã phát hành" suy ra từ lệnh SX (không cột trạng thái theo ca).
3. Giờ xuất bếp nhập tay chỉ khi phát hành 1 ca.
4. Đơn có dòng ở ≥ 2 ca không dùng khung giờ điểm giao (1 điểm giao chỉ có 1 khung giờ) → giờ bắt đầu ca − 60′.
5. Chuyến có đơn chưa gán ca → kiểm mẻ của ca mặc định.
6. Mã ca bỏ ký tự đặc biệt trong mã mẻ (`SANG_6H` → `SANG6H`, `DEM_23H` → `DEM23H`).

### T19 – Giao + đối soát + phản ánh (Pilot P8) · 2026-11-16 · DONE(PILOT)
Nhánh `feat/T19-delivery` (worktree `wt-T17`, trên T17). Chỉ backend (D-40).

Kế hoạch:
1. Rà soát DeliveryRoute / DeliveryStop / DeliveryOrder / DeliveryEvent / ProofOfDeliverie / OrderReconciliation / CustomerComplaint / ComplaintHistory / Vehicle / TemperatureLog → LEGACY_REVIEW mục (h).
2. Lập chuyến `CX-` (đơn giao `PH-` + điểm dừng) từ đơn Đang sản xuất của ngày / ca.
3. Nhiệt độ giao ≥ 60 °C (TemperatureLog DELIVERY + DeliveryEvent TEMP_CHECK), xuất phát (chặn nhiệt độ, mẻ chưa đóng gói, mẻ chưa lưu mẫu – D-42).
4. Đến điểm / bàn giao + POD (nhiệt độ bàn giao) / thất bại; đơn hàng → Đã giao; chuyến hoàn thành.
5. Đối soát suất thực tế phục vụ → OrderReconciliation, đóng → đơn Đã đối soát.
6. Phản ánh `PA-` cơ bản (tạo / giao xử lý / đã xử lý / đóng / từ chối / ghi chú).
7. Chặn CRUD cũ phá máy trạng thái; test InMemory + 1 test SQL Server rollback; tài liệu.

Rà soát: 10 bảng chỉ có CRUD sinh mã (tạo chuyến / đơn giao không số, đổi trạng thái tùy ý, POD / đối soát nhập tay, `DeliveryEvent/Insert` ghi được nhiệt độ "đạt" giả, phản ánh không số / không SLA). Mã CX / PA / PH (DeliveryOrder) đã có trong `DocumentNumberService` (T11). Chi tiết: LEGACY_REVIEW (h).

**Anh chốt 2026-11-16 (D-42, điều phối chuyển):** lưu mẫu bắt buộc **trước khi giao**, không phải trước khi đạt QC → `BusinessRules:ProductionRequireSampleBeforeRelease` mặc định `false` (appsettings + Options); `ProductionBatch/Release` khi chưa lưu mẫu chỉ **cảnh báo**; `ProductionBatch/RecordSampleRetained` nhận thêm mẻ Đã đóng gói (để bổ sung mẫu trước khi xuất chuyến). Chỉ sửa đúng 2 chỗ này trong `ProductionBatchService` + kỳ vọng test T17 (`ProductionTests`: đạt QC khi chưa lưu mẫu → Passed + cảnh báo, ghi mẫu sau).

Đã làm (đều trong file cũ, đúng mẫu 4.2; thêm 1 lớp tĩnh dùng chung `DeliveryRules`, không có service / controller mới):
- **Chuyến (`DeliveryRouteService` / `DeliveryRouteController`, `log-route`)**: `GetBoard` (chuyến của ngày, tải / % tải, statusCounts), `GetReadyOrders`, `GetDetail` (điểm dừng + POD, sự kiện, mẻ của ngày / ca + đã lưu mẫu, `listBlocker`, `allowedActions`), `Create` (JSON – có danh sách đơn: xe hợp lệ (không Maintenance / Inactive), tài xế = Employee, đơn Đang sản xuất của ngày + dòng thuộc ca, chưa có đơn giao còn hiệu lực; số `CX-YYMMDD-<mã xe>-NN`; mỗi đơn 1 đơn giao `PH-` (DeliveryNumber + DeliveryNo) + 1 điểm dừng, giờ dự kiến = `DeliveryWindowFrom`, thứ tự theo giờ; giờ xuất phát kế hoạch = giờ sớm nhất − 60′; cảnh báo vượt tải / xe bận / sản xuất chưa sẵn sàng), `RecordTemperature` (xuất phát hoặc tại điểm), `Dispatch` (chặn khi nhiệt độ xuất phát gần nhất < 60 °C / chưa đo, mẻ của ngày / ca chưa Đã đóng gói, mẻ chưa lưu mẫu – thông báo liệt kê số `ME-`; có nhiệt độ dưới ngưỡng kèm khắc phục → vẫn lưu lần đo, trả 400), `ExportDeliveryNote` (.xlsx). CRUD: Insert → lỗi; Update chỉ tên / mô tả / tài xế / giờ khi Kế hoạch; Delete chỉ Kế hoạch chưa có sự kiện (xóa mềm kèm điểm dừng + đơn giao).
- **Điểm dừng (`DeliveryStopService` / `DeliveryStopController`, `log-stop`)**: `Arrive`, `Deliver` (nhiệt độ bàn giao gần nhất tại điểm ≥ 60 °C; POD giao / nhận / từ chối, người nhận, đường dẫn chữ ký / ảnh; điểm Đã giao + DeliveredMealQty + ShortageQty; đơn giao Đã giao; đơn hàng → `Delivered` khi mọi đơn giao còn hiệu lực đã giao; suất bị từ chối → phản ánh `PA-` POD / SHORTAGE tự động; mọi điểm xong → chuyến Completed), `Fail` (lý do; đơn giao Failed, đơn hàng giữ InProduction để xếp chuyến bù). CRUD: Insert / Delete → lỗi; Update chỉ thứ tự / giờ dự kiến / ghi chú khi Kế hoạch.
- **Đối soát (`OrderReconciliationService` / `Controller`, `crm-reconciliation`)**: `GetPendingOrders`, `GetDetail`, `GetByOrder`, `Reconcile` (đơn Đã giao có POD; suất phục vụ ≤ Σ suất nhận POD, phục vụ + trả về + hủy ≤ nhận; Ordered / Delivered / Accepted (= phục vụ) / Variance / VariancePercent / Status Khớp – Chênh lệch / ProofOfDeliveryId / FinalAmount = giá trị đơn × suất tính tiền ÷ suất đặt theo `ReconciliationBillingBasis` = SERVED; ghi song song cột cũ; trả về / hủy / suất nhận / cơ sở tính tiền → JSON trong `Note`), `Close` (|chênh| > 1% bắt buộc ghi chú; đơn hàng → `Reconciled`). CRUD → lỗi.
- **Phản ánh (`CustomerComplaintService` / `Controller`, `log-complaint`)**: `Create` (`PA-YYMMDD-NNNN`, mức độ High / Medium / Low, `SlaDueAt` = lúc nhận + 4 / 24 / 72 giờ, liên kết đơn / điểm dừng / đơn giao / chuyến cùng khách, mẻ ghi vào mô tả), `Assign`, `Resolve`, `Close`, `Reject`, `AddComment`, `GetDetail` (lịch sử, `isOverdue`). Mọi bước ghi `ComplaintHistory`. CRUD: Insert / Delete → lỗi; Update chỉ phân loại / kênh / mô tả / mức độ khi Mới / Đang xử lý.
- **Chặn CRUD khác**: `DeliveryOrder` (Insert / Delete → lỗi, Update chỉ ghi chú), `ProofOfDelivery` (Insert / Update / Delete → lỗi), `DeliveryEvent` / `ComplaintHistory` Insert → lỗi (sổ chỉ ghi thêm, ghi qua action). Gỡ header auto-generated ở các file đã sửa tay.
- **`DeliveryRules`** (lớp tĩnh): ghi nhiệt độ giao (TemperatureLog DELIVERY + DeliveryEvent TEMP_CHECK, `ExternalEventId = TemperatureLog:<Id>`), lần đo gần nhất (cùng giây → thấp nhất), mẻ phục vụ chuyến + đã lưu mẫu, điều kiện xuất phát, hạn SLA, tạo phản ánh, chi tiết chuyến, mẫu in Biên bản giao hàng.
- Cấu hình `BusinessRules` (PROPOSED): `DeliveryRequirePackedBatches` true, `DeliveryRequireSampleRetained` true (D-42), `ReconciliationBillingBasis` SERVED, `ReconciliationVarianceRemarkPct` 1, `DeliveryAutoComplaintOnRejected` true (dùng sẵn `DeliveryTempMinC` 60, `ComplaintSlaHours`, `TransportMinutesBeforeCutoff`).
- Hằng `Model/Helper/DeliveryStatus.cs`; request `Model/Request/Custom/DeliveryCustomRequest.cs`; model `Model/Model/DeliveryModels.cs`; thông báo tiếng Việt trong `ConstantHelper.Message`.
- Quyền theo `PermissionConvention` (Get* = View, `Create` = Add, `Export*` = Export, còn lại Edit) trên `log-route`, `log-stop`, `crm-reconciliation`, `log-complaint` (ScreenMap + ObjectMenu đã có). **Không thêm SP, seed, mục menu.**

File chính: `Services/Implement/{DeliveryRules, DeliveryRouteService, DeliveryStopService, DeliveryOrderService, DeliveryEventService, ProofOfDeliveryService, OrderReconciliationService, CustomerComplaintService, ComplaintHistoryService, ProductionBatchService}.cs`, `Services/Interface/I{DeliveryRoute, DeliveryStop, OrderReconciliation, CustomerComplaint}Service.cs`, `WebApi/Controllers/{DeliveryRoute, DeliveryStop, OrderReconciliation, CustomerComplaint}Controller.cs`, `Model/Helper/{DeliveryStatus, ConstantHelper}.cs`, `Model/Option/BusinessRulesOptions.cs`, `Model/Request/Custom/DeliveryCustomRequest.cs`, `Model/Model/DeliveryModels.cs`, `WebApi/appsettings.json`, test `Tests/Delivery/{DeliveryTests, DeliverySqlServerTests}.cs` (+ sửa `Tests/Production/ProductionTests.cs`), tài liệu `docs/frontend-handoff/P8.md`, `docs/api-tests/p8-delivery.http`, BUSINESS_RULES §6 / §7 / §9, LEGACY_REVIEW (h), DECISIONS D-42 + GAP-DLV-01…07.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **152/152** xanh (+4):
  - `Delivery/DeliveryTests` (3, InMemory): luồng đủ – chờ xếp chuyến → lập `CX-261221-XED1-01` (điểm #1 B 11:00 40 suất, #2 A 11:30 60 suất, PH- liên tiếp, xuất phát kế hoạch 10:00, tải 100 / 120 = 83,33%), đơn đã có chuyến không xếp lại; xuất phát bị chặn (chưa đo + "Mẻ chưa lưu mẫu thức ăn (bắt buộc trước khi giao): ME-261221-D1-02"); 55 °C không khắc phục → 400 không ghi; 55 °C có khắc phục → lưu TemperatureLog DELIVERY (IsWithinRange = false, Min 60, người ghi) + sự kiện trỏ TemperatureLog, vẫn chặn; lưu mẫu mẻ Đã đóng gói + đo 65 °C → Đang giao; bàn giao 58 °C → lưu lần đo, chặn; 62 °C → B Đã giao (đơn B Delivered); A chưa đo tại điểm → chặn, nhận > giao → chặn; 63 °C giao 58 / nhận 56 → thiếu 2, từ chối 2 → phản ánh PA- POD / SHORTAGE, chuyến Completed; in biên bản mở được; đối soát A (phục vụ 57 > 56 → chặn; 50 + 4 + 3 > 56 → chặn; 50 / 4 / 2 → chênh −10, −16,67%, 1.500.000, Variance), đóng thiếu ghi chú → chặn, có ghi chú → Closed, đơn A Reconciled; B phục vụ 40 → Matched 1.200.000, đóng không cần ghi chú. Mẻ chưa đóng gói ("ME-261221-D2-02 (Đạt)") chặn xuất phát; CRUD cũ (DeliveryRoute / DeliveryEvent / ProofOfDelivery / OrderReconciliation / CustomerComplaint / ComplaintHistory Insert) → 400; xóa chuyến có lần đo → chặn, chuyến chưa đo → xóa được, đơn trở lại chờ xếp chuyến; chuyến thứ 2 cùng xe số `-02`. Phản ánh: mức độ sai / đơn khác khách → 400; tạo `PA-261221-NNNN` High → hạn +4 giờ, mẻ trong mô tả; Resolve khi Mới → 400; Assign → AddComment → Resolve (bắt buộc nội dung) → Reject khi Đã xử lý → 400 → Close; lịch sử CREATED / ASSIGNED / COMMENT / RESOLVED / CLOSED; Update khi Đóng → 400; LOW nhận cách 80 giờ → quá hạn, từ chối → hết quá hạn.
  - `Delivery/DeliverySqlServerTests` (1, SQL Server dev): chuyến `CX-991229-<XE>-NN` + đơn giao `PH-991229-NNNN` (sp_getapplock) → đo 57,5 °C (khắc phục) → xuất phát bị chặn → xuất phát 65 °C → đến điểm → bàn giao 64 °C + POD → chuyến Completed → đối soát 48 / 50 (−4%, 1.440.000) → đóng (đơn Reconciled) → phản ánh PA- tạo / giao / xử lý / đóng (4 dòng lịch sử) – trong transaction ngoài rồi rollback, không để lại dữ liệu (đã chạy, không bị bỏ qua).
  - `Production/ProductionTests` sửa theo D-42 (đạt QC khi chưa lưu mẫu → Passed + cảnh báo).
- `git status`: không có thay đổi trong `app-delicare-client/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không sửa PurchaseOrder* / GoodsReceipt* / QcInspection*, `ApprovalRequestService`, `IApprovalHandler`, seed ObjectMenu / UserRole, User / Person / Login, PermissionFilter.
- Script SQL: **không có** SP / seed mới (dùng SP `usp*_GetAll` sẵn có). Không chạy script nào lên DB dev.

AC (T19, bản Pilot):
- [x] Nhiệt độ giao < 60 °C chặn xuất phát và bàn giao (lần đo gần nhất; dưới ngưỡng bắt buộc khắc phục, vẫn lưu sổ).
- [x] Mẻ chưa lưu mẫu chặn xuất phát, thông báo liệt kê số ME- (D-42); mẻ chưa đóng gói chặn xuất phát.
- [x] Định dạng `CX-YYMMDD-<XE>-NN`, `PA-YYMMDD-NNNN` (+ `PH-` đơn giao).
- [x] Đối soát tính tay được (đặt / giao / nhận / phục vụ / trả về / hủy, chênh lệch, giá trị); đơn → Đã giao → Đã đối soát.
- [x] Vòng đời phản ánh (tạo / giao xử lý / đã xử lý / đóng / từ chối / ghi chú, lịch sử, hạn SLA, quá hạn khi xem).
- [x] 1 test SQL Server rollback: chuyến → xuất phát → bàn giao / POD → đối soát (+ phản ánh).
- [ ] Cảnh báo SLA tự động / leo thang, báo cáo suất theo điểm / ngày / buổi → DEFERRED(PILOT).
- [ ] `DELICARE_API_Catalog.xlsx` không sửa (theo điều phối). Gợi ý cột J / L (M = T19): 10.2 GET routes = Đã xong (`DeliveryRoute/GetBoard`, `GetDetail`); routes/plan = Một phần (`DeliveryRoute/Create`, 1 xe / lần, không chia khu vực); POST/PUT routes = Một phần (`Create`, `Update` khi Kế hoạch); routes/{id}/stops = Một phần (`DeliveryStop/Update`); load = Chưa bắt đầu; dispatch = Đã xong (`DeliveryRoute/Dispatch`); complete = Đã xong (tự động khi mọi điểm xong). Delivery-orders thủ công / split / route / cancel = Chưa bắt đầu (GET = CRUD `DeliveryOrder/GetAll`). 10.3 executions GET = Một phần (`DeliveryRoute/GetDetail`); arrive = Đã xong (`DeliveryStop/Arrive`); pod = Đã xong (`DeliveryStop/Deliver`, chưa ghi PackagingLedger); fail = Đã xong (`DeliveryStop/Fail`); events = Một phần (`GetDetail.listEvent`, `DeliveryEvent/GetAll`). 10.5 complaints GET = Một phần (CRUD GetAll; `isOverdue` ở GetDetail); POST = Đã xong (`CustomerComplaint/Create`); transition = Đã xong (`Assign` / `Resolve` / `Close` / `Reject`); history = Đã xong (`GetDetail.listHistory`). 6.7 reconciliations GET = Một phần (`GetPendingOrders`, `GetAll`; chưa có chart); reconcile = Đã xong (`OrderReconciliation/Reconcile`); close = Đã xong (`Close`). In biên bản giao nhận = Một phần (`DeliveryRoute/ExportDeliveryNote`).

DEFERRED(PILOT):
- Xếp hàng lên xe (`DeliveryLoading`, trạng thái Loading), `DeliveryExecution`, PackingOrder / PackingResult khi đóng gói, PackagingLedger OUT / RETURN (T20).
- Lập chuyến nhiều xe / theo khu vực, tách / chuyển / hủy đơn giao tay, 1 đơn nhiều điểm giao (GAP-ORD-01).
- Cảnh báo / leo thang SLA phản ánh và cảnh báo nhiệt độ tự động (T23), báo cáo suất theo điểm / ngày / buổi, biểu đồ Đặt – Giao – Nhận (spec 6.7 chart), KPI giao đúng giờ.
- Gửi Zalo cho khách khi xuất phát; tọa độ GPS liên tục.
- `ProducedQty` / `PackedQty` trong đối soát (cần gắn mẻ ↔ đơn).
- `allowedActions` của mẻ Đã đóng gói chưa có `recordSampleRetained` (API đã nhận) – để T17.1 bổ sung.

Câu hỏi cho anh (DECISIONS D-42, GAP-DLV-01…07):
1. Xuất phát bị chặn khi **còn mẻ của ngày / ca chưa đóng gói xong** – đúng thực tế (hay cho xe chạy khi đủ món của các điểm trên chuyến)?
2. Nhiệt độ: đo **1 lần lúc xuất phát** (cấp chuyến) + **1 lần mỗi điểm khi bàn giao** – đủ chưa, hay cần đo theo từng thùng / món?
3. Bàn giao dưới 60 °C: hiện **không cho bàn giao** (phải báo thất bại để giao bù) – hay cho khách nhận kèm biên bản?
4. Đối soát tính tiền theo **suất thực tế phục vụ** (spec 6.7) – hay theo suất đặt / số tối thiểu hợp đồng / max(đặt, phục vụ)? Cấu hình `ReconciliationBillingBasis` đổi được.
5. Suất bị từ chối khi bàn giao có tự tạo phản ánh (mức Trung bình) không?
6. Mẫu "Biên bản giao hàng" thật (nếu có) để em sửa file in.

Cần anh: không có script SQL mới; khai báo `Vehicle.Code` (mã ngắn dùng trong số chuyến), tài xế (Employee) và `DeliveryWindowFrom` của điểm giao trước khi chạy thử.

### T50R1 (phần Pilot) + T48.1 + phần còn lại của T-MERGE (Pilot P9) · 2026-11-17 · DONE(PILOT)
Nhánh `feat/T50R1-pilot-flow` (từ `release/pilot`). Backend + tài liệu + deploy staging; không đụng `app-delicare-client/src`, `docs/reference/*`.

Kế hoạch:
1. T-MERGE còn lại: `DB/README.md` mục "Pilot" (SP / seed theo thứ tự), `Deploy-Database.ps1` + `db-init.sh` đủ seed, chạy lại trên DB dev bằng `sqlcmd -b -I -f 65001`.
2. `docs/api-tests/pilot-flow.http` – 1 luồng P3 → P8, 1 bộ `[DEMO]`, có các chốt an toàn; test tự động qua HTTP (transaction ngoài rồi rollback).
3. Chạy API thật trên DB dev, chạy luồng; lỗi tìm được → sửa + test.
4. `UAT_CHECKLIST.md` mục PILOT; `docs/frontend-handoff/README.md`.
5. T48.1: deploy staging (DB + backend + Angular), smoke test.

Đã làm:
- **DB / deploy:** `DB/README.md` mục "Pilot" (11 script theo thứ tự chạy, so với `9d03b40`). `deploy/db/Deploy-Database.ps1` **bị hỏng từ T48.0** (đoạn băm mật khẩu admin bị chèn lặp 3 lần – chuỗi `'PBKDF2$100000…` không đóng, file không chạy được) → viết lại đoạn cuối, kiểm cú pháp bằng PowerShell parser, chạy thật trên staging; thêm `FoodSafetyChecklist_seed.sql` vào danh sách seed của `Deploy-Database.ps1` và `deploy/docker/db-init.sh`. DB dev: chạy lại 4 SP + 6 seed Pilot theo thứ tự với `-f 65001` (exit 0; `ObjectMenu_disable_legacy` lần chạy lại tắt 0).
- **Tên tiếng Việt lỗi font** trên DB dev / staging (seed từng chạy thiếu `-f 65001`: tên + ghi chú 4 ca, tên công ty DLC, ghi chú 33 đơn vị, mục "Duyệt – Đơn đặt suất ăn"): script mới `deploy/db/Repair-VietnameseText.ps1` (giải mã ngược Windows-1252 → UTF-8, chỉ sửa giá trị giải mã được, UPDATE từng dòng theo Id + giá trị cũ, ModifiedBy = admin, mặc định chạy thử) – dev sửa 44 giá trị, staging 9; chạy lại: 0.
- **`docs/api-tests/pilot-flow.http`** (117 request): danh mục tối thiểu (đơn vị, kho, NL mát / khô, NCC + bảng giá, 2 món + công thức gửi duyệt / duyệt / khóa, khách + điểm giao 11:00, hợp đồng + suất TRUA 35.000, xe, dây chuyền 1000 suất / mẻ, 2 lô bị chặn) → thực đơn tuần → phân công → đơn 1000 suất → chốt → MRP → PO 62,4 triệu → hộp duyệt GĐ → nhận hàng + QC → nhập kho → kế hoạch → phát hành → 2 mẻ → xuất NL → nấu / nhiệt độ lõi / lưu mẫu / sản lượng / đạt / đóng gói → chuyến → xuất phát → bàn giao + POD → đối soát → phản ánh. Biến + `# @name` theo cú pháp REST Client; 8 chốt an toàn ghi "→ 400".
- **Test `AppDelicare.Tests/Pilot/PilotFlowSqlServerTests.cs`** (SQL Server dev, `[SkippableFact]`): cùng luồng **qua HTTP thật** (routing, model binding, PermissionFilter, controller → service). Giữ 1 transaction ngoài qua nhiều request bằng `IStartupFilter` đặt `HttpContext.RequestServices` = 1 scope dùng chung có DbContext đang mở transaction (UnitOfWork tham gia transaction ngoài); cuối test rollback, kiểm số dòng Order / MaterialLot / InventoryTransaction / ProductionBatch / DeliveryRoute / CustomerComplaint / TemperatureLog không đổi. Kiểm: số PH- / PO- / NL- (2 lô khác số) / ME- / CX- / PA-, tổng 35.000.000 / 62.400.000, blockedQuantity 80, hộp duyệt scm-po không có PO / scm-po-director có, QC 9 °C bị chặn, xuất NL không lấy lô hết hạn / KHONG_DAT, lõi 65 °C chặn đạt QC, xuất phát bị chặn vì 55 °C và vì mẻ chưa lưu mẫu, bàn giao 58 °C bị chặn, đối soát 34.650.000, đơn Reconciled, 2 phản ánh.
- **Chạy thật trên DB dev** (`dotnet run` WebApi Development, `http://localhost:5125`), dữ liệu `[DEMO]` để lại trên dev:
  - **P9A** (PowerShell `Invoke-RestMethod`, ngày ăn 25/11/2026): chạy hết luồng nhưng lộ 2 lỗi (dưới). Số: đơn `PH-261117-0003`, `KH-261125`, đề nghị `PH-261117-0004`, `PO-261117-0001`, `XN-261117-005`, phiếu nhập `PH-261117-0005`, 2 lô **cùng số** `NL-261117-DEMOP9A-01` (lỗi – giữ nguyên để đối chiếu), mẻ `ME-261125-TRUA-01/02`, `CX-261125-DEMOP9A-01`, `PA-261117-0001/0002`.
  - **P9B** (PowerShell, sau khi sửa, ngày 26/11/2026): **88/88 bước đúng kỳ vọng**. Đơn `PH-261117-0012` (35.000.000 → Reconciled), `KH-261126`, đề nghị `PH-261117-0013`, `PO-261117-0002` (62.400.000, needsDirectorApproval, chỉ ở hộp `scm-po-director`, yêu cầu `XN-261117-008`), phiếu nhập / QC `PH-261117-0014`, lô `NL-261117-DEMOP9B-01` (bò 200) / `-02` (gạo 120), lệnh SX `PH-261117-0015/0016`, mẻ `ME-261126-TRUA-01` (bò kho) / `-02` (cơm) 08:30, phiếu xuất `PH-261117-0017/0018`, chuyến `CX-261126-DEMOP9B-01` + đơn giao `PH-261126-0001` → Completed, đối soát −10 (−1%) 34.650.000, phản ánh `PA-261117-0003` (tự động khi bàn giao) + `PA-261117-0004`.
  - **P9C** (chạy chính file `pilot-flow.http` bằng trình chạy nhỏ đọc cú pháp REST Client, ngày 02/12/2026): **117 request đúng kỳ vọng**. Đơn `PH-261117-0021` → Reconciled, `PO-261117-0003` → Received, lô `NL-261117-DEMOP9C-01/02`, mẻ `ME-261202-TRUA-01/02` Packed, `CX-261202-DEMOP9C-01` Completed, `PA-261117-0005/0006`.
- **Staging (T48.1):** `Deploy-Database.ps1` trên `AppDelicare_Staging` (131 SP + 6 seed, không đụng bảng) + sửa lỗi font; `Publish-Iis.ps1 -Output C:\Deploy\delicare-staging -Environment Staging`; `npm run build` rồi chép `dist\w3crm\browser` vào `wwwroot` (cùng địa chỉ API, Angular dùng `/api/` + `#/`); chạy `dotnet AppDelicare.WebApi.dll --urls http://localhost:5200` với biến môi trường của `staging.local.env.ps1`. Smoke test: `/health` Healthy (Staging), Swagger 200, đăng nhập admin, GET 200 cho `User/GetCurrent`, `Menu`, `Order`, `MaterialLot`, `InventoryBalance/GetSummary`, `MaterialRequirement/GetByDate`, `PurchaseRequest`, `PurchaseOrder`, `GoodsReceipt`, `QcInspection`, `ApprovalRequest/GetMyPending`, `ProductionPlan`, `ProductionBatch/GetBoard`, `DeliveryRoute/GetBoard`, `OrderReconciliation/GetPendingOrders`, `CustomerComplaint`, `Shift` (tên ca đúng dấu); trang `/` trả giao diện DELI-CARE. IIS thật: không làm (cần quyền admin – anh / IT theo deploy/README.md mục 4). Docker: máy chưa có Docker. `deploy/README.md` thêm mục 8 (Angular trên staging) + 9 (cập nhật staging).
- `UAT_CHECKLIST.md` mục **PILOT** (22 kịch bản API theo thứ tự request, vai trò gợi ý, kỳ vọng, các chốt an toàn) + ghi chú **PILOT-FE** (sau đợt FE, D-40). `docs/frontend-handoff/README.md` (mục lục P0-P2 … P8).

Lỗi tìm được và đã sửa (đều có test):
1. **Danh sách rỗng khi không lọc theo cha** – 35 SearchRequest còn lọc `Guid` không null (`MaterialLot.IngredientId`, `Order.CustomerId`, `Recipe.DishId`, `ProductionBatch.ProductionOrderId`, `MaterialIssue`, `InventoryBalance`, `InventoryTransaction`, `MenuAssignment`, `OrderReconciliation`, `DeliveryOrder`, `ProofOfDelivery`, `StockCount`…) → bỏ trống bị bind thành Guid rỗng, SP lọc `= 0000…` → `GetAll` luôn 0 dòng (màn Lô, Đơn hàng, Công thức, Mẻ… trống). Sửa như FE-2: thuộc tính → `Guid?`, 58 chỗ truyền tham số trong service đổi Guid rỗng → NULL. Test: `SearchFilterSqlServerTests.Pilot_lists_without_parent_filter_return_all_rows` (SQL Server, so với số dòng DB) + `Every_guid_filter_of_search_requests_is_nullable` (chặn tái phát cho mọi SearchRequest). `StorageLocation.WarehouseId`, `UomConversion.FromUnitId / ToUnitId`: service vốn không truyền lọc (ngoài Pilot – để nguyên).
2. **Trùng số chứng từ khi sinh nhiều mã trước 1 lần lưu** – phiếu nhập 2 dòng → 2 lô cùng số `NL-…-01` (cũng ảnh hưởng đề nghị mua nhiều NCC → nhiều PO cùng số). Nguyên nhân: trên SQL Server `DocumentNumberService` chỉ đọc MAX từ dòng đã lưu (nhánh InMemory có tính bản ghi đang theo dõi nên test InMemory không bắt được). Sửa gốc: cộng thêm mã của bản ghi `Added` đang theo dõi trong DbContext (dùng chung hàm `MaxOf` với nhánh InMemory). Test: `DocumentNumberSqlServerTests.Numbers_generated_in_a_loop_before_saving_are_distinct_on_sql_server` (3 lô → -01 / -02 / -03) + kiểm trong test luồng Pilot.
3. **`Deploy-Database.ps1` hỏng cú pháp** (từ T48.0) – xem trên; đã chạy thật trên staging.
4. **Test chập chờn** `UserAccountTests.Reset_returns_temp_password_…` (có từ trước: 1/169 lỗi ở baseline) – host test khác khởi động song song ghi đè `Serilog.Log.Logger` → chuyển lớp test vào collection không song song `StaticLoggerCollection`. 3 lần chạy liên tiếp 173/173.
Ghi chú (không phải lỗi): 2 lần đo nhiệt độ trong cùng 1 giây → lấy giá trị **thấp nhất** (thiết kế T19, an toàn) – test / `.http` ghi lần đo thấp kèm `MeasuredAt` lùi 10–15 phút như thực tế.

File chính: `AppDelicare.Services/Implement/DocumentNumberService.cs`; 35 file `AppDelicare.Model/SearchRequest/*SearchRequest.cs` + 33 service tương ứng (`AppDelicare.Services/Implement/*Service.cs`, chỉ dòng `dynamicParameters.Add`); test `Tests/Pilot/PilotFlowSqlServerTests.cs` (mới), `Tests/MasterData/SearchFilterSqlServerTests.cs`, `Tests/Common/DocumentNumberTests.cs`, `Tests/Admin/UserAccountTests.cs`, `Tests/Integration/ApiFactory.cs`; `DB/README.md`, `deploy/db/Deploy-Database.ps1`, `deploy/db/Repair-VietnameseText.ps1` (mới), `deploy/docker/db-init.sh`, `deploy/README.md`; `docs/api-tests/pilot-flow.http` (mới), `docs/claude/{UAT_CHECKLIST, ROADMAP, PROGRESS}.md`, `docs/frontend-handoff/README.md` (mới).

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 10 cảnh báo (≤ 11).
- `dotnet test`: **173/173** (+4: PilotFlow 1, SearchFilter 2, DocumentNumber 1), chạy 3 lần liên tiếp đều xanh; test SQL Server không bị bỏ qua.
- `npm run build` (`app-delicare-client`): 0 lỗi (1 cảnh báo cũ "Unable to locate stylesheet D:\assets\css\style.css").
- `git status`: không thay đổi trong `app-delicare-client/` (`dist` đã gitignore), `docs/reference/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; `api/v1` trong `*.cs` không tăng (3, đều trong test cũ).

AC (P9): [x] `pilot-flow.http` chạy hết P3 → P8 bằng 1 bộ `[DEMO]` (chạy thật trên DB dev) · [x] test tự động end-to-end qua HTTP · [x] UAT_CHECKLIST mục PILOT (API) · [~] mục PILOT phần thao tác trên giao diện → sau đợt FE (D-40) · [x] deploy staging backend + bản build Angular, smoke test · [ ] IIS thật (cần quyền admin) · [ ] Catalog (không có openpyxl).

`DELICARE_API_Catalog.xlsx` – **chưa cập nhật** (máy không có python / openpyxl; T-MERGE: không tự gộp file nhị phân). Gợi ý cho anh / lần sau (cột J Trạng thái, K ghi chú, M Task):
- Theo gợi ý đã ghi cuối các mục T13 (6.2 / 6.3 / 6.6), T16 (7.5 – 7.8), T14 (7.1), T15 (7.2 – 7.4, 9.1, 9.3, 9.5), T17 (8.1 – 8.4, 9.3), T19 (6.7, 10.2, 10.3, 10.5) và T45R.8 (dòng 34, 35, 38) phía trên.
- T50R1 không thêm action mới; các dòng GET danh sách của Lô / Đơn hàng / Công thức / Mẻ / Lệnh SX / Phiếu xuất / Kiểm kê / Phân công thực đơn / Đối soát / Đơn giao / POD: cột K thêm "T50R1: bỏ trống lọc cha = không lọc".
- Cột M thêm `T50R1` cho các dòng thuộc luồng P3 → P8 đã chạy end-to-end (đơn, MRP, PR, PO, nhận hàng, QC, kế hoạch, mẻ, chuyến, POD, đối soát, phản ánh).

Câu hỏi còn mở (PROPOSED – gom từ các task Pilot, chi tiết ở DECISIONS):
1. GAP-ORD-01…03, GAP-MENU-01/02: 1 đơn = 1 điểm giao; lựa chọn món lưu ở `MenuLine.SequenceNo`; xử lý đơn chưa xác nhận lúc chốt 20:00 D-1; lệch suất so với số đã xác nhận (chưa so trung bình 4 tuần), ai duyệt lệch suất (seed chỉ CEO).
2. GAP-INV-02…11: phiếu xuất tay không gắn lệnh SX; lô chưa có kết quả QC vẫn cho xuất; ngưỡng kiểm kê 2% theo dòng; điều chỉnh tay chưa cần duyệt.
3. GAP-MRP-02/04/08 + T14.1: chi tiết MRP trong Note (JSON); món chung nhiều lựa chọn không cộng dự phòng; cách tính "đã đề nghị".
4. GAP-PUR-01/02/05/06/07/09/10: không duyệt đề nghị mua; PO bị từ chối về Nháp; hàng không đạt không nhập kho; dung sai nhận 10%; % VAT theo nhóm NL (đang 8% mọi dòng); cờ "ưu tiên dùng trước" chỉ trong Note.
5. GAP-PRD-01…08 + T17.1 (a)–(f): chia phần theo ca, trạng thái phát hành theo ca, giờ xuất bếp nhập tay chỉ khi 1 ca, đơn nhiều ca, mã ca trong số mẻ.
6. GAP-DLV-01…07: chặn xuất phát khi còn mẻ của ngày / ca chưa đóng gói; đo nhiệt độ 1 lần / chuyến + 1 lần / điểm; bàn giao < 60 °C không cho nhận; tính tiền theo suất phục vụ; tự tạo phản ánh khi có suất từ chối; mẫu Biên bản giao hàng thật.
7. GAP-ADM-04/05: quy tắc tên đăng nhập; `IsActive` mặc định false.
8. Mới (T50R1, GAP-DOC-01): số đơn giao `PH-` lấy theo **ngày giao** (`PH-261126-0001`), chứng từ khác theo ngày lập – giữ hay thống nhất? Hai lần đo nhiệt độ cùng giây lấy giá trị thấp nhất – giữ?

Cần anh:
- UAT-PILOT theo `UAT_CHECKLIST.md` mục PILOT trên staging `http://localhost:5200` (chạy lại theo deploy/README.md mục 9 bước 4; mật khẩu admin staging trong `deploy/staging.local.md`).
- Duyệt / commit nhánh; chốt các câu hỏi trên; IIS thật nếu chọn IIS (deploy/README.md mục 4); cập nhật Catalog.

### BE2.1 – Lấp chỗ thiếu của luồng Pilot · 2026-11-17 · DONE
Nhánh `feat/BE2.1-flow-gaps` (worktree `wt-BE21`, từ `release/pilot` @ `f9b3231`). Chỉ backend (D-49); không đụng `app-delicare-client/`, `docs/reference/*`, `deploy/db/Deploy-Database.ps1`, `deploy/docker/db-init.sh` (T48.3 đang sửa song song).

Kế hoạch:
1. Rà soát: PackingOrder / PackingResult / DeliveryLoading / DeliveryExecution / DeliveryOrder chỉ có CRUD sinh mã (ghi tay được mọi trường); DeliveryRoute / Stop, ProductionBatch, ProductionPlan, MaterialRequirement, TemperatureLog, MealWasteRecord, Order, Menu, CustomerComplaint, ApprovalRequest, MaterialLot đã có nghiệp vụ T13–T19 → bổ sung method vào đúng service + action vào đúng controller (không service / controller mới; thêm 1 lớp tĩnh dùng chung `TraceRules` như `DeliveryRules`).
2. Đóng gói từ mẻ (171–175) + tách `CompleteOrderIfDoneAsync` sang `ProductionRules` dùng chung.
3. Đơn giao tay / tách / gán – chuyển / hủy (184–187), lập chuyến nhiều xe theo khu vực + đổi thứ tự (177–179), xếp hàng lên xe (180), thực hiện giao (188).
4. Kế hoạch sinh theo ca (137), đề nghị mua chọn dòng + dự phòng (107).
5. Nhiệt độ tự do (165), hao hụt (156), bảng đơn (90), thực đơn (82), phản ánh (197), chi tiết + hủy yêu cầu duyệt (211, 213), truy xuất 2 chiều (118, 119).
6. Test InMemory + SQL Server rollback; `.http` chạy thật trên DB dev; handoff; DECISIONS / ROADMAP / BUSINESS_RULES.

Đã làm (mẫu 4.2; quyền theo `PermissionConvention`, ngoại lệ ghi `[Permission]` / `[SkipPermission]`):
- **Đóng gói** (`PackingOrderService` / `PackingOrderController`, `log-packing-order`): `GetList`, `GetByBatch`, `GenerateFromBatch` (Thêm – mẻ Đạt QC + lõi ≥ 70 °C + đã ghi sản lượng; số phần đạt còn lại chia cho đơn Đã chốt / Đang SX cùng ngày + ca theo tỉ lệ suất món, phần dư lớn nhất; lệnh `PH-`), `Create` (Σ ≤ số đạt), `Start`, `RecordResult` (đóng gói + hỏng + loại = kế hoạch; `PackingResult`; hỏng / loại → `MealWasteRecord` PACKING_DAMAGE + WasteQty mẻ; mẻ chia hết + mọi lệnh xong → mẻ Đã đóng gói → lệnh SX / kế hoạch hoàn thành), `Cancel` (chỉ Kế hoạch, lý do). `ProductionBatch/Pack` chặn khi còn lệnh chưa xong. CRUD PackingOrder / PackingResult → 400.
- **Xếp xe / chuyến** (`DeliveryRouteService`): `Load` (JSON; DeliveryLoading Loaded + DeliveryExecution LOADED, chuyến → Đang xếp, sự kiện LOADED; danh sách trống = đơn giao chưa xếp), `Plan` (JSON, nhiều xe theo khu vực), `UpdateStops` (JSON); `Create` nhận thêm `listDeliveryOrderId`, đơn hàng lấy **phần suất còn lại**; tài xế trùng khung giờ → chặn (Create / Update). `Dispatch`: chặn khi còn đơn giao chưa xếp (`BusinessRules:DeliveryRequireLoading` = true), thực hiện giao → DISPATCHED. `DeliveryStop/Arrive / Deliver / Fail` cập nhật thực hiện giao (ARRIVED / DELIVERED / FAILED); đơn hàng → Đã giao chỉ khi không còn suất chưa lập đơn giao. Chi tiết chuyến thêm `loadedQty`, `executionStatus`; `allowedActions` thêm `load`, `updateStops` (Kế hoạch không còn `dispatch`).
- **Đơn giao** (`DeliveryOrderService` / `DeliveryOrderController`, `log-delivery-order`): `GetList`, `Create` (JSON), `Split` (JSON), `AssignRoute`, `Cancel`; số suất ở tag `[SUAT:n]` (GAP-DLV-08); đánh số lại điểm dừng 2 bước qua số âm vì UNIQUE (route, sequence) tính cả dòng xóa mềm (GAP-DLV-10). CRUD Insert → 400; Update giữ tag.
- **Thực hiện giao** (`DeliveryExecutionService` / `Controller`, `log-execution`): `GetBoard` + `statusCounts`; CRUD DeliveryExecution / DeliveryLoading → 400.
- **Kế hoạch** `ProductionPlan/Generate` (Thêm – 1 KH- / ngày từ đơn Đã xác nhận / Đã chốt, `ShiftId` dòng khi món thuộc 1 ca; chặn khi đã Duyệt / đã tính nhu cầu NL) – dùng `ProductionRules.DishDemandsAsync` (mới, cùng công thức MRP).
- **MRP** `MaterialRequirement/CreatePurchaseRequest` (JSON – dòng chọn + % dự phòng, làm tròn lên 4 số lẻ; cập nhật đề nghị Nháp tại chỗ; đã gửi duyệt → chặn).
- **Nhiệt độ** `TemperatureLog/Record` (Thêm `qc-temperature`; ngưỡng khu kho / HOT_HOLDING ≥ 60; CORE / DELIVERY / RECEIVING → chặn; ngoài ngưỡng → lưu + cảnh báo); CRUD Insert dùng cùng logic (bỏ IsWithinRange / RecordedBy của client). Gỡ header auto-generated của `TemperatureLog*`, `MealWasteRecord*`.
- **Hao hụt** `MealWasteRecord/Record` (Thêm `prod-waste`; mẻ / lệnh SX / món + ca; loại theo `ReferenceLists:WasteType`) + `GetList` (tổng suất / kg, theo loại); CRUD Insert → 400.
- **Danh sách**: `Order/GetBoard` (8 cột + statusCounts, lọc BranchId của khách), `Menu/GetList` (lineCount, dishCount, assignedCustomerCount, orderedMeals), `CustomerComplaint/GetList` (đủ trường, isOverdue, hoursToDue, statusCounts, overdueCount, phân trang), `MaterialLot/GetList` (tồn, hạn, bị chặn, sắp hết hạn).
- **Duyệt** `ApprovalRequest/GetDetail` + `Cancel` (`[SkipPermission]` như GetMyPending / Decide, service kiểm người gửi / người duyệt / admin); `IApprovalHandler.CancelAsync` mặc định = xử lý từ chối (handler cũ không sửa); ghi ApprovalDecision CANCEL.
- **Truy xuất** `MaterialLot/Trace` (xuôi) + `ProductionBatch/Trace` (ngược) – `TraceRules` (lớp tĩnh), liên kết suy luận đánh dấu `inferred` (GAP-TRC-01).
- Cấu hình: `BusinessRules:DeliveryRequireLoading` = true; `ReferenceLists:WasteType` + `PACKING_DAMAGE`; `ReferenceLists:TemperatureMeasurementType` (mới). Thông báo tiếng Việt trong `ConstantHelper.Message`. Hằng `DeliveryLoadingStatus`, `DeliveryExecutionStatus`, `PackingOrderStatus`, `DeliveryOrderStatus.Cancelled` (`Model/Helper/DeliveryStatus.cs`).
- **Không có SP / seed mới**, không thêm bảng / cột, không mục menu mới (mọi screen-id đã có trong `ObjectMenu_seed.sql`) → `deploy/db/scripts.order` (T48.3) không cần thêm dòng nào.

File chính: `AppDelicare.Services/Implement/{PackingOrderService, PackingResultService, DeliveryRouteService, DeliveryOrderService, DeliveryExecutionService, DeliveryLoadingService, DeliveryStopService, DeliveryRules, ProductionRules, ProductionBatchService, ProductionPlanService, MaterialRequirementService, TemperatureLogService, MealWasteRecordService, OrderService, MenuService, CustomerComplaintService, ApprovalRequestService, MaterialLotService, TraceRules (mới)}.cs`, interface tương ứng + `IApprovalHandler.cs`, 14 controller, `Model/Helper/{DeliveryStatus, ConstantHelper}.cs`, `Model/Option/BusinessRulesOptions.cs`, `Model/Model/{DeliveryModels, FlowGapModels (mới)}.cs`, `Model/Request/Custom/{DeliveryCustomRequest, MaterialRequirementCustomRequest, ProductionCustomRequest, PackingCustomRequest (mới), FlowGapCustomRequest (mới)}.cs`, `WebApi/appsettings.json`; test `Tests/Delivery/{FlowGapDeliveryTests, FlowGapSqlServerTests}.cs`, `Tests/Production/FlowGapTests.cs` (mới) + sửa `DeliveryTests`, `DeliverySqlServerTests`, `ProductionShiftTests`, `PilotFlowSqlServerTests` (thêm bước Load); `docs/api-tests/be2.1-flow-gaps.http` (mới), `pilot-flow.http`; `docs/frontend-handoff/BE2.1.md` (mới), `README.md`, `P7.md`, `P8.md`; `docs/claude/{DECISIONS, ROADMAP, BUSINESS_RULES, PROGRESS}.md`, `DB/README.md`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (cảnh báo cũ, không thêm mới).
- `dotnet test`: **185/185** (baseline 173 + 12: FlowGapDeliveryTests 4, FlowGapTests 7, FlowGapSqlServerTests 1 – SQL Server dev, không bị bỏ qua; 4 test cũ sửa để xếp hàng trước khi xuất phát).
  - FlowGapDeliveryTests: sinh lệnh đóng gói 100 → A 60 / B 40; chia hết thì chặn sinh lại / lập tay vượt / Pack thẳng; hủy B → còn 40 → lập tay lại; kết quả lệch tổng → chặn; 58 + 1 + 1 → hao hụt PACKING_DAMAGE 2; lệnh cuối → mẻ Packed, lệnh SX Completed; mẻ lõi 65 °C / chưa Đạt QC → chặn. Đơn giao tay 25 (điểm Quận 7) → vượt 35 còn lại chặn, điểm của khách khác chặn → tách 15 + 10 → chuyến (35 còn lại + 15) → gán 10 vào đầu → đổi thứ tự → chuyển sang chuyến B (đánh số lại cả 2, điểm cũ xóa mềm) → hủy đơn giao B (lý do bắt buộc) → xếp hàng 34 / 35 (cảnh báo) → chưa xếp hết thì chặn xuất phát; đã xếp thì không hủy / không gán vào chuyến Đang xếp → xuất phát → bảng thực hiện giao (DISPATCHED 2, READY 2, đơn Hủy không hiện) → đến điểm → ARRIVED; CRUD cũ chặn. Lập chuyến 2 xe: Quận Tân Bình (A 60 + C 30) → xe 100, TP. Thủ Đức (B 40) → xe 50, `CX-261217-XAR1-01`, hết hàng thì chặn.
  - FlowGapTests: Generate (Đã xác nhận + Đã chốt, bỏ Nháp: cơm 150 / gà 100 ca trưa / cá 50 ca tối; sinh lại cập nhật tại chỗ; đã duyệt chặn); CreatePurchaseRequest (dự phòng 5% → 105 / 35; chọn lại 10% → 110, dòng gạo xóa mềm; đã gửi duyệt chặn); nhiệt độ (thiếu đối tượng / CORE / DELIVERY / loại lạ chặn; 7 °C ngoài 0…5 → lưu + cảnh báo, server tính IsWithinRange; HOT_HOLDING 55 không đạt; CRUD Insert tính ở server); hao hụt (loại sai / thiếu số / mẻ chưa nấu / không đối tượng chặn; WasteQty mẻ 5; tổng 5 suất 5,5 kg); bảng đơn 8 cột / BranchId / tìm; thực đơn 3 dòng 2 món 1 khách 80 suất; phản ánh quá hạn 1 (hoursToDue −4) / lọc mức độ; duyệt: chi tiết công thức + dòng, người lạ không hủy / không xem, hủy → công thức Draft, Cancelled + CANCEL, admin hủy được của người khác; truy xuất lô → NCC / PO / phiếu nhập / mẻ 1 / lệnh đóng gói / đơn A / khách A, ngược mẻ 2 → đơn suy luận A + B, mẻ 1 → lô → NCC + lõi 78 °C; danh sách lô sắp hết hạn.
  - FlowGapSqlServerTests: đóng gói (số PH- qua sp_getapplock) → mẻ Packed; đơn giao tay → chuyến → tách → gán vào số 1 → đảo thứ tự → hủy (số âm) → lập lại + gán cuối (tái dùng số 3) → xếp hàng 3 dòng → xuất phát → 3 DISPATCHED; rollback, số dòng không đổi.
- **Chạy thật trên DB dev** (`dotnet run` WebApi Development, cổng 5321, trình chạy nhỏ đọc cú pháp REST Client – kiểm `responseCode` 200 / 400 theo đánh dấu "→ 400"):
  - `be2.1-flow-gaps.http` **138/138** (11 chốt an toàn trả 400) – chạy 3 lần (key B21H 30/12/2026 – lộ 1 lỗi ở tiêu đề của file .http (đánh dấu "→ 400" nhầm), đã sửa; B21J 06/01/2027; B21K 13/01/2027). Số chứng từ lần B21K: đơn `PH-261117-0074/0075/0076` (300 / 200 / 150 → Đang SX), kế hoạch `KH-270113`, đề nghị `PH-261117-0077` (rau 68,25 = 65 × 1,05 → Đã lập PO), PO gửi duyệt rồi người gửi hủy (về Nháp), mẻ `ME-270113-TRUA-01` → 3 lệnh đóng gói `PH-261117-0080/0081/0082` → Packed, đơn giao `PH-270113-0001` (60, Hủy) … `-0006`, chuyến `CX-270113-DEMOB21KX1-01` (Quận 7: 150 + 200 + 40 = 390 / 400) và `CX-270113-DEMOB21KX2-01` (Quận 1: 200 + 60) → Đang giao, 5 đơn giao DISPATCHED.
  - `pilot-flow.http` **121/121** (thêm lệnh đóng gói mẻ 1 + chốt Pack thẳng + bước Load) – chạy 2 lần (B21B 16/12/2026, B21C 23/12/2026).
  - Dữ liệu `[DEMO]` để lại trên DB dev (như T50R1). Không chạy script SQL nào.
- `git status`: không thay đổi trong `app-delicare-client/`, `docs/reference/`, `deploy/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không `api/v1` mới.

AC (BE2.1): [x] mọi mục phạm vi có API + test · [x] chốt ATTP giữ nguyên (lõi ≥ 70 °C kiểm lại khi đóng gói, giao ≥ 60 °C, lưu mẫu trước khi giao, lô hết hạn / KHONG_DAT bị chặn – không đổi) · [x] `.http` chạy thật · [x] handoff · [x] không bảng / cột / SP mới · [ ] Catalog Excel (máy không có openpyxl – bảng dưới).

Bảng cập nhật `DELICARE_API_Catalog.xlsx` (sheet `API_Catalog`, cột J / L / K; cột M = `BE2.1`):
| STT | Trạng thái (J) | API thực tế – khung cũ (L) | Ghi chú (K) |
|---|---|---|---|
| 82 | Đã xong | `GET Menu/GetList` | lineCount, dishCount, assignedCustomerCount, orderedMeals |
| 90 | Đã xong | `GET Order/GetBoard` | 8 cột + statusCounts; danh sách = `Order/GetAll` |
| 107 | Một phần | `POST MaterialRequirement/CreatePurchaseRequest` | Chưa gọi duyệt 2 cấp (BE2.3, D-46); làm tròn 4 số lẻ (D-50 g) |
| 118 | Đã xong | `GET MaterialLot/GetList` | + `MaterialLot/GetAll` (CRUD) |
| 119 | Đã xong | `GET MaterialLot/Trace`, `GET ProductionBatch/Trace` | Liên kết suy luận `inferred` (GAP-TRC-01); chưa QR (GAP-F-10) |
| 137 | Một phần | `POST ProductionPlan/Generate` | 1 KH- / ngày, tách ca ở dòng (D-50 a); chưa áp MenuChangeRequest (BE2.2) |
| 156 | Đã xong | `POST MealWasteRecord/Record`, `GET MealWasteRecord/GetList`, `ProductionBatch/RecordWaste` | Tổng hợp biểu đồ (157) để sau |
| 165 | Đã xong | `POST TemperatureLog/Record` | CORE / DELIVERY / RECEIVING qua nghiệp vụ; ngoài ngưỡng lưu + cảnh báo |
| 171 | Đã xong | `GET PackingOrder/GetList`, `GetByBatch` | |
| 172 | Đã xong | `POST PackingOrder/RecordResult` | |
| 173 | Đã xong | `POST PackingOrder/Create`, `GenerateFromBatch` | Sinh từ mẻ theo tỉ lệ suất (GAP-PCK-01) |
| 174 | Đã xong | `POST PackingOrder/Start` | |
| 175 | Đã xong | `POST PackingOrder/Cancel` | |
| 177 | Đã xong | `POST DeliveryRoute/Plan` | Khu vực theo AreaId / địa chỉ (GAP-DLV-09) |
| 178 | Đã xong | `POST DeliveryRoute/Create`, `PUT DeliveryRoute/Update` | Tài xế không trùng khung giờ |
| 179 | Đã xong | `PUT DeliveryRoute/UpdateStops` | |
| 180 | Đã xong | `POST DeliveryRoute/Load` | Bắt buộc trước Dispatch (D-50 b); chưa liên kết PackingOrder (GAP-C-03) |
| 181 | Đã xong | `POST DeliveryRoute/Dispatch` | (T19) + yêu cầu đã xếp hàng, thực hiện giao DISPATCHED; chưa gửi Zalo |
| 183 | Đã xong | `GET DeliveryOrder/GetList` | mealQuantity, routeNo, unassigned |
| 184 | Đã xong | `POST DeliveryOrder/Create` | Số suất ở tag Note (GAP-DLV-08) |
| 185 | Đã xong | `POST DeliveryOrder/Split` | |
| 186 | Đã xong | `POST DeliveryOrder/AssignRoute` | |
| 187 | Đã xong | `POST DeliveryOrder/Cancel` | |
| 188 | Đã xong | `GET DeliveryExecution/GetBoard` | + statusCounts |
| 197 | Đã xong | `GET CustomerComplaint/GetList` | isOverdue, hoursToDue, statusCounts |
| 211 | Đã xong | `GET ApprovalRequest/GetDetail` | business.record + lines |
| 213 | Đã xong | `POST ApprovalRequest/Cancel` | Cancelled + ApprovalDecision CANCEL (GAP-APR-01) |

PROPOSED chờ anh (DECISIONS **D-50**, mẫu D-49 – em đã cài theo "Đề xuất", không mục nào BLOCKED):
(a) Generate giữ 1 KH- / ngày (spec: mỗi ca 1 kế hoạch); (b) xếp hàng bắt buộc trước khi xuất phát; (c) số suất đơn giao ở tag `[SUAT:n]` trong Note (dài hạn: cột `DeliveryOrder.MealQuantity` – cần anh duyệt migration); (d) khu vực = AreaId / quận trong địa chỉ; (e) lệnh đóng gói chia theo tỉ lệ suất, vẫn cho Pack thẳng mẻ chưa có lệnh; (f) yêu cầu duyệt thêm trạng thái Cancelled; (g) dự phòng làm tròn 4 số lẻ, không lưu; (h) truy xuất suy luận + danh mục loại đo nhiệt độ.

Cần anh: không có script SQL mới. Khi gộp nhánh: appsettings có 3 khóa mới (DB/README.md); T48.3 không cần thêm dòng vào `deploy/db/scripts.order`. FE: đọc `docs/frontend-handoff/BE2.1.md` mục "Thay đổi hành vi" (xếp xe trước khi xuất phát, nhiều đơn giao / đơn).

### T48.3 – Tự cập nhật DB cho server Docker của dev FE · 2026-11-16 · DONE (Docker thật: chưa chạy – máy không có Docker)
Nhánh `feat/T48.3-db-autoupdate` (từ `release/pilot`, worktree `wt-T483`, song song BE2.1). Không đụng bảng / cột, không xóa dữ liệu, không chạy lên staging / production / server dev FE.

Đã làm:
1. **Một danh sách thứ tự duy nhất `deploy/db/scripts.order`**: 131 SP `DB/sp` (đúng thứ tự `Sort-Object Name` của PS1 cũ) → `DB/seed/00_Admin_seed.sql` → 6 seed (OrganizationUnit → ObjectMenu → ObjectMenu_disable_legacy → UserRole → Shift → FoodSafetyChecklist). `Deploy-Database.ps1` và `db-init.sh` cùng đọc file này (bỏ 2 danh sách seed lặp + bỏ `for f in DB/sp/*.sql`). Liệt kê từng file vì thứ tự sắp xếp tên của PowerShell và bash `LC_ALL=C` **khác nhau ở 22 SP** (vd. `uspContract_GetAll`, `uspCustomer_GetAll`, `uspUser_GetAll`). Dòng không có file / ghi 2 lần → lỗi; SP có trong `DB/sp` mà quên ghi → vẫn chạy ở cuối + CẢNH BÁO (giữ hành vi cũ "chạy mọi SP"); seed quên ghi → CẢNH BÁO, không chạy.
2. **`db-init.sh` viết lại, idempotent như PS1**: tạo DB nếu chưa có; DB 0 bảng mới chạy DDL; SP / seed theo `scripts.order`; `sqlcmd -C -b -I -f 65001`; mật khẩu SA qua `SQLCMDPASSWORD` (không còn `-P` trên dòng lệnh); admin chỉ tạo khi **chưa có** (đã có thì bỏ qua, không cần `ADMIN_PASSWORD`). **Hash admin MD5 → PBKDF2** `PBKDF2$100000$salt$hash`: container chỉ có sqlcmd nên tính PBKDF2-HMAC-SHA256 ngay trong SQL Server bằng `deploy/db/pbkdf2-hash.sql` (HASHBYTES SHA2_256, salt `CRYPT_GEN_RANDOM(16)`, 100.000 vòng, chỉ SELECT – không tạo object); mật khẩu → hex UTF-8 → file tạm quyền 600 (không in, không lên dòng lệnh). `--dry-run` in thứ tự không cần DB. Biến tùy chọn `DB_SERVER`, `DB_TRUSTED_AUTH`, `SQLCMD_BIN`, `REPO_ROOT` (để chạy thử ngoài container).
3. **Lỗi cũ tìm thấy và đã sửa**: `sqlcmd -v AdminPasswordHash=PBKDF2$…==$…` báo `Invalid argument` (sqlcmd tách sai giá trị có `=` – đuôi base64 của salt luôn có `==`) → **đường `-AdminPassword` của `Deploy-Database.ps1` từ T48.0 chưa bao giờ chạy được** trên DB mới. Nay cả PS1 và db-init.sh truyền `AdminUserName` / `AdminPasswordHash` bằng file tạm `:setvar` + `:r`. Comment đầu `00_Admin_seed.sql` sửa theo (chỉ comment).
4. `docker-compose.staging.yml`: mount thêm `deploy/db` vào `db-init`; `ADMIN_PASSWORD` không còn bắt buộc (chỉ cần lần đầu – db-init báo lỗi rõ nếu DB chưa có admin).
5. **`deploy/docker/update-db.sh`** (mới): `git pull --ff-only` → chỉ chạy `docker compose … --env-file deploy/docker/staging.env run --rm db-init` khi từ **lần cập nhật DB thành công gần nhất** (lưu ở `~/.delicare-update-db.last`) có thay đổi trong `DB/`, `deploy/db/`, `db-init.sh`, `docker-compose.staging.yml`; db-init lỗi → lần sau thử lại; pull lỗi → không chạy; `--force`; log có giờ; khóa `flock` chống chạy chồng; dòng cron 5 phút trong `deploy/docker/README.md`.
6. **Dự phòng server không ra Internet**: `Deploy-Database.ps1` thêm `-TrustServerCertificate` (SQL Docker chứng chỉ tự ký), `-ListOnly`, lấy tài khoản từ biến môi trường `DELICARE_SQL_USER` / `DELICARE_SQL_PASSWORD` / `DELICARE_ADMIN_PASSWORD`, mật khẩu SQL qua `SQLCMDPASSWORD`; `-Server` / `-Database` không còn Mandatory (để `-ListOnly`), thiếu thì báo lỗi.
7. `.gitattributes` (mới): `*.sh text eol=lf` – máy Windows `core.autocrlf=true` checkout `.sh` thành CRLF làm hỏng bash trong container (index vốn đã LF, không đổi file nào khác). `update-db.sh` có bit thực thi trong git.
8. Tài liệu: `deploy/docker/README.md` (mới), `docs/frontend-handoff/README.md` (mục cập nhật DB), `deploy/README.md` (mục 2, 5, 9, bảng cách C), `DB/README.md` (scripts.order là nguồn thứ tự; SP / seed mới phải thêm vào đó).

**Lệnh cho dev FE** (khác spec 1 chỗ: thêm `--env-file`, vì compose đọc `SA_PASSWORD` / `JWT_SIGNING_KEY` từ `staging.env`, không có thì báo lỗi biến chưa đặt):
`git pull && docker compose -f deploy/docker/docker-compose.staging.yml --env-file deploy/docker/staging.env run --rm db-init` – hoặc `bash deploy/docker/update-db.sh`.

Kiểm tra – **chạy THẬT**:
- Thứ tự: `Deploy-Database.ps1 -ListOnly` và `REPO_ROOT=. bash deploy/docker/db-init.sh --dry-run` ra **cùng 138 dòng**, `diff` với danh sách PS1 cũ (131 SP theo `Sort-Object Name` + admin + 6 seed) = **không khác**. Thử cảnh báo SP / seed ngoài danh sách và lỗi dòng không có file trên bản sao trong scratchpad.
- `pbkdf2-hash.sql` trên LocalDB (`master`, chỉ tính toán): kết quả **trùng byte** với .NET `Rfc2898DeriveBytes` (SHA256, 100.000 vòng) cho mật khẩu có tiếng Việt / ký tự đặc biệt và mật khẩu > 64 byte; ~3 giây.
- `Deploy-Database.ps1` trên DB tạm `AppDelicare_T483Check` (LocalDB, `-CreateDatabase -CreateSchemaIfEmpty -AdminPassword`), **2 lần**: 0 lỗi; lần 1 tạo 124 bảng + 131 SP + admin + 6 seed; lần 2 "Schema đã có", số dòng **không đổi** (User 1, Company 1, OrganizationUnit 33, ObjectMenu 147, UserGroup 16, UserRole 1934, Shift 4, KitchenWorkstation 6, FoodSafetyChecklist 1 / Item 2); hash admin `PBKDF2$100000$…` kiểm lại bằng mật khẩu = **đúng**.
- `db-init.sh` chạy thật trong Git Bash bằng sqlcmd Windows (`DB_TRUSTED_AUTH=1`, `DB_SERVER=(localdb)\MSSQLLocalDB`): (a) trên DB đã có dữ liệu (`AppDelicare_T483Check` ở trên): exit 0, "admin đã có – giữ nguyên", số dòng không đổi; (b) DB tạm mới `AppDelicare_T483CheckSh` **2 lần**: lần 1 tự tạo DB + 124 bảng + admin PBKDF2 (tính trong SQL) + seed, lần 2 không đổi gì; số dòng giống hệt bản PS1; hash admin kiểm bằng .NET = **đúng**; tên ca tiếng Việt đúng dấu (`-f 65001`); file tạm đã xóa. Hai DB tạm đã DROP sau khi kiểm.
- `update-db.sh` với repo git tạm (bare origin + clone) và `docker` giả: lần đầu chạy; không có commit mới → bỏ qua; commit chỉ sửa docs → bỏ qua; commit sửa `DB/seed` + db-init lỗi → exit 1 và lần sau chạy lại; `--force`; working tree bẩn làm `git pull` lỗi → không chạy db-init.

Kiểm tra – **chỉ bằng đọc lại (chưa chạy)**: chạy trong container thật (`mcr.microsoft.com/mssql/server:2022-latest`: `/opt/mssql-tools18/bin/sqlcmd`, bash 5, `od` / `sed` / `mktemp` có sẵn, user `mssql` không phải root – chỉ ghi vào `mktemp`); `docker compose run --rm db-init` thật; đăng nhập API trên Docker; đăng nhập SQL bằng tài khoản SQL (`-U` + `SQLCMDPASSWORD`, LocalDB chỉ có Windows auth); cron thật; `flock` (Git Bash không có – script bỏ qua khóa khi thiếu). Dev FE chạy lần đầu nên xem log `db-init`.

Cần anh: (1) dev FE chạy lệnh trên ở server Docker (lần đầu nên chạy tay, xem log); (2) chấp nhận thêm `--env-file` vào lệnh (hoặc đổi tên `staging.env` thành `deploy/docker/.env` để bỏ – em đề xuất **giữ `--env-file`**, không đổi tên file bí mật đã gitignore); (3) khi gộp BE2.1: thêm SP / seed mới của BE2.1 vào cuối `deploy/db/scripts.order` (nếu quên, SP vẫn chạy kèm cảnh báo, seed thì không).

### BE2.2 – Hợp đồng, dự báo, đổi món, sửa đơn sau giờ chốt (T13 mở rộng) · 2026-11-17 · DONE
Nhánh `feat/BE2.2-contract-forecast` (worktree `wt-BE22`, từ `release/pilot` @ `75306d5` = BE2.1 + T48.3). Chỉ backend (D-49); không đụng `app-delicare-client/`, `docs/reference/*`.

Kế hoạch:
1. Rà soát: `Contract`, `ContractMeal`, `Forecast`, `MenuChangeRequest` chỉ có CRUD sinh mã (ghi được mọi trường, kể cả `Status = Active` – "duyệt chui"); `Order` có T13 (giờ chốt, lệch suất ORDER_DEVIATION); engine duyệt 1 cấp T12 (`IApprovalHandler`); mục duyệt `#/wf-inbox?type=crm-contract / menu-change-request / crm-order` **đã có** trong seed (không thêm seed). → bổ sung vào đúng service / controller cũ, 3 handler mới, 1 lớp tĩnh `ForecastRules` (như `DeliveryRules`).
2. Hợp đồng: Create kèm suất + số HD-, Submit / Approve / Reject (engine CONTRACT), Terminate, GetDetail; khóa CRUD theo trạng thái.
3. Dự báo: Import (phiên bản), Publish, GetMatrix; gợi ý số suất mẫu tạo đơn; lệch dự báo khi xác nhận; forecastQuantity ở chi tiết đơn.
4. Đổi món: Create (PH-) + MENU_CHANGE; duyệt → kế hoạch Nháp; áp đổi món đã duyệt vào Generate / MRP / chia mẻ / truy xuất.
5. Sửa / thêm đơn sau giờ chốt: `Order/RequestChange`, `Order/Create` + `afterCutoffReason`, handler ORDER_CHANGE_AFTER_CUTOFF.
6. Test InMemory + SQL Server rollback; `.http` mới + chạy lại `pilot-flow.http` / `be2.1-flow-gaps.http` trên DB dev; handoff; DECISIONS D-51 / GAP.

Đã làm (mẫu 4.2; quyền theo `PermissionConvention`, ngoại lệ `[SkipPermission]` cho Approve / Reject – engine kiểm như `PurchaseOrder/Approve`):
- **Hợp đồng** (`ContractService` / `ContractController`, `ContractMealService`): `Create` (JSON – đầu HĐ + `listMeal`, 1 transaction; khách tồn tại, từ ≤ đến, số không trùng, ≥ 1 suất, MealType duy nhất, giá > 0; số `HD-YYMMDD-NNN` – `DocumentKind.Contract` mới, ghi song song `ContractNumber / ContractNo`; client gửi số thì giữ), `Submit` (Nháp → Chờ duyệt + ApprovalRequest `CONTRACT`), `Approve` / `Reject` (qua `ApprovalRequestService.Decide`; hash đầu HĐ + suất; duyệt → Hiệu lực, HĐ quá `effectiveTo` → chặn; từ chối / hủy → Nháp), `Terminate` (Hiệu lực, lý do, ngày chấm dứt trong khoảng hiệu lực, chặn khi còn đơn Nháp … Đang SX có ngày ăn sau ngày chấm dứt → Terminated), `GetDetail` (suất, allowedActions, yêu cầu duyệt chờ, số đơn mở). CRUD: Insert luôn Nháp (bỏ Status client), Update / Delete chỉ Nháp (Delete kèm suất); ContractMeal chỉ ghi khi HĐ Nháp, MealType không trùng. `OrderService.ActiveContractAsync` nhận cả HĐ Chấm dứt còn hiệu lực ở ngày ăn.
- **Dự báo** (`ForecastService` / `ForecastController`, `ForecastRules` mới): `Import` (JSON, kiểm từng dòng "Dòng n: …", khóa (khách, điểm, ngày, ca, loại suất), bản mới Nháp `VersionNo` + 1, bản Nháp cũ → Superseded, bản Đã công bố cũ giữ tới khi công bố bản mới; ghi song song `ForecastQuantity / Quantity`), `Publish` (form, khoảng ngày + `ListCustomerId`; bản đã công bố cũ → Superseded), `GetMatrix` (khách × ngày, `values` đã công bố + `draftValues`, totals, ≤ `ForecastMaxDays` 62). CRUD Insert → 400; Update / Delete chỉ Nháp.
- **Đơn hàng** (`OrderService` / `OrderController`): `GetNewTemplate.suggestedQuantity` = dự báo (điểm riêng, hoặc chia đều dự báo cấp khách cho điểm chưa có); `Confirm` tính lệch dự báo, > `ForecastVariancePctWarning` (10%) → cảnh báo + Note `[LECH_DU_BAO]` (không chặn); `GetDetail` + `forecastQuantity / forecastVariancePercent / forecastVarianceExceeded / pendingApprovalType / listLine[].forecastQuantity`, sau giờ chốt `allowedActions` + `requestChange`. **`RequestChange`** (JSON: số suất mới + `listNewLine`, lý do; chỉ sau giờ chốt; Nháp / Đã gửi / Đã xác nhận / Đã chốt) → ApprovalRequest `ORDER_CHANGE_AFTER_CUTOFF` kèm đề xuất; `Create` + `afterCutoffReason` sau giờ chốt → đơn Nháp + yêu cầu cùng loại. Handler `OrderChangeAfterCutoffApprovalHandler` (mục `crm-order`): duyệt → thêm dòng (giá / thực đơn tra lúc duyệt), ghi số, đơn → Đã chốt, Note `[SAU_GIO_CHOT]` (+ nhắc CalculateTopUp nếu đã có kế hoạch); từ chối → không đổi (thêm đơn → Hủy). Hash coi Đã xác nhận ↔ Đã chốt là cùng giai đoạn (nút chốt 20h không làm hỏng yêu cầu). Đang chờ duyệt (bất kỳ loại) → chặn UpdateLines / RequestChange; LockByMealDate liệt kê cả 2 loại.
- **Đổi món** (`MenuChangeRequestService` / `Controller` – gỡ header auto-generated; handler `MenuChangeApprovalHandler`): `Create` (form; lý do, 2 món khác nhau; có đơn → đơn chưa giao, ngày ăn của đơn, món cũ phải có trong thực đơn của đơn ở thứ đó, số suất mặc định = suất dòng đơn; không đơn → ngày + số suất bắt buộc; số `PH-` (Voucher thêm đích `MenuChangeRequest.RequestNo`); ApprovalRequest `MENU_CHANGE`), `Approve` / `Reject`, `GetDetail` (tên món / đơn / ca, kế hoạch ngày + `planNeedsManualAction`). Duyệt → Approved + ApprovedBy / ApprovedAt, kế hoạch **Nháp** của ngày: món cũ − số suất, món mới +; hủy (ApprovalRequest/Cancel) → Cancelled. `ProductionRules.ApplyMenuChanges` + `ApprovedMenuChangesAsync`: `DishDemandsAsync` (Generate, chia mẻ đóng gói, truy xuất) và MRP `ComputeAsync` (Calculate / CalculateTopUp) áp đổi món đã duyệt. CRUD Insert / Update → 400, Delete chỉ Từ chối / Hủy.
- **Engine**: `ApprovalRequest/GetDetail` (`business`) thêm CONTRACT (Contract + ContractMeal), MENU_CHANGE, ORDER_CHANGE_AFTER_CUTOFF. Không sửa engine: cả 3 loại mới chạy **1 cấp T12**; handler chỉ thực thi ở bước cuối (`ApproveAsync`) → **BE2.3 chuyển hợp đồng (79) / sửa đơn sau giờ chốt sang nhiều bước chỉ cần sửa engine** (`CreateAsync` tạo nhiều `ApprovalStep`, `Decide` chỉ gọi handler ở bước cuối), không đổi handler / service nghiệp vụ.
- Cấu hình: `BusinessRules:ForecastVariancePctWarning` (10), `ForecastMaxDays` (62). Thông báo tiếng Việt trong `ConstantHelper.Message`; trạng thái `ContractStatus`, `ForecastStatus`, `MenuChangeStatus`, `OrderStatus.ChangeAfterCutoffBusinessType` (`Model/Helper/OrderStatus.cs`).
- **Không có SP / seed mới**, không bảng / cột, không mục menu mới → `deploy/db/scripts.order` không thêm dòng (ghi ở `DB/README.md`).

File chính: `AppDelicare.Services/Implement/{ContractService, ContractMealService, ForecastService, ForecastRules (mới), MenuChangeRequestService, OrderService, ProductionRules, MaterialRequirementService, ProductionPlanService (chú thích), ApprovalRequestService, DocumentNumberService}.cs`, `Implement/Approval/{ContractApprovalHandler, MenuChangeApprovalHandler, OrderChangeAfterCutoffApprovalHandler}.cs` (mới), interface tương ứng + `IDocumentNumberService`, `WebApi/Controllers/{Contract, Forecast, MenuChangeRequest, Order}Controller.cs`, `Model/Helper/{OrderStatus, ConstantHelper}.cs`, `Model/Option/BusinessRulesOptions.cs`, `Model/Model/{ContractForecastModels (mới), OrderModels}.cs`, `Model/Request/Custom/{ContractForecastCustomRequest (mới), OrderCustomRequest}.cs`, `WebApi/appsettings.json`; test `Tests/Sales/{ContractForecastTests, ContractForecastSqlServerTests}.cs` (mới) + sửa `OrderTests` (allowedActions sau giờ chốt có `requestChange`), `PilotFlowSqlServerTests` (Submit + Approve hợp đồng); `docs/api-tests/be2.2-contract-forecast.http` (mới), `pilot-flow.http`, `be2.1-flow-gaps.http`; `docs/frontend-handoff/BE2.2.md` (mới), `README.md`; `docs/claude/{DECISIONS, ROADMAP, BUSINESS_RULES, PROGRESS}.md`, `DB/README.md`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (cảnh báo cũ).
- `dotnet test`: **194/194** (baseline 185 + 9; không mất test, không test bị bỏ qua):
  - `ContractForecastTests` (8): hợp đồng – chặn từ > đến / không suất / suất trùng / giá 0, Create HD-261208-NNN Nháp, loại suất chỉ khi Nháp, Submit → XN- + hộp duyệt `crm-contract`, người không quyền Approve → 400, admin duyệt → Active + ApprovalDecision, CRUD Update HĐ Hiệu lực → 400, CRUD Insert `Status=Active` → Nháp, Terminate (thiếu lý do / còn đơn → 400, xong → Terminated, đơn tới ngày chấm dứt vẫn tạo được); từ chối → Nháp + lý do, giữ số HĐ giấy + chặn trùng, sửa giá sau khi gửi → không duyệt được; dự báo – import chặn (trống / điểm khác khách / trùng khóa), v1 Nháp không gợi ý, công bố → gợi ý A 100 / B 51 (cấp khách), v2 Nháp → ma trận 151 + draft 120, công bố → 171 (+ superseded 1), ma trận > 62 ngày → 400, CRUD Insert / Update bản công bố → 400, xác nhận +12,5% → cảnh báo + Note, +7,84% không cảnh báo; `ForecastRules` chia đều / phần dư / lệch; đổi món – chặn thiếu lý do / món không có trong thực đơn / 2 món trùng, PH-, hộp duyệt, duyệt → kế hoạch gà 70 / cá 30, sinh lại kế hoạch vẫn giữ, hủy qua ApprovalRequest/Cancel → Cancelled + xóa được, kế hoạch đã duyệt → `planNeedsManualAction`; `ApplyMenuChanges` theo đơn / ca, giới hạn số đang có; sửa đơn sau giờ chốt – trước giờ chốt → 400, UpdateLines sau giờ chốt → 400, thiếu lý do / không đổi → 400, gửi → chưa ghi, chờ duyệt chặn lần 2, LockByMealDate giữa chừng vẫn duyệt được → 120 + dòng SANG 20, Locked, tổng 5,2 triệu; từ chối → không đổi; thêm đơn sau giờ chốt (thiếu lý do → 400, có lý do → Nháp + yêu cầu → duyệt Locked; từ chối → Hủy).
  - `ContractForecastSqlServerTests` (1, SQL Server dev, đã chạy): HĐ Create (HD- qua sp_getapplock) → Submit → duyệt; dự báo Import → Publish → Matrix → gợi ý 100; đơn 120 → xác nhận lệch +20% + Note; đổi món PH- → duyệt; thêm đơn hôm nay sau giờ chốt → duyệt → Locked → RequestChange → duyệt (35 × 38 000); rollback, số dòng Contract / Forecast / MenuChangeRequest không đổi.
- **Chạy thật trên DB dev** (`dotnet run` WebApi Development cổng 5322, trình chạy nhỏ đọc cú pháp REST Client – kiểm `responseCode` 200 / 400 theo "→ 400"; phản hồi file .xlsx tính đạt):
  - `be2.2-contract-forecast.http` **73/73** (15 chốt an toàn trả 400), key B22D, ngày 17/12/2026: hợp đồng `HD-261117-001` Active, `DEMOB22DHDB` Terminated; dự báo v1 điểm A1 Superseded, cấp khách 51 + v2 A1 120 Published; đơn `PH-261117-0115` 135 suất Confirmed (lệch +12,5%); kế hoạch ngày 17/12: gà 105 / cá 30; đổi món `PH-261117-0116` Approved, `-0117` Rejected; đơn sau giờ chốt `PH-261117-0118` (ngày ăn 16/11) Locked 45 suất / 1 600 000 đ.
  - `pilot-flow.http` **123/123** (+2 bước gửi duyệt / duyệt hợp đồng; 8 chốt), key B22A, ngày 20/01/2027.
  - `be2.1-flow-gaps.http` **142/142** (+4 bước; 11 chốt), key B22C, ngày 09/12/2026. Lần đầu (ngày 27/01/2027) sai 8 bước vì lô tồn đầu kỳ của file hạn +60 ngày → ngày ăn > 60 ngày thì MRP đề nghị cả gà (không có NCC); đã ghi chú vào đầu file (không phải lỗi BE2.2).
  - Dữ liệu `[DEMO]` để lại trên DB dev (như BE2.1). Không chạy script SQL nào.
- `git status`: không thay đổi trong `app-delicare-client/`, `docs/reference/`, `deploy/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không `api/v1` mới.

AC (BE2.2): [x] mọi mục phạm vi có API + test · [x] duyệt qua engine T12 (không đường tắt), thiết kế sẵn cho BE2.3 · [x] `.http` mới chạy thật + 2 file cũ chạy lại · [x] handoff · [x] không bảng / cột / SP / seed mới · [ ] Catalog Excel (máy không có openpyxl – bảng dưới).

Bảng cập nhật `DELICARE_API_Catalog.xlsx` (sheet `API_Catalog`, cột J / L / K; cột M = `BE2.2`):
| STT | Trạng thái (J) | API thực tế – khung cũ (L) | Ghi chú (K) |
|---|---|---|---|
| 78 | Đã xong | `POST Contract/Create` (JSON), `GET Contract/GetDetail` | Số HD-YYMMDD-NNN (GAP-CTR-01); CRUD Insert luôn Nháp |
| 79 | Đã xong | `POST Contract/Submit`, `Contract/Approve`, `Contract/Reject` (+ `ApprovalRequest/Decide`) | Engine 1 cấp T12 (CONTRACT, mục `crm-contract`); nhiều cấp → BE2.3 |
| 80 | Đã xong | `POST Contract/Terminate` | Chặn khi còn đơn chưa giao sau ngày chấm dứt (D-51 d) |
| 81 | Không làm | – | Job hết hạn để sau (D-45); kiểm hạn lúc tạo đơn / duyệt |
| 87 | Đã xong | `GET Forecast/GetMatrix` | + draftValues; ≤ 62 ngày |
| 88 | Đã xong | `POST Forecast/Import` (JSON) | Bản đã công bố cũ giữ tới khi công bố bản mới (D-51 e) |
| 89 | Đã xong | `POST Forecast/Publish` | |
| 91 | Đã xong | `GET Order/GetDetail` | (T13) + forecastQuantity, pendingApprovalType, requestChange |
| 92 | Đã xong | `GET Order/GetNewTemplate` | suggestedQuantity = dự báo (D-51 f) |
| 96 | Đã xong | `POST Order/Confirm` | Lệch dự báo > 10% cảnh báo + Note `[LECH_DU_BAO]` (D-51 g) |
| 104 | Đã xong | `POST MenuChangeRequest/Create`, `Approve`, `Reject`, `GET GetDetail` | Áp vào kế hoạch Nháp + mọi lần tính lại (GAP-MCR-01) |
| 137 | Đã xong | `POST ProductionPlan/Generate` | (BE2.1) + áp đổi món đã duyệt |
| – | (BUSINESS_RULES §2) | `POST Order/RequestChange` (JSON), `Order/Create` + `afterCutoffReason` | ORDER_CHANGE_AFTER_CUTOFF, mục `crm-order` (D-51 i) |

PROPOSED chờ anh (DECISIONS **D-51** (a)–(j), mẫu D-49 – em đã cài theo "Đề xuất", không mục nào BLOCKED): (a) số HĐ `HD-YYMMDD-NNN`; (b) CRUD hợp đồng / loại suất khóa theo trạng thái; (c) duyệt HĐ 1 cấp CEO tới BE2.3; (d) chấm dứt chặn khi còn đơn sau ngày chấm dứt, đơn tới ngày chấm dứt vẫn nhận; (e) bản dự báo đã công bố giữ tới khi công bố bản mới; (f) dự báo cấp khách chia đều cho điểm; (g) lệch dự báo chỉ cảnh báo + Note; (h) đổi món áp kế hoạch Nháp + mọi lần tính lại; (i) sửa / thêm đơn sau giờ chốt qua duyệt, xong → Đã chốt, dùng chung mục duyệt `crm-order`; (j) đơn chưa xác nhận lúc chốt xin nhận muộn qua (i).

Cần anh: không có script SQL mới. Khi gộp nhánh: appsettings có 2 khóa mới (có mặc định trong code). FE: đọc `docs/frontend-handoff/BE2.2.md` mục "Thay đổi hành vi" (hợp đồng luôn Nháp → phải gửi duyệt + duyệt; CRUD loại suất / dự báo / đổi món bị giới hạn).

### BE2.3 – Duyệt nhiều cấp (T12 mở rộng) + áp vào nghiệp vụ · 2026-11-17 · DONE
Nhánh `feat/BE2.3-multilevel-approval` (worktree `wt-BE23`, từ `release/pilot` @ `69aad11` = T48.3 + BE2.1 + BE2.2). Chỉ backend (D-49); không đụng `app-delicare-client/`, `docs/reference/*`.

Kế hoạch:
1. Rà soát: engine T12 (`ApprovalRequestService` + `IApprovalHandler`) tạo 1 bước, `Decide` luôn là cấp cuối; `PurchaseRequest` chưa có Submit (Convert nhận cả Nháp); `StockCount/Post` > 2% chỉ admin; `InventoryTransaction/Adjust` ghi thẳng; mục duyệt `scm-stockcount`, `scm-adjustment` đã có, chưa có `scm-pr-l1/l2`. → bổ sung vào đúng service / controller cũ, 3 handler mới, không service / controller mới.
2. Engine nhiều cấp (D-46 làm trước): bước theo quy trình, InProgress, hạn, mỗi cấp 1 người, hủy ở mọi cấp, cấu hình `ApprovalSteps`.
3. Đề nghị mua 2 cấp + seed (CodeGen `Seeds.ExtraApprovals`), cập nhật test / `.http` cũ.
4. Áp vào kiểm kê (131, 132), điều chỉnh tồn tay, PO theo ngưỡng, hợp đồng (cấu hình).
5. Test InMemory + SQL Server rollback; `.http` mới + chạy lại 3 file luồng; handoff; DECISIONS D-52 / GAP.

Đã làm (mẫu 4.2):
- **Engine** (`ApprovalRequestService`, `IApprovalHandler`): `IApprovalHandler.StepScreenIdsAsync` (mặc định 1 bước = `RequiredScreenIdAsync` → handler cũ không sửa); cấu hình `BusinessRules:ApprovalSteps[BusinessType]` ghi đè. `CreateAsync` tạo 1 `ApprovalStep` / cấp (cấp 1 giao ngay + `DueAt`, cấp sau `Waiting` chưa giao), `ApprovalItem` lưu quy trình (`ActionCode APPROVE`, `{"steps":[…]}` – GAP-APR-02), `ApprovalRequest.DueAt`. `Decide`: quyền theo mục của **cấp đang chờ**, hash kiểm mọi cấp, cấp chưa cuối → giao cấp sau, yêu cầu `InProgress` (message "Đã duyệt cấp 1/2 – chuyển cấp 2 (…)"), không gọi handler; cấp cuối → handler; từ chối → cấp sau `Skipped`, handler nhận `[Cấp n/N] lý do`. Mỗi cấp 1 người (`ApprovalRequireDistinctApprovers`, admin miễn). Hạn 48 giờ / cấp (`ApprovalStepDueHours`, `…ByType`), quá hạn = cờ `isOverdue` (+ `[QUÁ HẠN]` ở ghi chú bước), `ApprovalBlockOverdueDecision` để chặn. `GetMyPending` theo cấp đang chờ (+ `requestStatus, stepNo, stepCount, stepName, dueAt, isOverdue`); `GetDetail` + `listStepInfo`, `currentStepNo`, `stepCount`, `dueAt`, `isOverdue`, business PurchaseRequest / StockCount / STOCK_ADJUSTMENT; `Cancel` ở mọi cấp (cấp sau Skipped). `OpenProgressAsync` (cấp đang chờ của 1 chứng từ) dùng ở chi tiết PR / PO / kiểm kê. `OpenStatuses` (Pending, InProgress) thay mọi kiểm tra `"Pending"` ở Contract / MenuChange / Order / PO / Recipe service.
- **Đề nghị mua 2 cấp (D-46)** – `PurchaseRequestService` / `Controller`: `Submit` (Nháp, ≥ 1 dòng → Chờ duyệt + PURCHASE_REQUEST), `Approve` / `Reject` (`[SkipPermission]`, engine kiểm `scm-pr-l1` / `scm-pr-l2`), handler `PurchaseRequestApprovalHandler` (cấp cuối → Đã duyệt; từ chối / hủy → Nháp + Note). `ConvertToPurchaseOrder` chỉ Đã duyệt; `GetDetail` + `pendingApproval`, `allowedActions` theo trạng thái. Đề nghị do MRP lập cũng phải gửi duyệt (khóa tính lại khi PendingApproval / Approved đã có).
- **Kiểm kê (131, 132)** – `StockCountService` / `Controller`: `Submit` (dòng lệch có lý do; lệch > 2% → Chờ duyệt + STOCK_COUNT 2 cấp `scm-stockcount` → `scm-stockcount-l2`; ≤ 2% → 400), `Approve` / `Reject`, handler `StockCountApprovalHandler` (cấp cuối ghi ApprovedBy / ApprovedAt, giữ Chờ duyệt; từ chối → Đang kiểm); `Post` lệch > 2% chỉ khi đã duyệt (admin không còn ghi thẳng), giữ người duyệt. Chi tiết + `isApproved`, `pendingApproval`, `allowedActions` mới.
- **Điều chỉnh tồn tay** – `InventoryTransactionService.Adjust` → `InventoryAdjustResultModel`: mặc định gửi duyệt STOCK_ADJUSTMENT (nội dung ở proposal, BusinessId = khóa nối, giảm quá tồn chặn ngay), handler `StockAdjustmentApprovalHandler` duyệt → bút toán `ManualAdjustment` với `ReferenceId` = khóa nối (số PH- lúc duyệt); từ chối không ghi. `StockAdjustmentRequireApproval` = false → ghi ngay như T16.
- **PO theo ngưỡng**: `PurchaseOrderService.ApprovalStepScreenIdsAsync` – mặc định D-11 (1 cấp); `PurchaseOrderDirectorTwoLevel` = true → PO ≥ 50 triệu 2 cấp `scm-po` → `scm-po-director`. PO detail + `pendingApproval`.
- **Hợp đồng (79)**: handler BE2.2 không đổi; mặc định 1 cấp `crm-contract` (spec), 2 cấp bằng cấu hình `ApprovalSteps:CONTRACT` (có test).
- **Seed**: `tools/AppDelicare.CodeGen/Seeds.cs` `ExtraApprovals` + `scm-pr-l1` (OPS-SCM), `scm-pr-l2` (COO), `scm-stockcount-l2` (FIN-ACC) → sinh lại `DB/seed/ObjectMenu_seed.sql`, `UserRole_seed.sql` bằng `--layers security` (chỉ thêm dòng; 2 file đã nằm trong `deploy/db/scripts.order`). **Đã chạy 2 seed trên DB dev** (`sqlcmd -f 65001`), kiểm 3 mục + quyền. Không SP mới, không bảng / cột.
- Cấu hình `appsettings.json` (có chú thích): `ApprovalSteps`, `ApprovalStepDueHours`, `ApprovalStepDueHoursByType`, `ApprovalBlockOverdueDecision`, `ApprovalRequireDistinctApprovers`, `PurchaseOrderDirectorTwoLevel`, `StockAdjustmentRequireApproval`. Thông báo mới trong `ConstantHelper.Message`.

File chính: `Services/Implement/{ApprovalRequestService, PurchaseRequestService, StockCountService, InventoryTransactionService, PurchaseOrderService, ContractService, MenuChangeRequestService, OrderService, RecipeService}.cs`, `Implement/Approval/{PurchaseRequest, StockCount, StockAdjustment}ApprovalHandler.cs` (mới), `PurchaseOrderApprovalHandler.cs`, `Interface/{IApprovalHandler, IApprovalRequestService, IPurchaseRequestService, IStockCountService, IInventoryTransactionService, IPurchaseOrderService}.cs`, `WebApi/Controllers/{PurchaseRequest, StockCount, InventoryTransaction}Controller.cs`, `WebApi/appsettings.json`, `Model/{Option/BusinessRulesOptions, Helper/ConstantHelper, Helper/PurchasingStatus, Helper/InventoryStatus, Model/ApprovalModels, Model/FlowGapModels, Model/InventoryModels, Model/PurchasingModels}.cs`, `tools/AppDelicare.CodeGen/Seeds.cs`, `DB/seed/{ObjectMenu, UserRole}_seed.sql`; test `Tests/Approval/{MultiLevelApprovalTests, MultiLevelApprovalSqlServerTests}.cs` (mới), `Tests/Integration/{ApprovalTestHelpers (mới), InMemoryApiFactory (Settings)}.cs`, sửa `PurchasingTests`, `MrpTests`, `PurchasingSqlServerTests`, `InventoryTests`, `PilotFlowSqlServerTests`; `docs/api-tests/be2.3-multilevel-approval.http` (mới), `pilot-flow.http`, `be2.1-flow-gaps.http`, `p4-inventory.http`, `p6-purchasing.http`; `docs/frontend-handoff/BE2.3.md` (mới), `README.md`; `docs/claude/{DECISIONS, ROADMAP, BUSINESS_RULES, PROGRESS}.md`, `DB/README.md`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (cũ).
- `dotnet test`: **203/203** (baseline 194 + 9; không mất test, không bỏ qua):
  - `MultiLevelApprovalTests` (8): PR 2 cấp với 3 user thật (người gửi, cấp 1, cấp 2) – bước / hạn / ApprovalItem, cấp 2 chưa tới lượt không thấy / không duyệt, cấp 1 → InProgress, Convert chặn, cấp 2 → Approved + 2 ApprovalDecision, Convert OK; từ chối cấp 2 → Nháp "[Cấp 2/2]", gửi lại, hủy giữa chừng (người khác hủy → 400), bước Approved / Cancelled, CANCEL; 1 người 2 cấp bị chặn + nội dung sửa giữa 2 cấp bị chặn; quá hạn = cờ + `[QUÁ HẠN]` + hạn cấp 2 tính từ lúc giao; tùy chọn chặn quá hạn (factory riêng); cấu hình hợp đồng 2 cấp + PO 2 cấp theo ngưỡng (PO nhỏ vẫn 1 cấp); điều chỉnh tồn (giảm quá tồn chặn, tiêu đề hộp duyệt, quyền, duyệt → bút toán, từ chối → không ghi); yêu cầu cũ không có quy trình lưu vẫn duyệt theo handler.
  - `MultiLevelApprovalSqlServerTests` (1, SQL Server dev, đã chạy): điều chỉnh → duyệt → bút toán PH-; kiểm kê 20% → Submit → 2 cấp → Post; PR cấp 1 → từ chối cấp 2 → gửi lại → 2 cấp (UNIQUE ApprovalStep, XN- khác nhau); rollback.
  - Test cũ sửa theo luật mới: Convert sau khi duyệt 2 cấp (`ApprovePurchaseRequestAsync`), Adjust + duyệt, kiểm kê > 2% Submit + 2 cấp (admin cũng không Post thẳng), Pilot SQL thêm bước duyệt PR + điều chỉnh.
- **Chạy thật trên DB dev** (WebApi Development cổng 5323, trình chạy REST Client nhỏ – kiểm `responseCode` theo "→ 400"): `be2.3-multilevel-approval.http` **58/58** (8 chốt, key B23A); `pilot-flow.http` **131/131** (9 chốt, key B23P, ngày 03/02/2027); `be2.1-flow-gaps.http` **147/147** (key B23G, ngày 31/12/2026); `be2.2-contract-forecast.http` **73/73** (không phải sửa – hợp đồng vẫn 1 cấp; key B23C, ngày 07/01/2027). Dữ liệu `[DEMO]` để lại trên DB dev.
- `git status`: không thay đổi trong `app-delicare-client/`, `docs/reference/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không `api/v1` mới.

AC (BE2.3): [x] engine nhiều cấp theo ApprovalStep, hạn (cờ), hủy · [x] PR 2 cấp D-46 làm trước, Convert chỉ Đã duyệt, PR do MRP cũng duyệt · [x] áp kiểm kê 131/132, điều chỉnh tồn, PO theo ngưỡng, hợp đồng (cấu hình) · [x] handler 1 cấp cũ giữ nguyên (mặc định) · [x] test + `.http` · [x] handoff · [ ] Catalog Excel (máy không có openpyxl – bảng dưới).

Bảng cập nhật `DELICARE_API_Catalog.xlsx` (sheet `API_Catalog`) – STT catalog → Trạng thái / API thực tế / Task:
| STT | Trạng thái (J) | API thực tế – khung cũ (L) | Task (M) | Ghi chú (K) |
|---|---|---|---|---|
| 79 | Đã xong | `POST Contract/Submit`, `Approve`, `Reject` (+ `ApprovalRequest/Decide`) | BE2.2, BE2.3 | Engine nhiều cấp; mặc định 1 cấp `crm-contract`, đổi bằng `ApprovalSteps:CONTRACT` (D-52 f) |
| 110 | Đã xong | `POST PurchaseRequest/Submit`, `Approve`, `Reject` | BE2.3 | 2 cấp `scm-pr-l1` → `scm-pr-l2` (D-46) |
| 111 | Đã xong | `POST PurchaseRequest/ConvertToPurchaseOrder` | T15, BE2.3 | Chỉ Đã duyệt |
| 131 | Đã xong | `POST StockCount/Submit`, `Approve`, `Reject` | BE2.3 | Lệch > 2% → STOCK_COUNT 2 cấp (D-52 f, i) |
| 132 | Đã xong | `POST StockCount/Post` | T16, BE2.3 | Lệch > 2% chỉ khi đã duyệt (ApprovedBy) |
| 210 | Đã xong | `GET ApprovalRequest/GetMyPending?type=` | T12, BE2.3 | Theo cấp đang chờ; stepNo / stepCount / dueAt / isOverdue |
| 211 | Đã xong | `GET ApprovalRequest/GetDetail` | BE2.1, BE2.3 | + listStepInfo, currentStepNo, stepCount |
| 212 | Đã xong | `POST ApprovalRequest/Decide` | T12, BE2.3 | Nhiều cấp, mỗi cấp 1 người, hạn = cờ |
| 213 | Đã xong | `POST ApprovalRequest/Cancel` | BE2.1, BE2.3 | Hủy ở mọi cấp, cấp sau Skipped |
| – (7.6 adjust) | Đã xong | `POST InventoryTransaction/Adjust` → duyệt STOCK_ADJUSTMENT | BE2.3 | GAP-INV-12 (D-52 h) |
| – (PO theo ngưỡng) | Đã xong | `PurchaseOrder/Submit` / `Approve` | T15.1, BE2.3 | `PurchaseOrderDirectorTwoLevel` (mặc định false = D-11) |

PROPOSED chờ anh (DECISIONS **D-52** (a)–(j), mẫu D-49 – em đã cài theo "Đề xuất", không mục nào BLOCKED): (a) nhóm duyệt cấp 1 OPS-SCM / cấp 2 COO; (b) trạng thái InProgress; (c) quy trình lưu ở ApprovalItem (GAP-APR-02); (d) mỗi cấp 1 người, admin miễn; (e) hạn 48 giờ = cờ (GAP-APR-03); (f) số cấp: PR 2, kiểm kê 2 (cấp 2 Kế toán FIN-ACC), còn lại 1; (g) PO giữ D-11, 2 cấp chỉ khi bật cấu hình; (h) mọi điều chỉnh tồn tay phải duyệt (GAP-INV-12); (i) kiểm kê duyệt xong giữ Chờ duyệt tới khi Post, từ chối → Đang kiểm; (j) lý do từ chối kèm cấp.

Cần anh: không có SP mới; seed ObjectMenu / UserRole sinh lại (đã chạy trên DB dev; staging / Docker chạy qua `Deploy-Database.ps1` / `db-init`). FE: đọc `docs/frontend-handoff/BE2.3.md` mục "Thay đổi hành vi" (lập PO chỉ từ đề nghị Đã duyệt, `Adjust` trả yêu cầu duyệt, kiểm kê > 2% phải Submit, trạng thái `InProgress`). Mục duyệt cũ `#/wf-inbox?type=scm-pr` không còn dùng – anh tắt trên màn menu.

### BE2.4 – Kho & sản xuất mở rộng · 2026-11-17 · DONE
Nhánh `feat/BE2.4-inventory-production` (worktree `wt-BE24`, từ `release/pilot` @ `eae2b71` = T48.3 + BE2.1 + BE2.2 + BE2.3). Chỉ backend (D-49); không đụng `app-delicare-client/`, `docs/reference/*`.

Kế hoạch:
1. Rà soát: `MaterialReservation`, `MaterialConsumptionVariance`, `ActualCost`, `MealCost` chỉ có CRUD sinh mã (+ SP `GetAll`), chưa nghiệp vụ nào ghi; `InventoryBalance.ReservedQuantity` luôn 0; MRP `ExpectedIncomingQuantity` = 0; `ProductionOrder` chưa có hủy / đóng; mẻ chỉ ghi trạm bếp (công đoạn theo tổ DEFERRED); hết hạn lô chỉ chặn lúc xuất. Mục menu của mọi màn hình (`scm-reservation`, `scm-consumption-var`, `cost-actual`, `cost-meal`) đã có → không seed menu / quyền.
2. Giữ chỗ khi phát hành + phân bổ FEFO lên tồn, xuất cho lệnh dùng phần đã giữ (126).
3. MRP trừ hàng sắp về từ PO.
4. Hủy / đóng lệnh SX (144, 145), chênh lệch tiêu hao (135), giá thành thực tế (152, 153), giá vốn suất (154).
5. Công đoạn theo tổ (Sơ chế → Nấu → Chia suất, người / tổ) + tiến độ bếp theo tổ; xử lý lô hết hạn (thao tác tay, D-45).
6. Test InMemory + SQL Server rollback; `.http` mới + chạy lại 4 file luồng; handoff; DECISIONS D-53 / GAP.

Đã làm (mẫu 4.2 – thêm method / action vào đúng service / controller cũ; 3 lớp tĩnh dùng chung mới như `ProductionRules`: `ReservationRules`, `ProductionStageRules`, `CostingRules`; không service / controller mới):
- **Giữ chỗ (126)** – `ReservationRules`: `ProductionPlan/Release` giữ chỗ mỗi (lệnh SX, nguyên liệu) = min(cần, khả dụng tại ngày SX − giữ chỗ khác), thiếu → vẫn phát hành + cảnh báo "giữ chỗ … a / b – thiếu c"; `InventoryBalance.ReservedQuantity` = tổng giữ chỗ còn hiệu lực phân bổ FEFO lên lô xuất được, **tính lại sau mỗi bút toán của nguyên liệu** (`InventoryLedgerService`) và mỗi lần giữ / xuất / giải phóng. `MaterialIssueService.Create / Post`: phiếu của lệnh đã giữ dùng phần của lệnh (khả dụng = tồn − giữ chỗ lệnh khác); ghi sổ xong → giữ chỗ `PartiallyIssued` / `Issued`. `ProductionOrder/Reserve` giữ bổ sung. Chi tiết kế hoạch / lệnh + `listReservation`; `MaterialReservation/GetAll` (SP cũ) dùng cho màn `scm-reservation`.
- **MRP hàng sắp về** – `MaterialRequirementService.ComputeAsync`: Σ dòng PO Đã duyệt / Đã gửi / Nhận một phần có ngày giao dự kiến ≤ ngày SX − đã nhận đạt (`PurchasingRules.ReceiptTotalsAsync`), quy đổi về đơn vị cơ sở; PO chưa có ngày giao → cảnh báo; `CalculateTopUp` bỏ PO của chính đề nghị kế hoạch. Cấu hình `MrpIncludeIncomingPurchaseOrders`.
- **Hủy lệnh (145)** – `ProductionOrder/Cancel` (lý do): chỉ Phát hành + mọi mẻ Kế hoạch + chưa có phiếu Đã xuất; phiếu Nháp → Hủy, mẻ → `Cancelled` (trạng thái mới), giải phóng giữ chỗ; ca không còn lệnh → kế hoạch về Đã duyệt (phát hành lại được). Giao (`DeliveryRules.BatchesAsync`) bỏ mẻ Hủy.
- **Đóng lệnh (144) + chênh lệch tiêu hao (135)** – `ProductionOrder/Close`: Hoàn thành → Đóng; `MaterialConsumptionVariance` theo nguyên liệu (chuẩn = định lượng × phần nấu được, thực tế = Σ phiếu xuất Đã xuất, % / giá trị, cờ vượt `ConsumptionVariancePctWarning`); giá thành; giải phóng giữ chỗ dư. `MaterialConsumptionVariance/GetList` (lệnh / từ–đến / nguyên liệu / chỉ vượt ngưỡng).
- **Giá thành thực tế (152, 153)** – `CostingRules` + `ProductionOrder/CalculateCost`: `ActualCost` 1 dòng / mẻ (NVL theo đơn giá lô, phiếu không gắn mẻ chia theo phần; nhân công / chung theo cấu hình; hao hụt chỉ phân tích); `ActualCost/GetList` (theo lệnh + cơ cấu chi phí + giá chuẩn / phần).
- **Giá vốn suất (154)** – `MealCost/Calculate` (đơn Đã giao / Đã đối soát): 1 dòng / (dòng đơn, món); suất nhận đối soát → POD → suất đặt; giá / phần = giá thành lệnh SX của món / ca ÷ phần món của ngày / ca, lệnh chưa tính → giá chuẩn; doanh thu dòng đơn chia theo tỉ lệ giá vốn; `MealCost/GetList`.
- **Công đoạn theo tổ** – `ProductionStageRules` + `ProductionBatch/StartStage`, `FinishStage`, `GetStageBoard`: danh mục = checklist `PRD_STAGE` (seed mới), sự kiện = `FoodSafetyChecklistResult` (STARTED / DONE, tổ + người trong `ActualValue`); không nhảy công đoạn; bắt đầu Nấu = bắt đầu mẻ; Chia suất chỉ khi Đạt QC; mẻ đã ghi công đoạn → `Finish` cần Nấu xong, `Pack` cần Chia suất xong (`ProductionRequireStages` = false giữ mẻ không ghi công đoạn như T17). Chi tiết mẻ + `listStage`, `allowedActions` `startStage` / `finishStage`; bảng tiến độ theo (công đoạn, tổ): chờ / đang / xong + số phần.
- **Lô hết hạn** – `MaterialLot/ProcessExpired` (kho tùy chọn, `Preview`): lô Khả dụng hạn < hôm nay → Hết hạn + WASTE toàn bộ tồn (`ReferenceType = LotExpiry`, 1 số PH-), lô tạm giữ bỏ qua, lô sắp hết hạn liệt kê.
- CRUD Insert / Update của `ActualCost`, `MealCost`, `MaterialConsumptionVariance`, `MaterialReservation` → 400 (chỉ qua nghiệp vụ).
- Cấu hình `BusinessRules` (appsettings, PROPOSED): `ProductionReserveOnRelease`, `MrpIncludeIncomingPurchaseOrders`, `ProductionRequireStages`, `ProductionStageTeams`, `ProductionLaborCostPerHour`, `ConsumptionVariancePctWarning`. Hằng `MaterialReservationStatus`, `ProductionBatchStatus.Cancelled`, `ProductionStageStatus`, `ProductionCode.Stage*`, `InventoryReferenceType.LotExpiry`; thông báo tiếng Việt trong `ConstantHelper.Message`.
- **Seed mới `DB/seed/ProductionStage_seed.sql`** (checklist `PRD_STAGE`: SO_CHE / NAU / CHIA_SUAT) – đã thêm vào `deploy/db/scripts.order` (sau `FoodSafetyChecklist_seed.sql`) + `DB/README.md`; **đã chạy trên DB dev 2 lần** (`sqlcmd -f 65001`, lần 2 không thêm dòng). Không SP mới, không bảng / cột.

File chính: `Services/Implement/{ReservationRules, ProductionStageRules, CostingRules}.cs` (mới), `{InventoryLedgerService, MaterialIssueService, MaterialRequirementService, ProductionPlanService, ProductionOrderService, ProductionBatchService, MaterialLotService, ActualCostService, MealCostService, MaterialConsumptionVarianceService, MaterialReservationService, DeliveryRules}.cs`, `Interface/I{ProductionOrder, ProductionBatch, MaterialLot, ActualCost, MealCost, MaterialConsumptionVariance}Service.cs`, `WebApi/Controllers/{ProductionOrder, ProductionBatch, MaterialLot, ActualCost, MealCost, MaterialConsumptionVariance}Controller.cs`, `WebApi/appsettings.json`, `Model/{Option/BusinessRulesOptions, Helper/{ConstantHelper, InventoryStatus, ProductionStatus}, Model/{InventoryProductionModels (mới), ProductionModels}, Request/Custom/ProductionCustomRequest}.cs`, `DB/seed/ProductionStage_seed.sql`, `deploy/db/scripts.order`, `DB/README.md`; test `Tests/Production/{InventoryProductionTests, InventoryProductionSqlServerTests}.cs` (mới); `docs/api-tests/be2.4-inventory-production.http` (mới), `pilot-flow.http` (+ P9), ghi chú chạy lại ở `be2.1 / be2.2 / be2.3`; `docs/frontend-handoff/BE2.4.md` (mới), `README.md`; `docs/claude/{DECISIONS, ROADMAP, BUSINESS_RULES, PROGRESS}.md`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **210/210** (baseline 203 + 7; không mất test, không bỏ qua):
  - `InventoryProductionTests` (6, InMemory, số tính tay): giữ chỗ 33 kg thịt / 4 kg rau lúc phát hành, phân bổ FEFO L1 10 / L2 20 / L3 3; xuất tay 18 kg (> 17 chưa giữ) → 400; xuất mẻ 84 phần 11,088 kg dùng phần đã giữ → PartiallyIssued 21,912, phân bổ lại L2 18,912 / L3 3, Available = OnHand − Reserved; CRUD giữ chỗ → 400 · thiếu tồn → cảnh báo "20 / 33 – thiếu 13", hàng về → `Reserve` đủ 33; hủy: thiếu lý do / phiếu Đã xuất → 400, hủy phiếu rồi hủy lệnh → mẻ Hủy, tồn giữ chỗ 0, kế hoạch còn Đã phát hành; lệnh Đang SX → 400 · hủy hết lệnh của ca (kèm phiếu Nháp → Hủy) → kế hoạch Đã duyệt, phát hành lại → giữ chỗ 33 · công đoạn: nhảy công đoạn / mã sai → 400, Sơ chế (người khác tổ → cảnh báo), Nấu → mẻ Đang nấu + lệnh Đang SX, Finish trước khi xong Nấu → 400, Chia suất trước Đạt → 400, Pack trước xong Chia suất → 400, bảng tiến độ theo tổ (CHIA_SUAT đang 1 – OPS-KIT-PORT; SO_CHE xong 1 / chờ 3); giá thành NVL 4 500 (4 kg mẻ + 0,5 kg không gắn mẻ), hao hụt 112,5, 78 phần đạt; đóng lệnh: chênh lệch rau +0,5 kg / 12,5% / 500 đ vượt 5%, đóng lại → 400, ActualCost CRUD → 400; giá vốn suất: đơn Đã chốt → 400; đối soát 300 / 330 → tổng 4 954 500 (canh thực tế 4 500 + thịt chuẩn 15 000 × 330), giá / suất 16 515, doanh thu 9 900 000, tính lại không nhân đôi · lô hết hạn: xem trước không ghi, xử lý → WASTE 5 kg "Hết hạn sử dụng 18/12/2026" + Hết hạn, lô tạm giữ bỏ qua, lô sắp hết hạn liệt kê, chạy lại 0 · MRP: PO 20 kg − nhận 5 = 15 sắp về (PO không ngày giao + PO giao sau ngày SX không tính) → cần 30, mua 15.
  - `InventoryProductionSqlServerTests` (1, SQL Server dev, đã chạy – không skip): hủy tồn lô hết hạn 30 kg → phát hành giữ chỗ 5 kg (lô còn hạn Reserved 5 / Available 3) → xuất mẻ → giữ chỗ Đã xuất → 3 công đoạn + ATTP → Pack bị chặn tới khi xong Chia suất → đóng lệnh (chênh lệch 0, NVL 5 000, 100 đ / phần) → giá vốn suất (lãi gộp 1 495 000); rollback, không để lại dữ liệu.
- **Chạy thật trên DB dev** (WebApi Development cổng 5324, trình chạy REST Client nhỏ – kiểm `responseCode` theo "→ 400"): `be2.4-inventory-production.http` **125/125** (9 chốt, key B24B, ngày 08/12/2026 – lần đầu B24A trùng ngày kế hoạch cũ 02/12 nên bỏ); `pilot-flow.http` **136/136** (+ P9 đóng lệnh / giá thành / giá vốn suất; key B24Q, ngày 11/12/2026); `be2.1-flow-gaps.http` **147/147** (key B24G, ngày 15/12/2026); `be2.2-contract-forecast.http` **73/73** (key B24C, ngày 03/12/2026); `be2.3-multilevel-approval.http` **58/58** (key B24E). Kết quả DB dev kiểm tay: lệnh B24B Đóng, ActualCost NVL 9 100 000 (gà 97,5 × 80 000 + rau 65 × 20 000), chênh lệch 0, giữ chỗ lệnh hủy Released / lệnh mới Issued, lô `DEMOB24B-HH` Hết hạn + WASTE 2 kg. Dữ liệu `[DEMO]` để lại trên DB dev.
- `git status`: không thay đổi trong `app-delicare-client/`, `docs/reference/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không `api/v1` mới.

AC (BE2.4): [x] giữ chỗ khi phát hành / xuất (126) · [x] hàng sắp về từ PO cho MRP · [x] chênh lệch tiêu hao (135) · [x] xử lý lô hết hạn bằng action (không job, D-45) · [x] đóng / hủy lệnh SX (144, 145) · [x] giá thành thực tế + giá vốn suất (152–154) · [x] công đoạn theo tổ Sơ chế → Nấu → Chia suất (người / tổ), "không chuyển công đoạn khi công đoạn trước chưa xong" (AC T17 còn thiếu) · [x] giữ kiểm soát ATTP (≥ 70 °C, chặn lô hết hạn / KHONG_DAT, lưu mẫu) · [x] test + `.http` · [x] handoff · [ ] Catalog Excel (máy không có openpyxl – bảng dưới).

Bảng cập nhật `DELICARE_API_Catalog.xlsx` (sheet `API_Catalog`) – STT catalog → Trạng thái / API thực tế / Task:
| STT | Trạng thái (J) | API thực tế – khung cũ (L) | Task (M) | Ghi chú (K) |
|---|---|---|---|---|
| 121 | Đã xong | `POST MaterialLot/ProcessExpired` (thao tác tay) | BE2.4 | Không job (D-45); lô tạm giữ bỏ qua; `Preview` (D-53 d) |
| 126 | Đã xong | `GET MaterialReservation/GetAll`; `ProductionOrder/GetDetail` + `ProductionPlan/GetDetail` (`listReservation`); `POST ProductionOrder/Reserve` | BE2.4 | Giữ khi `ProductionPlan/Release`, phân bổ FEFO suy ra (GAP-INV-13, D-53 a) |
| 128 | Đã xong | `POST MaterialIssue/Create` / `Post`, `ProductionBatch/CreateMaterialIssue` | T16, T17, BE2.4 | Giảm giữ chỗ → PartiallyIssued / Issued |
| 135 | Đã xong | `GET MaterialConsumptionVariance/GetList` | BE2.4 | Sinh khi `ProductionOrder/Close` (D-53 h) |
| 140 | Đã xong | `POST ProductionPlan/Release` | T17, T17.1, BE2.4 | + giữ chỗ, `listReservation` |
| 142 | Đã xong | `GET ProductionOrder/GetDetail` | T17, BE2.4 | + reservations, variances, actualCost, allowedActions |
| 144 | Đã xong | `POST ProductionOrder/Close` | BE2.4 | Hoàn thành → Đóng + chênh lệch + giá thành + giải phóng giữ chỗ |
| 145 | Đã xong | `POST ProductionOrder/Cancel` | BE2.4 | Chỉ Phát hành chưa nấu / chưa xuất kho; mẻ Hủy (D-53 c) |
| 152 | Đã xong | `GET ActualCost/GetList` | BE2.4 | + breakdown, costPerPortion, standardCostPerPortion |
| 153 | Đã xong | `POST ProductionOrder/CalculateCost` | BE2.4 | ActualCost theo mẻ (GAP-CST-01, D-53 f) |
| 154 | Đã xong | `POST MealCost/Calculate`, `GET MealCost/GetList` | BE2.4 | Đơn Đã giao / Đã đối soát (GAP-CST-02, D-53 g) |
| – (7.1 incoming) | Đã xong | `POST MaterialRequirement/Calculate` / `CalculateTopUp` (`expectedIncomingQuantity`) | BE2.4 | PO ≤ ngày SX − đã nhận (D-53 b) |
| – (T17 công đoạn theo tổ) | Đã xong | `POST ProductionBatch/StartStage`, `FinishStage`; `GET ProductionBatch/GetStageBoard` | BE2.4 | Checklist PRD_STAGE (GAP-PRD-09, D-53 e) |

PROPOSED chờ anh (DECISIONS **D-53** (a)–(h), mẫu D-49 – em đã cài theo "Đề xuất", không mục nào BLOCKED): (a) giữ chỗ theo lệnh × nguyên liệu, tồn giữ chỗ suy ra FEFO mọi kho; (b) hàng sắp về chỉ PO có ngày giao ≤ ngày SX; (c) hủy lệnh chỉ khi chưa nấu / chưa xuất kho, mẻ Hủy, mở lại kế hoạch; (d) lô hết hạn: WASTE toàn bộ, bỏ qua lô tạm giữ; (e) công đoạn = checklist PRD_STAGE, tùy chọn bắt buộc; (f) giá thành: hao hụt chỉ phân tích, không cộng tổng; (g) giá vốn suất phân bổ theo phần món ngày / ca, doanh thu = LineAmount; (h) chênh lệch tiêu hao theo phần nấu được, ngưỡng 5% chỉ cảnh báo.

Cần anh: chạy `DB/seed/ProductionStage_seed.sql` trên staging / Docker (qua `Deploy-Database.ps1` / `db-init` – đã có trong `scripts.order`; DB dev đã chạy). FE: đọc `docs/frontend-handoff/BE2.4.md` mục "Thay đổi hành vi" (tồn khả dụng trừ giữ chỗ, MRP trừ hàng sắp về, mẻ Hủy, Finish / Pack kiểm công đoạn khi mẻ đã ghi công đoạn).

### BE2.5 – ATTP, vệ sinh, hộp / khay (T18 phần nghiệp vụ, T20) · 2026-11-17 · DONE
Nhánh `feat/BE2.5-foodsafety-packaging` (worktree `wt-BE25`, từ `release/pilot` @ `7e87f7c` = T48.3 + BE2.1 … BE2.4). Chỉ backend (D-49); không đụng `app-delicare-client/`, `docs/reference/*`.

Kế hoạch:
1. Rà soát: `FoodSafetyChecklist*`, `CleaningBatch`, `CleaningChecklistResult`, `ContainerReturn`, `ReturnInspection`, `PackagingLedger`, `SupplierIssue` chỉ có CRUD sinh mã (+ SP `GetAll`), chưa nghiệp vụ nào; `FoodSafetyChecklistResult` đã dùng cho lưu mẫu (T17) và công đoạn (BE2.4); bàn giao (`DeliveryStopService.Deliver`) chưa ghi sổ bao bì; mục menu / quyền của mọi màn hình (`qc-checklist-result`, `qc-cleaning`, `qc-cleaning-result`, `qc-supplier-issue`, `log-container-return`, `log-return-inspection`, `log-packaging-ledger`) đã có → không seed menu / quyền.
2. Checklist theo ca / mẻ: lần chạy + ghi kết quả + lỗi trọng yếu giữ mẻ (161, 162); seed bộ checklist.
3. Sổ bao bì OUT khi bàn giao (190), thu hồi + kiểm tra (194, 195), số dư + thất thoát (196).
4. Mẻ vệ sinh 6 bước (167–169); sự cố NCC chuyển trạng thái (170).
5. Test InMemory + SQL Server rollback; `.http` mới + chạy lại 5 file luồng; handoff; DECISIONS D-54 / GAP.

Đã làm (mẫu 4.2 – thêm method / action vào đúng service / controller cũ; 3 lớp tĩnh dùng chung mới `FoodSafetyRules`, `PackagingRules`, `CleaningRules`; không service / controller mới):
- **Checklist ATTP (161, 162)** – `FoodSafetyChecklistResult/GetRun` (bộ + ngày + ca + mẻ: tiêu chí theo SortOrder ghép kết quả mới nhất, `dueAt` = giờ xuất bếp của ca + `ChecklistOffsetsMinutes`, `isOverdue`, tỷ lệ đạt 7 ngày) và `Record` (JSON): mỗi lần ghi 1 dòng mới, tiêu chí bắt buộc phải có kết quả, FAIL bắt buộc khắc phục, bộ PRD_STAGE / PRD_BATCH / VS-KHAY chặn (ghi qua thao tác riêng). **Tiêu chí trọng yếu FAIL gắn mẻ** → mẻ Đạt → Chờ QC + HOLD (đang nấu / chờ QC → HOLD, Đã đóng gói giữ trạng thái); `ProductionRules.SafetyBlockersAsync` (Release / Pack / PackingOrder) + `DeliveryRules.ProductionBlockers` (Dispatch) chặn tới khi kiểm lại ĐẠT; Telegram → R2 (trả `listCriticalFailure`). CRUD kết quả checklist → 400.
- **Sổ bao bì OUT khi bàn giao (190)** – `DeliveryStop/Deliver` + `PackagingQuantity?`, `PackagingType?`: mặc định OUT = suất nhận × 1, loại = kết quả đóng gói của đơn / `KHAY`; `0` = hộp dùng 1 lần; kèm phiếu `ContainerReturn` Chờ thu hồi.
- **Thu hồi + kiểm tra (194, 195)** – `ContainerReturn/Record` (đơn giao Đã giao, loại theo OUT của đơn giao, không vượt số còn ở khách, IN tốt / hỏng, phiếu Chờ → Đã thu hồi), `Inspect` (OK / SHORTAGE / DAMAGED / DIRTY / REJECTED, khác OK bắt buộc ghi chú, `ReturnInspection`, phiếu → Đã kiểm tra, tự lập mẻ vệ sinh cho khay dùng được), `GetList` (+ statusCounts), `GetDetail`. CRUD Insert / Update → 400; Delete chỉ phiếu Chờ thu hồi.
- **Số dư + thất thoát (196)** – `PackagingLedger/GetBalance` (OUT / IN / hỏng / LOSS / còn ở khách theo khách × loại, tỷ lệ thất thoát trong kỳ, ngưỡng `Customer.ExternalData` hoặc 3%, `isOverThreshold`), `PackagingLedger/Record` (ghi tay OUT / IN / LOSS có lý do, không vượt số còn ở khách, cảnh báo vượt ngưỡng). CRUD Insert → 400.
- **Mẻ vệ sinh 6 bước (167–169)** – `CleaningBatch/Create` (từ phiếu kiểm tra, số `VS-YYMMDD-<CA>`, ≤ số dùng được còn lại), `RecordResults` (JSON, đi lần lượt, khử khuẩn bắt buộc hóa chất), `Complete` (PASS đủ 6 bước → sổ AVAILABLE; REJECT có lý do → vệ sinh lại), `GetDetail`. CRUD Insert / Update → 400, `CleaningChecklistResult` / `ReturnInspection` CRUD → 400.
- **Sự cố NCC (170)** – `SupplierIssue/Transition` (Mới → Đang xử lý / Đã xử lý → Đóng, mở lại; Đã xử lý bắt buộc cách xử lý; người phụ trách = User), `GetList` (+ isOverdue, hoursToDue, allowedTransitions, statusCounts); CRUD Insert luôn Mới + hạn mặc định, Update không đổi trạng thái.
- Cấu hình `BusinessRules` (PROPOSED): `FoodSafetyCriticalFailHoldsBatch`, `CleaningChecklistCode`, `CleaningChemicalRequiredItems`, `CleaningAutoCreateOnInspect`, `PackagingOutOnDelivery`, `PackagingDefaultType`, `PackagingUnitsPerMeal`; `ReferenceLists`: `PackagingType`, `PackagingTransactionType`. Hằng `FoodSafetyCode`, `CleaningBatchStatus`, `ContainerReturnStatus`, `ReturnInspectionResult`, `PackagingTransactionType`, `PackagingCondition`, `SupplierIssueStatus`; thông báo tiếng Việt trong `ConstantHelper.Message`.
- **Seed mới `DB/seed/FoodSafetyQa_seed.sql`** (QA_FG, PREP, PREP_AREA_CLEAN, BLAST_CHILL, KITCHEN_CLEAN, VS-KHAY) – đã thêm vào `deploy/db/scripts.order` (sau `ProductionStage_seed.sql`) + `DB/README.md`; **đã chạy trên DB dev 2 lần** (`sqlcmd -b -I -f 65001`, lần 2 không thêm dòng). Không SP mới, không bảng / cột.

File chính: `Services/Implement/{FoodSafetyRules, PackagingRules, CleaningRules}.cs` (mới), `{FoodSafetyChecklistResult, CleaningBatch, CleaningChecklistResult, ContainerReturn, ReturnInspection, PackagingLedger, SupplierIssue, DeliveryStop}Service.cs`, `ProductionRules.cs`, `DeliveryRules.cs`, `Interface/I{FoodSafetyChecklistResult, CleaningBatch, ContainerReturn, PackagingLedger, SupplierIssue}Service.cs`, `WebApi/Controllers/{FoodSafetyChecklistResult, CleaningBatch, ContainerReturn, PackagingLedger, SupplierIssue}Controller.cs`, `WebApi/appsettings.json`, `Model/{Option/BusinessRulesOptions, Helper/{ConstantHelper, FoodSafetyStatus (mới)}, Model/{FoodSafetyPackagingModels (mới), DeliveryModels}, Request/Custom/{FoodSafetyPackagingCustomRequest (mới), DeliveryCustomRequest}}.cs`, `DB/seed/FoodSafetyQa_seed.sql`, `deploy/db/scripts.order`, `DB/README.md`; test `Tests/Production/{FoodSafetyPackagingTests, FoodSafetyPackagingSqlServerTests}.cs` (mới); `docs/api-tests/be2.5-foodsafety-packaging.http` (mới), `pilot-flow.http` (+ P10), ghi chú chạy lại ở `be2.1 / be2.2 / be2.3 / be2.4`; `docs/frontend-handoff/BE2.5.md` (mới), `README.md`; `docs/claude/{DECISIONS, ROADMAP, BUSINESS_RULES, PROGRESS}.md`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **216/216** (baseline 210 + 6; không mất test, không bỏ qua). Một lần chạy khác gặp 3–4 test SQL Server cũ bị **deadlock** do tiến trình khác (worktree `wt-BE26` chạy test cùng DB dev cùng lúc) – chạy lại xanh.
  - `FoodSafetyPackagingTests` (5, InMemory, số tính tay): checklist thành phẩm – thiếu tiêu chí / FAIL không khắc phục / mã sai / bộ PRD_STAGE → 400, Cảm quan FAIL → mẻ Đạt → Chờ QC + HOLD (+ output HOLD), Release → 400, kiểm lại cùng giây → dòng mới (historyCount 2), tỷ lệ đạt 3/3 = 100%, Release OK, CRUD → 400 · PREP: hạn 07:00 (ca 10:30 − 60′ − 150′), quá hạn lúc 10:00; FAIL trọng yếu không gắn mẻ → cảnh báo; mẻ Đã đóng gói FAIL → chặn xuất chuyến, kiểm lại → hết chặn · giao 60 / nhận 58 → OUT 58 loại của kết quả đóng gói + phiếu Chờ thu hồi; thu 60 → 400, hỏng > trả → 400, thu 55 (hỏng 3) → IN 52 + 3, còn 3; kiểm tra hỏng 4 → mẻ VS 51 khay; bước 3 trước 1 → 400, khử khuẩn thiếu hóa chất → 400, kết luận thiếu bước → 400, REJECT → mẻ mới `…-2` → 6 bước → PASS → AVAILABLE 51; thất thoát 4 > 3 → 400, thất thoát 2 → 3,45% > ngưỡng khách 2% (cảnh báo); CRUD sổ / phiếu / kiểm tra / mẻ → 400 · hộp dùng 1 lần (0) → không sổ, thu → 400; tồn đầu kỳ OUT tay → thu theo loại · sự cố NCC: mức độ sai → 400, Insert luôn Open + hạn +2 ngày, Open → Closed → 400, InProgress (owner = người thao tác), Resolved thiếu cách xử lý → 400, Update không đổi trạng thái, Closed (ClosedAt), Update sự cố Đóng → 400, danh sách quá hạn lên đầu.
  - `FoodSafetyPackagingSqlServerTests` (1, SQL Server dev, đã chạy – không skip): FAIL trọng yếu trên mẻ Đã đóng gói → Dispatch 400 → kiểm lại → xuất phát → bàn giao OUT 50 → thu 48 (hỏng 2) → kiểm tra DIRTY → mẻ VS 46 → 6 bước → PASS → LOSS 2 (4% > 3%) → sự cố NCC 4 trạng thái; rollback, không để lại dữ liệu. Bắt được lỗi tag loại bao bì có dấu "]" (`[DEMO] KHAY`) – đã sửa (GAP-PKG-02).
- **Chạy thật trên DB dev** (WebApi Development cổng 5335, trình chạy REST Client nhỏ – kiểm `responseCode` theo "→ 400"): `be2.5-foodsafety-packaging.http` **142/142** (16 chốt, key B25A, ngày 22/12/2026); `pilot-flow.http` **141/141** (+ P10 thu hồi hộp → kiểm tra → vệ sinh → số dư; key B25P, 29/12/2026); `be2.1-flow-gaps.http` **147/147** (key B25G, 05/01/2027); `be2.2-contract-forecast.http` **73/73** (key B25C, 04/01/2027); `be2.3-multilevel-approval.http` **58/58** (key B25E); `be2.4-inventory-production.http` **125/125** (key B25B, 12/01/2027). Kết quả DB dev kiểm tay (B25A): sổ bao bì OUT 200 KHAY → IN 192 GOOD + 3 DAMAGED → LOSS 3 → AVAILABLE 191 (`VS-261117-TRUA-2`), mẻ `VS-261117-TRUA` Hoàn tất / REJECT, `…-2` Hoàn tất / PASS. Dữ liệu `[DEMO]` để lại trên DB dev.
- `git status`: không thay đổi trong `app-delicare-client/`, `docs/reference/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không `api/v1` mới.

AC (BE2.5): [x] chạy checklist + ghi kết quả, lỗi trọng yếu (161, 162) · [x] mẻ vệ sinh khay 6 bước, phải đủ 6 bước mới hoàn tất (167–169, AC T20) · [x] chuyển trạng thái sự cố NCC (170) · [x] thu hồi + kiểm tra + sổ bao bì OUT / IN khi giao / thu (190, 194–196) · [x] tỷ lệ thất thoát theo khách + cờ vượt ngưỡng (AC T20) · [x] giữ kiểm soát ATTP (≥ 70 °C, lưu mẫu, công đoạn, lô hết hạn – không đổi; thêm chặn lỗi trọng yếu) · [x] test + `.http` · [x] handoff · [ ] Catalog Excel (máy không có openpyxl – bảng dưới) · [–] báo cáo NCR / dashboard: ngoài phạm vi (để sau).

### BE2.6 – Nhân sự (T21) · 2026-11-17 · DONE
Nhánh `feat/BE2.6-hr` (worktree `wt-BE26`, từ `release/pilot` @ `7e87f7c` = T48.3 + BE2.1–BE2.4). Chỉ backend (D-49); không đụng `app-delicare-client/`, `docs/reference/*`. Làm song song BE2.5 (ATTP / khay / bao bì) – không đụng phần đó; DECISIONS ghi khối riêng "BE2.6" cuối file, mục này ở cuối PROGRESS.

Kế hoạch:
1. Rà soát: `ShiftAssignment`, `AttendanceEvent`, `LeaveRequest`, `ShiftSwapRequest`, `Employee`, `KitchenWorkstation` chỉ có CRUD sinh mã + SP `GetAll`; mục menu `#/hr-shift-assign`, `#/hr-attendance`, `#/hr-leave`, `#/hr-swap`, `#/wf-inbox?type=hr-leave` / `hr-swap` + quyền (CEO / COO / FIN-HR) **đã có** trong seed T07 → không seed mới. Không có cột User ↔ Employee (GAP-ADM-03); UNIQUE phân ca tính cả dòng xóa mềm.
2. Lưới ca + phân ca upsert + hàng loạt + xác nhận (201–204) trong `ShiftAssignmentService` / Controller.
3. Vào / ra ca GPS – QR – máy – chấm hộ + bảng công dạng danh sách (205, 206) trong `AttendanceEventService` / Controller.
4. Đơn nghỉ, đổi / nhường ca + 2 handler engine BE2.3 + hủy (207–209) trong `LeaveRequestService`, `ShiftSwapRequestService`.
5. Test InMemory + SQL Server rollback; `.http` mới + chạy lại `pilot-flow.http`; handoff; DECISIONS D-55 / GAP-HR-02…05.

Đã làm (mẫu 4.2 – thêm method / action vào đúng service / controller cũ; 1 lớp tĩnh dùng chung mới `HrRules` như `ProductionRules`; không service / controller mới):
- **Lưới ca (201)** – `ShiftAssignment/GetGrid` (`FromDate`, `Days` 1–31 mặc định 7, `OrganizationUnitId` gồm đơn vị con, `SearchText`): nhân viên đang làm × ngày, ô = ca / trạng thái / `onLeave` (đơn nghỉ Đã duyệt) / `leavePending` / `hasAttendance` / `pendingSwap`; `shifts`, `kpiToday` (được phân / đã vào ca theo ca).
- **Phân ca (202)** – `PUT ShiftAssignment/Assign` upsert theo (nhân viên, ngày), `ShiftId` trống = bỏ ca (xóa mềm tay – không dùng `CommitAsync(Delete)` vì lệnh đó xóa mềm mọi bản ghi đang sửa), giờ theo ca (ca qua đêm → hôm sau) hoặc `PlannedStart / End`; chặn ngày đã qua, nhân viên nghỉ việc, ngày nghỉ phép Đã duyệt, ca đã chấm công / đã vào ca ("dùng đơn đổi ca"), ca trong đơn đổi ca chờ duyệt; phân lại đúng (nhân viên, ca, ngày) đã xóa → **dùng lại dòng** (GAP-HR-03). Đổi giờ ca Đã xác nhận → về Kế hoạch + cảnh báo.
- **Hàng loạt (203)** – `ShiftAssignment/BulkAssign` (form lặp `EmployeeIds`, `SkipWeekdays`; trống = nhân viên của đơn vị; ≤ 31 ngày): bỏ qua ô đã có ca / nghỉ phép / nhân viên nghỉ việc → `{ created, skipped, listSkipped }`.
- **Xác nhận (204)** – `ShiftAssignment/Confirm` Kế hoạch → Xác nhận (ngày / khoảng, đơn vị, ca), **không gửi Telegram** (`notificationSent = false`). `ShiftAssignment/MarkAbsent` – đánh Vắng ca hết giờ không chấm công (thao tác tay thay job, D-45).
- **Chấm công (205)** – `AttendanceEvent/CheckIn`, `CheckOut` (`[SkipPermission]`, service kiểm): nhân viên của tài khoản (GAP-HR-02: `UserExternalIdentity` → mã nhân viên = tên đăng nhập → SĐT hồ sơ) hoặc nhân viên khác khi có quyền Thêm `#/hr-attendance` + ghi rõ hình thức (T21 AC), PROXY bắt buộc lý do, Note "Ghi bởi …"; hình thức `GPS / QR / MACHINE / PROXY` (nhận MOBILE / ZALO / KIOSK); tìm ca hôm nay / ca đêm hôm qua trong [bắt đầu − 120′, hết ca]; `DistanceMeters` tới trạm bếp Hoạt động gần nhất (haversine), `IsValid` = ≤ `RadiusMeters` (trống 150 m) – ngoài bán kính vẫn ghi `IsValid = false` + cảnh báo (`RejectOutsideRadius` để chặn); QR cần mã trạm; đi muộn / về sớm > 5′ ghi Note; chống trùng `ExternalEventId`; ca → Đã vào ca / Hoàn thành. Tọa độ: chỉ lưu ở `AttendanceEvent` (sổ chỉ ghi thêm), **AuditChange che giá trị Latitude / Longitude** (`DatabaseContext.GetChanges`), không trả lại trong kết quả, không ghi log. `AttendanceEvent/GetMine` – ca hôm nay của tôi + `allowedActions`. CRUD `AttendanceEvent/Insert` → 400.
- **Bảng công (206 – danh sách)** – `AttendanceEvent/GetTimesheet` (1 dòng / nhân viên: ca được phân, có mặt, muộn + phút, về sớm + phút, vắng = Vắng hoặc hết giờ chưa vào ca, thiếu ra ca, lần ngoài bán kính, ngày nghỉ, giờ công; phân trang; ≤ 62 ngày), `GetTimesheetDetail` (1 dòng / ca).
- **Đơn nghỉ (207, 209)** – `LeaveRequest/Create` (`[SkipPermission]`: chính mình, người khác cần Thêm `#/hr-leave`): loại theo `ReferenceLists:LeaveType`, ≤ 30 ngày, lùi ≤ 7 ngày, lý do bắt buộc, không chồng đơn, không trùng ngày đã chấm công; số `DN-YYMMDD-NNNN`; ApprovalRequest **LEAVE_REQUEST** (handler mới `LeaveRequestApprovalHandler`, mặc định 1 cấp `hr-leave`) + `ApprovalRequestId`. Duyệt → Đã duyệt + xóa mềm ca Kế hoạch / Xác nhận chưa chấm công trong khoảng ("Nghỉ theo đơn DN-…"), chi tiết cảnh báo "ca thiếu người"; từ chối → Từ chối; `Cancel` (chỉ Chờ duyệt, người tạo, hủy luôn ApprovalRequest qua engine). `Approve` / `Reject` (engine kiểm quyền), `GetDetail` (người tạo / nhân viên / quyền Xem / quyền Duyệt), `GetMine`. CRUD Insert / Update → 400, Delete chỉ đơn Từ chối / Hủy.
- **Đổi / nhường ca (208, 209)** – `ShiftSwapRequest/Create`: hoán đổi (`ProposedShiftAssignmentId`) hoặc nhường (`TargetEmployeeId`); kiểm lúc lập và lại lúc duyệt (chưa chấm công, không trong đơn khác, không 2 ca / ngày, không nghỉ phép); số `PH-` (DocumentNumberService: thêm `ShiftSwapRequest.RequestNo` vào nhóm Phiếu); ApprovalRequest **SHIFT_SWAP** (handler mới `ShiftSwapApprovalHandler`, 1 cấp `hr-swap`) → duyệt hoán đổi EmployeeId trong transaction (người nhận có dòng xóa mềm cùng ca / ngày → dùng lại dòng). `Approve` / `Reject` / `Cancel` / `GetDetail` / `GetMine` như đơn nghỉ.
- Hộp duyệt: tiêu đề "Đơn nghỉ DN-… – bỏ k ca đã phân", "Đổi ca PH-…: A (ca ngày) ↔ B (ca ngày)", "Nhường ca …"; `ApprovalRequest/GetDetail` → `business` LeaveRequest / ShiftSwapRequest + các ca (`ApprovalRequestService.BusinessAsync`).
- Cấu hình `BusinessRules:Hr` (appsettings, PROPOSED, lớp `HrRulesOptions` gắn vào `BusinessRulesOptions.Hr`): `CheckInEarlyMinutes` 120, `CheckInLateMinutes` 0, `LateGraceMinutes` 5, `EarlyLeaveGraceMinutes` 5, `RejectOutsideRadius` false, `DefaultRadiusMeters` 150, `BulkAssignMaxDays` 31, `TimesheetMaxDays` 62, `LeaveMaxBackdateDays` 7, `LeaveMaxDays` 30, `GridDays` 7. `ReferenceLists:LeaveType`, `AttendanceSource`. Hằng `ShiftAssignmentStatus`, `AttendanceEventType`, `AttendanceSource`, `LeaveRequestStatus`, `ShiftSwapStatus`, `HrScreen`, `EmployeeStatus` (`Model/Helper/HrStatus.cs`); thông báo tiếng Việt `ConstantHelper.Message.Hr*` (khối đầu lớp Message).
- **Không SP mới, không seed mới, không bảng / cột** → `deploy/db/scripts.order` và `DB/README.md` không đổi.

File chính: `Services/Implement/{HrRules (mới), ShiftAssignmentService, AttendanceEventService, LeaveRequestService, ShiftSwapRequestService, ApprovalRequestService, DocumentNumberService}.cs`, `Implement/Approval/{LeaveRequest, ShiftSwap}ApprovalHandler.cs` (mới), `Interface/I{ShiftAssignment, AttendanceEvent, LeaveRequest, ShiftSwapRequest}Service.cs`, `WebApi/Controllers/{ShiftAssignment, AttendanceEvent, LeaveRequest, ShiftSwapRequest}Controller.cs`, `WebApi/appsettings.json`, `Data/Connection/DatabaseContext.cs` (che tọa độ trong AuditChange), `Model/{Helper/HrStatus (mới), Helper/ConstantHelper, Option/HrRulesOptions (mới), Option/BusinessRulesOptions, Model/HrModels (mới), Request/Custom/HrCustomRequest (mới)}.cs`; test `Tests/Hr/{HrTests, HrSqlServerTests}.cs` (mới); `docs/api-tests/be2.6-hr.http` (mới); `docs/frontend-handoff/BE2.6.md` (mới), `README.md`; `docs/claude/{DECISIONS, ROADMAP, BUSINESS_RULES, PROGRESS}.md`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **216/216** (baseline 210 + 6; không mất test, không bỏ qua):
  - `HrTests` (5, InMemory, đồng hồ test Thứ 5 10/12/2026): phân ca TRUA → đổi Đêm (kết thúc 01:00 hôm sau) → đổi lại TRUA dùng lại dòng (1 dòng / bộ ba), giờ riêng + giờ sai, ngày đã qua / nghỉ việc / CRUD → 400; hàng loạt 3 nhân viên × tuần bỏ CN = 16 tạo, 3 bỏ qua, theo đơn vị gồm tổ con; lưới (7 ngày, ô trống null, lọc tổ con); xác nhận 3 → chạy lại 0, đổi giờ ca đã xác nhận → Kế hoạch + cảnh báo, bỏ ca · chấm công: quá sớm (gợi ý 08:30), tài khoản không gắn nhân viên, thiếu GPS, tự chấm PROXY, mã kênh sai → 400; GPS ~35 m hợp lệ (MOBILE → GPS), không trả tọa độ, trùng ExternalEventId, vào ca 2 lần → 400; AuditChange không chứa tọa độ; nhân viên theo SĐT hồ sơ ~556 m → ghi, IsValid false + cảnh báo, muộn 12′; chấm hộ: không quyền / thiếu hình thức / thiếu lý do / QR sai trạm → 400, PROXY ghi "Ghi bởi"; ra ca về sớm 30′; GetMine 160′; bảng công (5 dòng kể cả nghỉ việc: có mặt, về sớm, muộn, ngoài bán kính, thiếu ra ca, vắng), chi tiết, khoảng ngày sai → 400; MarkAbsent 1; ca đã chấm công không bỏ được · `RejectOutsideRadius` = true chặn (bán kính mặc định 150 m), QR cần mã trạm (không phân biệt hoa thường) · đơn nghỉ: loại sai / ngày ngược / thiếu lý do / lập hộ không quyền / lùi > 7 ngày → 400; DN-, LEAVE_REQUEST + ApprovalRequestId, cảnh báo sẽ bỏ 2 ca, trùng khoảng → 400; người không quyền không duyệt / không xem được; hộp duyệt `hr-leave` (tiêu đề "bỏ 2 ca"), chi tiết business; duyệt → 2 ca xóa mềm "Nghỉ theo đơn", lưới onLeave, phân ca ngày nghỉ → 400, hủy đơn đã duyệt → 400; từ chối (thiếu lý do → 400) giữ ca; người khác hủy → 400, người tạo hủy → Đã hủy + yêu cầu Đã hủy; GetMine 3; ngày đã chấm công → 400 · đổi ca: người nhận đã có ca ngày đó → 400, lập cho ca người khác không quyền / thiếu lý do / người nhận không khớp → 400, PH-, ca trong đơn khác → 400, phân ca đang có đơn → 400, lưới pendingSwap, hộp duyệt `hr-swap` (tiêu đề ↔), người nhận xem chi tiết, duyệt → hoán đổi 2 ca; nhường cho người bận → 400; nhường cho người có dòng xóa mềm cùng ca / ngày → dùng lại dòng; ca đã chấm công → 400; người khác hủy → 400, người tạo hủy → Đã hủy.
  - `HrSqlServerTests` (1, SQL Server dev, đã chạy – không skip): đổi ca rồi đổi lại trên UNIQUE thật (không lỗi, 2 dòng), hàng loạt 5 / bỏ 1, chấm GPS ghi hộ ~44 m hợp lệ (muộn 10′, Note, AuditChange không có tọa độ) → ra ca ~730 m IsValid false → bảng công; đơn nghỉ DN- duyệt qua engine → ca xóa mềm; nhường ca cho người có dòng xóa mềm → dùng lại dòng; rollback, không để lại dữ liệu.
  - Lần chạy toàn bộ đầu tiên có 4 test SQL Server (vd. `FlowGapSqlServerTests`) lỗi một lần rồi chạy lại 2 lần đều xanh – nghi do agent BE2.5 dùng chung DB dev cùng lúc; không phải do BE2.6 (test HR chỉ chạm bảng nhân sự).
- **Chạy thật trên DB dev** (WebApi Development cổng 5326, trình chạy REST Client nhỏ – kiểm `responseCode` theo "→ 400"): `be2.6-hr.http` **65/65** (20 chốt, key B26B; lần B26A dở dang vì máy chạy múi giờ PST – file dùng `{{$datetime … +7 giờ}}` = giờ VN); `pilot-flow.http` **136/136** (key B26P, ngày 17/02/2027 – đã sửa code dùng chung ApprovalRequestService / DocumentNumberService / DatabaseContext); `be2.3-multilevel-approval.http` **58/58** (key B26E). Kết quả DB dev kiểm tay: NV3 B26B vào ca GPS 22,24 m hợp lệ / ra ca 1 111,95 m không hợp lệ, Note "Trạm …; Đi muộn 10 phút; Ghi bởi admin (GPS…)"; đơn nghỉ DN-261117-0004 Đã duyệt / 0005 Từ chối / 0006 Đã hủy; đổi ca PH-261117-0205, 0206 Đã duyệt (0206 hoán đổi dùng lại dòng NV2 TRUA 11/01 đã xóa), 0207 Đã hủy; AuditChange AttendanceEvent không có tọa độ. Dữ liệu `[DEMO]` để lại trên DB dev.
- `git status`: không thay đổi trong `app-delicare-client/`, `docs/reference/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không `api/v1` mới.

AC (T21 / BE2.6): [x] lưới ca, phân ca, phân ca hàng loạt, xác nhận (201–204, không gửi Telegram) · [x] chấm công vào / ra GPS theo KitchenWorkstation lat / long / bán kính (205) – **test hợp lệ theo bán kính** · [x] **chấm hộ ghi rõ SourceChannel** · [x] bảng công dạng danh sách (206) · [x] nghỉ phép, đổi ca, hủy + duyệt qua engine (207–209) · [x] test + `.http` · [x] handoff · [ ] Catalog Excel (máy không có openpyxl; theo yêu cầu không sửa `docs/reference/*` – bảng dưới).

Bảng cập nhật `DELICARE_API_Catalog.xlsx` (sheet `API_Catalog`) – STT catalog → Trạng thái / API thực tế / Task:
| STT | Trạng thái (J) | API thực tế – khung cũ (L) | Task (M) | Ghi chú (K) |
|---|---|---|---|---|
| 161 | Đã xong | `GET FoodSafetyChecklistResult/GetRun` | BE2.5 | + `dueAt` / `isOverdue` theo `ChecklistOffsetsMinutes`, `listPassRate7d` (D-54 c) |
| 162 | Đã xong | `POST FoodSafetyChecklistResult/Record` (JSON) | BE2.5 | Dòng mới mỗi lần (không upsert – GAP-QA-03); lỗi trọng yếu giữ mẻ, chặn đạt QC / đóng gói / xuất chuyến; Telegram → R2 (D-54 d) |
| 167 | Đã xong | `POST CleaningBatch/Create`; tự lập trong `ContainerReturn/Inspect` | BE2.5 | Số VS-YYMMDD-<CA> (D-54 g) |
| 168 | Đã xong | `POST CleaningBatch/RecordResults` (JSON), `GET CleaningBatch/GetDetail` | BE2.5 | Checklist VS-KHAY, đi lần lượt, khử khuẩn ghi hóa chất (D-54 f) |
| 169 | Đã xong | `POST CleaningBatch/Complete` | BE2.5 | PASS → sổ AVAILABLE; REJECT → vệ sinh lại (GAP-QA-04) |
| 170 | Đã xong | `POST SupplierIssue/Transition`, `GET SupplierIssue/GetList`; CRUD cũ | BE2.5 | OwnerId = User; quá hạn = cờ khi xem, không job (D-54 e) |
| 190 | Đã xong | `POST DeliveryStop/Deliver` (+ `PackagingQuantity`, `PackagingType`) | T19, BE2.5 | Sổ bao bì OUT + phiếu Chờ thu hồi (D-54 a) |
| 194 | Đã xong | `POST ContainerReturn/Record`, `GET ContainerReturn/GetList`, `GetDetail` | BE2.5 | IN tốt / hỏng; loại bao bì tag trong Note (GAP-PKG-02) |
| 195 | Đã xong | `POST ContainerReturn/Inspect` | BE2.5 | ReturnInspection + tự lập mẻ vệ sinh; DIRTY = GAP-C-05 |
| 196 | Đã xong | `GET PackagingLedger/GetBalance`; `POST PackagingLedger/Record` | BE2.5 | Còn ở khách = OUT − IN − LOSS, tỷ lệ thất thoát + ngưỡng (GAP-PKG-01 / 03) |

PROPOSED chờ anh (DECISIONS **D-54** (a)–(h), mẫu D-49 – em đã cài theo "Đề xuất", không mục nào BLOCKED): (a) OUT mặc định = suất nhận × 1 khay khi bàn giao; (b) loại phát sinh OUT / IN / LOSS / AVAILABLE, thiếu khi kiểm tra không tự thành LOSS; (c) checklist ghi dòng mới (giữ lịch sử) thay upsert; (d) lỗi trọng yếu giữ mẻ + chặn đạt QC / đóng gói / xuất chuyến; (e) máy trạng thái sự cố NCC, OwnerId = User; (f) vệ sinh đi lần lượt 6 bước, REJECT = vệ sinh lại; (g) kiểm tra thu hồi tự lập mẻ vệ sinh; (h) ngưỡng thất thoát theo `Customer.ExternalData`, loại bao bì tag trong Note.

Cần anh: chạy `DB/seed/FoodSafetyQa_seed.sql` trên staging / Docker (qua `Deploy-Database.ps1` / `db-init` – đã có trong `scripts.order`; DB dev đã chạy). FE: đọc `docs/frontend-handoff/BE2.5.md` mục "Thay đổi hành vi" (bàn giao ghi sổ bao bì, lỗi trọng yếu chặn mẻ / chuyến, CRUD bị chặn).

| 201 | Đã xong | `GET ShiftAssignment/GetGrid` | BE2.6 | `cells` theo ngày yyyy-MM-dd, `onLeave` / `leavePending` / `pendingSwap`, `kpiToday` |
| 202 | Đã xong | `PUT ShiftAssignment/Assign` | BE2.6 | Upsert (nhân viên, ngày), ShiftId trống = bỏ ca; dòng xóa mềm dùng lại (GAP-HR-03, D-55 d) |
| 203 | Đã xong | `POST ShiftAssignment/BulkAssign` | BE2.6 | Form lặp `EmployeeIds`, `SkipWeekdays` (1 = T2 … 7 = CN) |
| 204 | Đã xong | `POST ShiftAssignment/Confirm` | BE2.6 | Không gửi Telegram (`notificationSent = false`, D-55 k) |
| 205 | Đã xong | `POST AttendanceEvent/CheckIn`, `CheckOut`; `GET AttendanceEvent/GetMine` | BE2.6 | GPS / QR / MACHINE / PROXY (D-55 c, e); ngoài bán kính ghi IsValid = false |
| 206 | Đã xong | `GET AttendanceEvent/GetTimesheet`, `GetTimesheetDetail`; `POST ShiftAssignment/MarkAbsent` | BE2.6 | Danh sách, không báo cáo; vắng tự tính (D-55 j) |
| 207 | Đã xong | `POST LeaveRequest/Create`, `Approve`, `Reject`; `GET LeaveRequest/GetDetail`, `GetMine` | BE2.6 | LEAVE_REQUEST 1 cấp `hr-leave`; duyệt bỏ ca (D-55 f, g) |
| 208 | Đã xong | `POST ShiftSwapRequest/Create`, `Approve`, `Reject`; `GET ShiftSwapRequest/GetDetail`, `GetMine` | BE2.6 | SHIFT_SWAP 1 cấp `hr-swap`; hoán đổi / nhường ca (D-55 h, i) |
| 209 | Đã xong | `POST LeaveRequest/Cancel`, `ShiftSwapRequest/Cancel` | BE2.6 | Chỉ Chờ duyệt, người tạo; hủy luôn ApprovalRequest |

PROPOSED chờ anh (DECISIONS khối **"BE2.6"** cuối file – **D-55** (a)–(k), mẫu D-49 – em đã cài theo "Đề xuất", không mục nào BLOCKED): (a) Employee.Status Inactive / Resigned = nghỉ việc; (b) tài khoản ↔ nhân viên theo UserExternalIdentity → mã = tên đăng nhập → SĐT (GAP-HR-02); (c) hình thức GPS / QR / MACHINE / PROXY, chấm cho người khác cần quyền Thêm + ghi rõ hình thức; (d) 1 ca / người / ngày, dùng lại dòng xóa mềm (GAP-HR-03); (e) ngoài bán kính vẫn ghi IsValid = false, bán kính mặc định 150 m, vào ca sớm 120′, ân hạn 5′, trạm / muộn ghi Note (GAP-HR-04); (f) loại nghỉ + giới hạn ngày; (g) duyệt nghỉ 1 cấp, bỏ cả ca Đã xác nhận; (h) đơn đổi ca số PH-, không sinh CC- (GAP-HR-05); (i) người nhận đổi ca đồng ý ngoài hệ thống, có nhường ca; (j) vắng tự tính + MarkAbsent; (k) không gửi Telegram.

Cần anh: không có SP / seed mới (không phải chạy gì thêm trên staging / Docker). Quy ước tạo tài khoản nhân viên: **tên đăng nhập = mã nhân viên** để nhân viên tự chấm công / lập đơn (D-55 b). Nhập tọa độ + bán kính cho trạm bếp thật (`KitchenWorkstation`) – hiện các trạm OPS-KIT-* trên DB dev chưa có tọa độ. FE: đọc `docs/frontend-handoff/BE2.6.md` mục "Thay đổi hành vi" (CRUD phân ca / chấm công / đơn bị chặn).

### BE2.7 – API dùng chung + mẫu in · 2026-11-17 · DONE (PDF chưa có – D-56 b)
Nhánh `feat/BE2.7-common-print` (worktree `wt-BE27`, từ `release/pilot` @ `724488d` = T48.3 + BE2.1–BE2.6). Chỉ backend (D-49); không đụng `app-delicare-client/`, `docs/reference/*`. Refresh token (Catalog 32) **không làm** (D-47).

Kế hoạch:
1. Rà soát: `DocumentFileController` cũ chỉ có `Delete`; `DocumentFileService` cũ (TestDemo) lưu tệp trong wwwroot; `AuditChangeController` chỉ có GetAll; mẫu in cũ: `PurchaseOrder/ExportOrder`, `MaterialRequirement/ExportPreProduction`, `ProductionPlan` / `ProductionBatch/ExportProductionSheet`, `DeliveryRoute/ExportDeliveryNote`, `Menu/ExportForCustomer` (ClosedXML .xlsx); GemBox.Document (Word, code TestDemo) có sẵn. `BUSINESS_DOCS.md` không có mẫu thật cho 6 phiếu của BE2.7.
2. Danh sách trắng + thống kê trạng thái + xuất Excel / CSV + lịch sử gộp (`CommonRegistry`, `CommonService`, `CommonController` – controller không gắn bảng như `DashboardController`).
3. Tệp đính kèm: thêm `Upload` / `GetList` / `Download` vào đúng `DocumentFileService` / `DocumentFileController` cũ; lưu ngoài wwwroot theo cấu hình `DocumentStorage`.
4. Mẫu in: action `Print` ở 6 controller chứng từ (PermissionConvention → quyền In) + `Common/PrintBatch`; nội dung lấy từ GetDetail sẵn có.
5. Test InMemory + SQL Server rollback + bảo mật; `.http`; handoff; DECISIONS D-56; deploy (volume tệp).

Đã làm (mẫu 4.2; không SP / seed / bảng / cột / NuGet mới):
- **Danh sách trắng** `Services/Implement/CommonRegistry.cs`: 35 danh sách (key = tên controller cũ, màn = ScreenMap – test kiểm), cột trạng thái / ngày / tìm chữ / cột xuất (tiêu đề tiếng Việt), dòng con cho lịch sử, nhãn khóa ngoại. Truy vấn EF dựng bằng biểu thức từ thuộc tính có thật, giá trị qua biến → tham số SQL; không nhận tên bảng / cột từ client. `GET Common/GetResources` (chỉ danh sách có quyền Xem).
- **Catalog 2** `GET Common/GetStatusSummary` (Resource, SearchText, FromDate, ToDate): `total` + `listByStatus` đủ mã STATUS_CATALOG (kể cả 0) + mã lạ + "(Chưa có trạng thái)". Quyền Xem màn của danh sách.
- **Catalog 8** `GET Common/ExportList` (… + Status, Format xlsx / csv): ClosedXML (tiêu đề, giờ xuất, STT, A4 ngang), trạng thái → nhãn, khóa ngoại → tên / số (theo nhóm 500 Id), ≤ `BusinessRules:Common:ExportMaxRows` 10 000 (cắt + ghi chú); CSV UTF-8 BOM, ô `= + - @` thêm `'`. Quyền Xuất.
- **Catalog 15** `GET Common/GetHistory` (resource, id): AuditChange của bản ghi + dòng con, ApprovalRequest (BusinessId) + ApprovalDecision từng cấp + kết thúc (hủy…), ComplaintHistory (phản ánh), tệp đính kèm; mới nhất trước, ≤ 500; che mật khẩu / token / secret / license key / chuỗi kết nối / tọa độ / ConfigJson khi đọc (cả dòng cũ), bỏ cột hệ thống + dòng Actor / CorrelationId; tên "Họ tên (tên đăng nhập)". Quyền Xem.
- **Catalog 19–21** `POST DocumentFile/Upload` (multipart: File, Category, Resource, RecordId), `GET DocumentFile/GetList`, `GET DocumentFile/Download` (`[SkipPermission]` + kiểm quyền theo màn chứng từ trong service): ≤ `DocumentStorage:MaxFileSizeMb` 20, đuôi pdf / jpg / jpeg / png / xlsx / docx ∩ cấu hình, luôn chặn tệp chạy được / html / svg / Office macro, kiểm chữ ký đầu tệp + Content-Type; tên an toàn; khóa lưu trữ `<Resource>/<RecordId:N>/<category>/<sha256>.<ext>` trong `FileNameHas` (GAP-DOC-01), đường dẫn tuyệt đối kiểm nằm trong gốc (`DocumentStorageRules`); trùng nội dung → trả tệp cũ; ghi DB lỗi → xóa tệp vừa ghi; tải xuống ghi AuditChange `Download` (SaveChanges thường – không sinh audit của audit), `nosniff`. Cấu hình `DocumentStorage` (Startup đổi đường dẫn tương đối theo ContentRoot), `.gitignore` `**/App_Data/`, Dockerfile tạo thư mục, compose volume `delicare-docs`, `deploy/README.md`.
- **Catalog 24–29** `GoodsReceipt/Print` (QC đã kết luận), `MaterialIssue/Print` (Đã xuất), `StockCount/Print` (chưa ghi sổ → "BẢN NHÁP"), `DeliveryOrder/Print`, `ProofOfDelivery/Print`, `OrderReconciliation/Print` (khác Mở): `PrintService` dựng `PrintDocumentModel` từ GetDetail sẵn có (GR / MI / SC / đối soát) hoặc entity (phiếu giao, POD), `PrintRenderer` vẽ **xlsx (mặc định, ClosedXML)** / **docx (GemBox)**; đầu trang bảng `Company`; bố cục 01-VT / 02-VT / 05-VT tham khảo (D-56 c). **PDF → 400**: GemBox.Document trong `Library/` là bản .NET Framework – `Save(PdfDefault)` lỗi `System.Windows.Rect` trên .NET 8 (đã thử); Syncfusion / EvoPdf chưa license → GAP-PRINT-01, D-56 b.
- **Catalog 30** `POST Common/PrintBatch` (JSON: items [{type, id}], deliveryRouteId, format): thêm mọi phiếu giao của chuyến theo thứ tự điểm dừng, bỏ trùng, ≤ 100 (`PrintBatchMaxItems`), quyền In theo từng loại, 1 phiếu lỗi → 400 liệt kê; 1 tệp (Excel 1 sheet / phiếu, Word 1 trang / phiếu).
- Thiếu quyền ở API dùng chung → **HTTP 403 kèm Result** (responseCode 403, nêu quyền + màn). Thông báo tiếng Việt `ConstantHelper.Message` khối BE2.7; cấu hình `BusinessRules:Common` (`CommonRulesOptions`).

File chính: `Services/Implement/{CommonRegistry, CommonAccess, CommonService, DocumentStorageRules, PrintService, PrintRenderer}.cs` (mới), `Implement/DocumentFileService.cs`, `Interface/{ICommonService, IPrintService}.cs` (mới), `Interface/IDocumentFileService.cs`, `WebApi/Controllers/CommonController.cs` (mới), `{DocumentFile, GoodsReceipt, MaterialIssue, StockCount, DeliveryOrder, ProofOfDelivery, OrderReconciliation}Controller.cs`, `WebApi/{Startup.cs, appsettings.json, Dockerfile}`, `Model/{Model/CommonModels, Option/DocumentStorageOptions, Option/CommonRulesOptions, Request/Custom/CommonCustomRequest}.cs` (mới), `Model/{Option/BusinessRulesOptions, Helper/ConstantHelper}.cs`, `.gitignore`, `deploy/docker/docker-compose.staging.yml`, `deploy/README.md`; test `Tests/Common/{CommonApiTests, PrintTests, DocumentFileTests, CommonSqlServerTests, CommonTestUsers}.cs` (mới); `docs/api-tests/be2.7-common-print.http` (mới); `docs/frontend-handoff/BE2.7.md` (mới), `README.md`; `docs/claude/{DECISIONS, ROADMAP, PROGRESS}.md`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **234/234** (baseline 222 + 12; không mất / bỏ qua test):
  - `CommonApiTests` (5): danh sách trắng – mọi cột có thật, màn khớp ScreenMap, cột khóa ngoại có nhãn, cột xuất không nhạy cảm, bảng hệ thống / chữ lạ / `../` không tra được; thống kê (đủ mã, lọc chữ + ngày, 400 cho User / "Order;DROP TABLE" / không cột trạng thái); xuất xlsx (nhãn khách / trạng thái, chuỗi "=HYPERLINK" không thành công thức) + CSV (BOM, `'=`), pdf / UserRole → 400; quyền (Xem không Xuất → 403, màn khác → 403, không quyền → 403, chưa đăng nhập → 401, GetResources chỉ màn được Xem); lịch sử (thứ tự, dòng con, duyệt + bình luận, xóa mềm, che PassWord / Latitude / BotToken cả trong JSON, ComplaintHistory).
  - `PrintTests` (3): phiếu nhập Chờ QC → 400, sau QC → xlsx (mặc định) / docx (ghi chú có xuống dòng), pdf → 400, nội dung dòng (lô, số lượng, thành tiền), quyền In (Xem → 403, In → 200); phiếu xuất Nháp → 400 / Đã xuất → in; đối soát Mở → 400 / Đóng → in; kiểm kê Đang kiểm = BẢN NHÁP, Đã ghi sổ = không; phiếu giao (khung giờ, tài xế, bữa), POD; in hàng loạt theo chuyến (thứ tự điểm dừng, tên sheet không trùng), docx, 101 phiếu / loại sai / rỗng / không tìm thấy → 400, thiếu quyền In POD → 403.
  - `DocumentFileTests` (3, factory riêng, thư mục tạm): tải lên → tệp tên SHA-256 trong thư mục cấu hình, khóa lưu trữ, audit Added; trùng → isDuplicate; danh sách theo chứng từ / đuôi / loại; tải xuống (nội dung, attachment, nosniff, AuditChange Download); lịch sử có FILE; quyền (Xem POD → tải được, tải lên cần Sửa, màn khác → 403, mọi tệp cần adm-document, 401); tài liệu chung; xóa mềm → 400. Chặn .exe / .svg / .html / exe đổi đuôi .pdf, .png / Content-Type sai / Office macro / không đuôi / > 1 MB / rỗng / category `../../etc` / Resource `../User`, `User` / thiếu RecordId / chứng từ không có; tên `..\..\windows\evil.png` → chỉ giữ tên, tệp vẫn trong gốc; không ghi DB khi bị từ chối. Khóa lưu trữ đi ra ngoài gốc → ném lỗi.
  - `CommonSqlServerTests` (1, SQL Server dev, đã chạy – không skip): thống kê / xuất / lịch sử với AuditChange thật (Added → Modified), lọc tệp theo khóa (`LIKE`), in phiếu nhập xlsx / docx từ GetDetail thật; rollback, không để lại dữ liệu.
- **Chạy thật trên DB dev** (WebApi Development cổng 5327, trình chạy REST Client nhỏ – kiểm `responseCode` theo "→ 400"): `be2.7-common-print.http` **43/43** (15 chốt an toàn), dùng dữ liệu [DEMO] của các lần chạy trước (phiếu nhập PH-261117-0232, phiếu xuất PH-261117-0263, kiểm kê PH-261117-0254, POD của PH-261229-0001, đối soát PH-261117-0230). Lần chạy đầu 41/43: docx lỗi khi ghi chú thật có xuống dòng (GemBox Run) → đã sửa (`OneLine`) + thêm vào test. Tệp [DEMO] PDF thử nằm ở `AppDelicare.WebApi/App_Data/DocumentFiles` (gitignored). `pilot-flow.http` không chạy lại: BE2.7 không sửa logic luồng (chỉ thêm action / constructor `DocumentFileService`); `PilotFlowSqlServerTests` vẫn xanh.
- `git status`: không thay đổi trong `app-delicare-client/`, `docs/reference/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không `api/v1` mới. Không SP / seed mới → `deploy/db/scripts.order`, `DB/README.md` không đổi.

AC (BE2.7): [x] thống kê trạng thái (2) · [x] xuất Excel danh sách (8) + CSV · [x] tệp đính kèm tải lên / danh sách / tải xuống (19–21), ngoài wwwroot, kiểm kích thước / đuôi / nội dung, chống path traversal, quyền theo màn chứng từ · [x] lịch sử gộp AuditChange + ApprovalDecision (15), che bí mật / GPS · [~] mẫu in 24–30: đủ 7 chức năng, xuất xlsx / docx – **chưa PDF** (D-56 b) · [x] test + bảo mật + `.http` · [x] handoff · [ ] Catalog Excel (máy không có openpyxl – bảng dưới) · [–] refresh token (32): để sau (D-47).

Bảng cập nhật `DELICARE_API_Catalog.xlsx` (sheet `API_Catalog`) – STT catalog → Trạng thái / API thực tế / Task:
| STT | Trạng thái (J) | API thực tế – khung cũ (L) | Task (M) | Ghi chú (K) |
|---|---|---|---|---|
| 2 | Đã xong | `GET Common/GetStatusSummary`; `GET Common/GetResources` | BE2.7 | Danh sách trắng 35 danh sách; đủ mã STATUS_CATALOG (D-56 g) |
| 8 | Đã xong | `GET Common/ExportList` (xlsx / csv) | BE2.7 | Quyền Xuất; ≤ 10 000 dòng (D-56 g) |
| 15 | Đã xong | `GET Common/GetHistory` | BE2.7 | + dòng con, ComplaintHistory, tệp; che bí mật / GPS (D-56 h) |
| 19 | Đã xong | `POST DocumentFile/Upload` | BE2.7 | Khóa lưu trữ trong FileNameHas (GAP-DOC-01, D-56 a, i); quyền Sửa màn chứng từ |
| 20 | Đã xong | `GET DocumentFile/GetList` | BE2.7 | Lọc được theo chứng từ (Resource + RecordId) |
| 21 | Đã xong | `GET DocumentFile/Download` | BE2.7 | Quyền Xem màn chứng từ; audit lượt tải; url nội bộ không ký (D-56 j) |
| 24 | Một phần | `GET GoodsReceipt/Print` | BE2.7 | xlsx / docx, chưa PDF (GAP-PRINT-01, D-56 b) |
| 25 | Một phần | `GET MaterialIssue/Print` | BE2.7 | như 24 |
| 26 | Một phần | `GET StockCount/Print` | BE2.7 | như 24; BẢN NHÁP khi chưa ghi sổ |
| 27 | Một phần | `GET DeliveryOrder/Print`; (theo chuyến: `DeliveryRoute/ExportDeliveryNote` T19) | BE2.7 | như 24 |
| 28 | Một phần | `GET ProofOfDelivery/Print` | BE2.7 | như 24; chữ ký in tham chiếu (D-56 d) |
| 29 | Một phần | `GET OrderReconciliation/Print` | BE2.7 | như 24 |
| 30 | Một phần | `POST Common/PrintBatch` (JSON) | BE2.7 | ≤ 100, theo chuyến; xlsx / docx gộp |
| 32 | Không làm | – | – | Refresh token để sau (D-47, GAP-AUTH-01) |

PROPOSED chờ anh (DECISIONS khối **"BE2.7"** cuối file – **D-56** (a)–(j), mẫu D-49, không mục nào BLOCKED): (a) liên kết tệp ↔ chứng từ qua khóa lưu trữ trong `FileNameHas` (GAP-DOC-01); **(b) PDF: em đề xuất anh duyệt NuGet GemBox.Document bản .NET (đã có license) – hiện xlsx / docx (GAP-PRINT-01)**; (c) bố cục phiếu theo 01-VT / 02-VT / 05-VT tham khảo (chưa có mẫu thật); (d) POD in tham chiếu chữ ký; (e) điều kiện in; (f) quyền API dùng chung (tải lên = Sửa), 403 có body; (g) danh sách trắng + giới hạn xuất; (h) phạm vi lịch sử + che; (i) trùng / nơi lưu / xóa mềm giữ tệp; (j) url nội bộ không ký.

Cần anh: chọn thư viện PDF (D-56 b); nếu có mẫu thật của 6 phiếu thì gửi để chỉnh bố cục; staging IIS cấp quyền ghi `App_Data\DocumentFiles` (hoặc đặt `DocumentStorage__RootPath`) + sao lưu thư mục cùng DB; Docker đã có volume `delicare-docs`. Không có SP / seed mới. FE: đọc `docs/frontend-handoff/BE2.7.md` (403 có body ở API dùng chung; mẫu in là tệp Excel / Word).

### BE2.9 – Gộp, kiểm tra, deploy (cuối đợt BE-2) · 2026-11-17 · DONE
Nhánh `feat/BE2.9-full-flow` (worktree `wt-BE29`, từ `release/pilot` @ `7b0fc43` = T48.3 + BE2.1–BE2.7; BE2.8 để sau – D-45). Chưa push / chưa gộp `main` – **dừng, chờ anh duyệt**. Không đụng `app-delicare-client/`, `docs/reference/*`; không bảng / cột / SP / seed mới.

Kế hoạch:
1. `pilot-flow.http` → `full-flow.http` (toàn luồng R1 + chốt ATTP + kiểm số liệu), chạy thật trên DB dev; chạy lại mọi `be2.x-*.http` trên code đã gộp.
2. Test HTTP toàn luồng trên SQL Server (transaction ngoài, rollback).
3. Soát `deploy/db/scripts.order` ↔ `DB/sp`, `DB/seed`; `DB/README.md` + khóa cấu hình BE-2.
4. Deploy staging như T50R1 / T48.1 (DB + publish + thư mục tệp), smoke test, chạy toàn luồng trên staging, dừng.
5. UAT_CHECKLIST mục TOÀN LUỒNG R1, handoff README, ROADMAP, PROGRESS (tổng hợp PROPOSED còn mở).

Các task BE-2 đã xong (đã gộp `release/pilot`): **BE2.1** lấp chỗ thiếu luồng (đóng gói, xếp xe, đơn giao, lập chuyến nhiều xe, thực hiện giao, kế hoạch theo ca, đề nghị mua chọn dòng, nhiệt độ tự do, hao hụt, truy xuất) · **BE2.2** hợp đồng + dự báo + đổi món + sửa đơn sau giờ chốt · **BE2.3** duyệt nhiều cấp (đề nghị mua 2 cấp, kiểm kê, điều chỉnh tồn, PO theo ngưỡng) · **BE2.4** giữ chỗ, hàng sắp về, hủy / đóng lệnh, công đoạn theo tổ, giá thành + giá vốn suất, lô hết hạn · **BE2.5** checklist ATTP + lỗi trọng yếu, vệ sinh khay 6 bước, hộp / khay, sự cố NCC · **BE2.6** nhân sự (lưới ca, chấm công GPS, đơn nghỉ / đổi ca) · **BE2.7** API dùng chung (thống kê, xuất Excel / CSV, lịch sử, tệp đính kèm) + mẫu in xlsx / docx · **T48.3** thứ tự script DB duy nhất · **BE2.9** (mục này). **BE2.8** (job) để sau (D-45).

Đã làm:
- **`docs/api-tests/full-flow.http`** (mới, 216 request, 22 chốt an toàn "→ 400", 60 dòng kiểm số liệu `# @expect`): danh mục → tồn đầu kỳ lô bị chặn (duyệt điều chỉnh) → hợp đồng `Contract/Create` → Submit → Approve (+ chốt thêm suất khi Hiệu lực) → dự báo Import → Publish → Matrix → thực đơn → `Order/GetNewTemplate` (gợi ý 1000 = dự báo) → Create → Submit → Confirm (lệch 0%) → Lock → **đề nghị mua định kỳ gạo 50 kg → 2 cấp → PO 1 triệu đã gửi (hàng sắp về)** → MRP (bò 200 / bị chặn 80 / mua 200; gạo 120 / sắp về 50 / mua 70) → đề nghị 2 cấp → PO 61,4 triệu chỉ ở hộp Giám đốc → nhận hàng + QC (9 °C chặn) + nhận PO định kỳ → in phiếu nhập xlsx / docx (pdf → 400) → chặn xuất tay lô hết hạn → phát hành (giữ chỗ bò 200) → mẻ Bò kho theo tổ SO_CHE → NAU → CHIA_SUAT (chặn nhảy công đoạn, Finish trước xong Nấu, lõi 65 °C, Chia suất trước Đạt) → lệnh đóng gói (chặn Pack thẳng) → mẻ Cơm xuất FEFO 2 lô (lô định kỳ hạn sớm trước) → checklist QA_FG Cảm quan FAIL trọng yếu giữ mẻ → kiểm lại → đóng gói → chặn giá vốn khi chưa giao → chuyến (55 °C, chưa lưu mẫu, **chưa xếp xe** – 3 chốt) → Load → Dispatch → bàn giao 58 °C chặn / 63 °C → POD → in biên bản / phiếu giao / POD → tải lên PDF bằng chứng POD → danh sách → tải xuống (.exe → 400) → thu hồi hộp → kiểm tra → vệ sinh (thiếu hóa chất → 400) → số dư OUT 998 / IN 990 / còn 8 → đối soát 34.650.000 → in → phản ánh → đóng 2 lệnh SX (đóng lại → 400) → giá thành NVL 62.400.000 → giá vốn suất (990 suất nhận) → phiếu xuất / in hàng loạt / thống kê / xuất xlsx + csv / lịch sử PO + POD (có tệp) / truy xuất ngược → xử lý lô hết hạn (xem trước → xử lý → 0) → nhân sự smoke (phân ca hôm nay, chấm hộ thiếu lý do → 400, vào ca GPS ~22 m, không trả tọa độ, bảng công). `pilot-flow.http` **giữ lại** (đầu file ghi "đã thay bằng full-flow.http"; còn được tham chiếu ở handoff / PROGRESS cũ).
- Trình chạy `.http` nhỏ (scratchpad, không commit – như các task trước) thêm: multipart CRLF, nhận tệp theo Content-Type, kiểm `# @expect <đường dẫn> <phép> <giá trị>` (`==`, `!=`, `>=`, `contains`, `~` regex, `length ==`, `[field=value]`).
- **Test `AppDelicare.Tests/Pilot/FullFlowSqlServerTests.cs`** (mới, SQL Server dev, `[SkippableFact]`, đã chạy – không skip): cùng toàn luồng qua HTTP thật trong 1 transaction ngoài (scope DI dùng chung như `PilotFlowSqlServerTests`), rollback; tệp đính kèm ghi vào thư mục tạm (`DocumentStorage:RootPath` qua `UseSetting`) rồi xóa; không gọi danh sách Dapper (kết nối riêng sẽ chờ khóa) – Id lấy bằng DbContext. Kiểm 14 bảng không đổi số dòng sau rollback (Order, Contract, Forecast, PurchaseOrder, MaterialLot, InventoryTransaction, ProductionBatch, DeliveryRoute, CustomerComplaint, TemperatureLog, PackagingLedger, DocumentFile, AttendanceEvent, ApprovalRequest). `PilotFlowSqlServerTests` giữ nguyên.
- **`deploy/db/scripts.order`** soát bằng `diff`: 140 dòng = 131 `DB/sp/*.sql` + `00_Admin_seed.sql` + 8 seed = đúng toàn bộ `DB/sp` + `DB/seed` (không thiếu / thừa). `DB/README.md`: dòng BE2.6 / BE2.7 / BE2.9, sửa thứ tự bảng seed (12 ProductionStage trước 13 FoodSafetyQa), "6 seed" → "8 seed", **bảng khóa cấu hình mới của BE-2** (BE2.1 … BE2.7, kể cả `DocumentStorage:*`, `Licenses:GemBox`).
- `deploy/README.md` mục 9: bước 5 dùng `full-flow.http`, bước 6 thư mục tệp đính kèm staging, bước 7 cách dừng, dòng "lần cập nhật gần nhất". `docs/frontend-handoff/README.md`: mục lục toàn luồng + thay đổi hành vi BE2.9. `UAT_CHECKLIST.md`: mục **TOÀN LUỒNG R1** (23 kịch bản theo phân hệ, vai trò, kỳ vọng) thay phạm vi PILOT (mục PILOT giữ, ghi "đã thay"). Đầu các `be2.x-*.http` ghi kết quả chạy lại BE2.9.

Lỗi tìm được và đã sửa (có test):
1. **6 bảng điều hành theo ngày trả HTTP 500 khi thiếu tham số ngày** (lộ khi smoke test staging gọi `DeliveryExecution/GetBoard` không ngày; dò thêm cả họ): `DeliveryExecution/GetBoard`, `DeliveryRoute/GetBoard`, `DeliveryRoute/GetReadyOrders`, `ProductionBatch/GetBoard`, `ProductionBatch/GetStageBoard`, `Forecast/GetMatrix` – `[Required] DateTime` không bắt được giá trị mặc định → `DateTime.MinValue` xuống SQL → `SqlDateTime overflow`. Sửa như `MaterialRequirement/GetByDate`: kiểm ngày mặc định ở đầu service → 400 "Chọn ngày giao" (hằng mới `ConstantHelper.Message.DeliveryDateRequired`) / "Chọn ngày sản xuất" (`MrpDateRequired`) / "Khoảng ngày không hợp lệ…" (`ForecastService.RangeError` – áp luôn cho `Publish`). Test `Tests/Pilot/MissingDateParameterTests.cs` (7 trường hợp thiếu ngày + 1 có ngày vẫn 200). FE nên luôn gửi ngày mặc định (handoff README).
Ghi chú (không phải lỗi, không sửa): máy dev chạy múi giờ PST nên `CreatedDate` (DatabaseContext dùng `DateTime.Now` – D-18) trên DB dev là giờ PST; máy chủ thật / Docker đặt `TZ=Asia/Ho_Chi_Minh` nên đúng giờ VN. Giá vốn suất [DEMO] cao hơn giá bán (bò 20 kg / 100 suất × 300.000 = 60.000 đ / phần) – do số thử, logic đúng D-53 g.

File chính: `AppDelicare.Services/Implement/{DeliveryExecutionService, DeliveryRouteService, ProductionBatchService, ForecastService}.cs`, `AppDelicare.Model/Helper/ConstantHelper.cs`; test `AppDelicare.Tests/Pilot/{FullFlowSqlServerTests, MissingDateParameterTests}.cs` (mới); `docs/api-tests/full-flow.http` (mới), `pilot-flow.http`, `be2.1…be2.7-*.http` (dòng đầu); `DB/README.md`, `deploy/README.md`; `docs/claude/{UAT_CHECKLIST, ROADMAP, PROGRESS, DECISIONS}.md`, `docs/frontend-handoff/README.md`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline).
- `dotnet test`: **243 / 243** (baseline 234 + 9: FullFlowSqlServerTests 1 – SQL Server dev, không skip; MissingDateParameterTests 8), không mất / bỏ qua test.
- **Chạy thật trên DB dev** (`dotnet run` WebApi Development cổng 5329, `(localdb)\MSSQLLocalDB` / `AppDelicare`, trình chạy REST Client nhỏ – kiểm `responseCode` theo "→ 400" + `# @expect`):
  - `full-flow.http` **216 / 216** (22 chốt, 60 @expect) – 3 lần: B29F1 01/12/2026 (215 / 216 – 1 sai do tiêu đề khối in có chữ "→ 400" nhầm, đã sửa file), **B29F2 04/12/2026** 216 / 216, **B29F3 23/11/2026** 216 / 216 (sau khi sửa lỗi 1). Số chứng từ B29F2: HĐ `HD-261117-007`, đơn `PH-261117-0329` (→ Reconciled), đề nghị định kỳ `DEMOB29F2-DK` → `PO-261117-0028`, kế hoạch `KH-261204`, đề nghị MRP `PH-261117-0330` → `PO-261117-0029` (61.400.000, GĐ duyệt), phiếu nhập `PH-261117-0331`, mẻ `ME-261204-TRUA-01` (Bò kho, 3 công đoạn) / `-02` (Cơm), phiếu xuất `PH-261117-0335`, lệnh đóng gói `PH-261117-0336`, chuyến `CX-261204-DEMOB29F2-01` (Hoàn thành), phản ánh `PA-261117-0038`, mẻ vệ sinh `VS-261117-TRUA-*`.
  - Chạy lại trên code đã gộp: `pilot-flow.http` **141 / 141** (B29P, 30/11/2026), `be2.1` **147 / 147** (B29G, 07/12/2026), `be2.2` **73 / 73** (B29C, 27/11/2026), `be2.3` **58 / 58** (B29E), `be2.4` **125 / 125** (B29B, 14/12/2026), `be2.5` **142 / 142** (B29A, 24/11/2026), `be2.6` **65 / 65** (B29H), `be2.7` **43 / 43**. Dữ liệu `[DEMO]` để lại trên DB dev; không chạy script SQL nào trên dev.
- **Staging (T48.1 / mục 9):** `Deploy-Database.ps1 -Server "(localdb)\MSSQLLocalDB" -Database AppDelicare_Staging` (schema có sẵn – không đụng bảng; 131 SP + 8 seed, `ObjectMenu_disable_legacy` tắt 0); `Repair-VietnameseText.ps1` chạy thử: 0 giá trị lỗi font (kiểm tay tên 8 bộ checklist, 4 ca đúng dấu; 4 mục duyệt BE2.3 có); `Publish-Iis.ps1 -Output C:\Deploy\delicare-staging -Environment Staging` (2 lần – lần 2 sau khi sửa lỗi; bản build Angular cũ trong `wwwroot` giữ nguyên, **không** build lại FE; `App_Data` của máy dev không bị publish); tạo thư mục `C:\Deploy\delicare-staging\App_Data\DocumentFiles`; chạy `dotnet AppDelicare.WebApi.dll --urls http://localhost:5200` với biến môi trường của `deploy/staging.local.env.ps1` (không in bí mật). Smoke **49 / 49**: `/health` Healthy, `/health/live`, Swagger, trang `/` (DELI-CARE), đăng nhập admin staging, GET 200 cho 43 API chính của mọi phân hệ (đơn, hợp đồng, dự báo, MRP, mua, nhận hàng, QC, kho, lô, giữ chỗ, kiểm kê, kế hoạch, mẻ, tiến độ tổ, đóng gói, chuyến, đơn giao, thực hiện giao, đối soát, phản ánh, hộp / khay, vệ sinh, sự cố NCC, checklist, hộp duyệt, phân ca, bảng công, đơn nghỉ / đổi ca, giá thành, giá vốn, chênh lệch, API dùng chung, tệp, ca, tổ chức) + xuất Excel; lần đầu lộ lỗi 1 (đã sửa, publish lại, smoke lại đạt). Sau đó chạy **`full-flow.http` trên staging: 216 / 216** (key B29S, ngày 01/12/2026 – gồm in phiếu nhập / phiếu giao / POD / đối soát / in hàng loạt xlsx + docx và tải lên / tải xuống tệp PDF: tệp nằm ở `App_Data\DocumentFiles\ProofOfDelivery\…\evidence\<sha256>.pdf`); log staging 0 dòng `[Error]`. Dữ liệu `[DEMO]` key B29S để lại trên staging (mẫu cho UAT). **Đã dừng API staging** (và API dev).
- **Khởi động lại staging:** `. <repo>\deploy\staging.local.env.ps1; cd C:\Deploy\delicare-staging; dotnet AppDelicare.WebApi.dll --urls http://localhost:5200` (deploy/README.md mục 9 bước 4; dừng: Ctrl+C – bước 7). Mật khẩu admin staging trong `deploy/staging.local.md` (gitignored).
- `git status`: không thay đổi trong `app-delicare-client/`, `docs/reference/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không `api/v1` mới; không NuGet mới.

AC (BE2.9): [x] mỗi task build 0 lỗi + test xanh + `.http` chạy được trên DB dev (chạy lại đủ 8 file) · [x] `scripts.order` / `DB/README.md` đủ SP / seed + khóa cấu hình · [x] `full-flow.http` luồng đầy đủ chạy hết (dev + staging) · [x] test tự động toàn luồng qua HTTP · [x] deploy staging + smoke · [x] dừng, báo anh (không push / gộp) · [ ] Catalog Excel (máy có Python 3.14 nhưng **không có openpyxl** – bảng dưới) · [ ] IIS thật / Docker thật (cần quyền admin / máy có Docker – như T48.1, T48.3).

**Bảng tóm tắt Catalog (`DELICARE_API_Catalog.xlsx`, sheet `API_Catalog`) cần cập nhật cho cả đợt BE-2** – chi tiết từng dòng (cột J / K / L) ở bảng cuối mục BE2.1 … BE2.7; cột M = task:
| Task (M) | STT catalog | Trạng thái (J) |
|---|---|---|
| BE2.1 | 82, 90, 118, 119, 156, 165, 171–175, 177–181, 183–188, 197, 211, 213 | Đã xong |
| BE2.1 | 107, 137 | Một phần → **Đã xong** sau BE2.2 / BE2.3 (137 áp đổi món – BE2.2; 107 đề nghị do MRP phải duyệt 2 cấp – BE2.3) |
| BE2.2 | 78, 79, 80, 87, 88, 89, 91, 92, 96, 104, 137 | Đã xong |
| BE2.2 | 81 | Không làm (job hết hạn – D-45) |
| BE2.3 | 79, 110, 111, 131, 132, 210, 211, 212, 213 | Đã xong |
| BE2.4 | 121, 126, 128, 135, 140, 142, 144, 145, 152, 153, 154 | Đã xong |
| BE2.5 | 161, 162, 167, 168, 169, 170, 190, 194, 195, 196 | Đã xong |
| BE2.6 | 201–209 | Đã xong |
| BE2.7 | 2, 8, 15, 19, 20, 21 | Đã xong |
| BE2.7 | 24–30 | Một phần (xlsx / docx, chưa PDF – D-56 b) |
| BE2.7 | 32 | Không làm (refresh token – D-47) |
| BE2.9 | 87 (Forecast/GetMatrix), 188 (DeliveryExecution/GetBoard) + bảng chuyến / mẻ / tiến độ tổ / đơn chờ xếp chuyến (dòng của T17, T19, BE2.1, BE2.4) | Giữ trạng thái; K thêm "BE2.9: thiếu ngày → 400" |
| BE2.9 | mọi dòng của luồng trong `full-flow.http` | M thêm `BE2.9` (đã chạy end-to-end dev + staging) |

**DANH SÁCH PROPOSED / GAP CÒN MỞ CẦN ANH CHỐT (tổng hợp cuối BE-2, mẫu D-49 – em đã cài theo "Đề xuất", không mục nào BLOCKED):**
| Mục | Vấn đề | Đề xuất của em |
|---|---|---|
| **D-53 (a)–(h)** BE2.4 | Giữ chỗ theo lệnh × NL (FEFO suy ra); hàng sắp về chỉ PO có ngày giao ≤ ngày SX; hủy lệnh chỉ khi chưa nấu / chưa xuất; lô hết hạn WASTE toàn bộ (bỏ qua lô tạm giữ); công đoạn = checklist PRD_STAGE (không bắt buộc); giá thành không cộng hao hụt; giá vốn suất phân bổ theo phần món; chênh lệch tiêu hao ngưỡng 5% chỉ cảnh báo | **Đồng ý toàn bộ** như đã cài (toàn luồng chạy đúng với các quy tắc này). Riêng (e): khi bếp đã quen thì bật `ProductionRequireStages = true` |
| **D-54 (a)–(h)** BE2.5 | OUT = suất nhận × 1 khay khi bàn giao; loại sổ OUT / IN / LOSS / AVAILABLE; checklist ghi dòng mới; lỗi trọng yếu giữ mẻ; máy trạng thái sự cố NCC; vệ sinh lần lượt 6 bước; kiểm tra tự lập mẻ vệ sinh; ngưỡng thất thoát theo khách trong `Customer.ExternalData` | **Đồng ý toàn bộ** |
| **D-55 (a)–(k)** BE2.6 | Nghỉ việc = Inactive / Resigned; User ↔ Employee (b); GPS / QR / MACHINE / PROXY; 1 ca / người / ngày; ngoài bán kính vẫn ghi `IsValid = false`; loại nghỉ; duyệt nghỉ 1 cấp; đổi ca số PH-; người nhận đồng ý ngoài hệ thống; vắng tự tính; không gửi Telegram | **Đồng ý toàn bộ**; (b) xem GAP-HR-02 |
| **D-56 (a)–(j)** BE2.7 | Khóa lưu trữ tệp trong `FileNameHas`; PDF (b); bố cục 01-VT / 02-VT / 05-VT; POD in tham chiếu chữ ký; điều kiện in; quyền API dùng chung + 403 có body; danh sách trắng; phạm vi lịch sử + che; tệp trùng / nơi lưu; url không ký | **Đồng ý** (a), (c)–(j); (b) xem GAP-PRINT-01; (c) anh gửi mẫu thật 6 phiếu nếu có để chỉnh |
| **GAP-PRINT-01** (D-56 b) | ✅ **Đã xử lý ở BE2.7.1** – anh chốt 2026-11-17: PDFsharp-MigraDoc 6.2.4 (MIT) + font Noto Sans (OFL); PDF là định dạng in mặc định (PROPOSED D-56 k). **Không nâng lên 6.5.0** (đổi license sang AGPL). | (đã chốt) |
| **GAP-DLV-08** (D-50 c đã chốt cách tạm) | `DeliveryOrder` không có cột số suất – đang lưu tag `[SUAT:n]` trong Note | **Migration thêm cột `DeliveryOrder.MealQuantity decimal(18,4) NULL`** trước production (em chuyển đọc / ghi trong `DeliveryRules` + chuyển dữ liệu từ tag, 1 task nhỏ). Khác: giữ tag tới R2 (rủi ro sửa Note tay làm mất số suất) |
| **GAP-HR-02** (D-55 b) | Không có cột nối User ↔ Employee | **R1: quy ước tên đăng nhập = mã nhân viên** (đã chạy, không đổi lược đồ); dài hạn migration thêm `Employee.UserId uniqueidentifier NULL`. Khác: `UserExternalIdentity` + kênh "INTERNAL" (không đổi lược đồ nhưng phải seed kênh, dễ nhầm với Zalo / Telegram) |
| **GAP-DOC-01** (BE2.7, = GAP-F-09) | `DocumentFile` không có cột chứng từ / loại / SHA-256 – khóa lưu trữ trong `FileNameHas` | **R1 giữ cách hiện tại** (đã chạy dev + staging, có test chống path traversal); migration sau thêm `OwnerTable`, `OwnerId`, `Category`, `Sha256` khi anh duyệt đổi lược đồ. Lưu ý: mã **GAP-DOC-01 bị trùng** với GAP của T50R1 (số đơn giao PH- theo ngày giao) – em đề xuất đổi mã mục T50R1 thành **GAP-DOC-00** khi anh chốt (chưa đổi để không lệch handoff) |
| Câu hỏi cũ còn mở (không đổi trong BE2.9) | D-06 (khách Zalo), D-12 (tồn an toàn MRP), D-15 (IIS / Docker production), D-18 (múi giờ lưu DB), D-19 (buổi ăn ↔ ca), D-26 (`AuditChange/DeleteData`), D-27 (AutoMapper), D-25 (ma trận quyền), GAP-ORD / INV / MRP / PUR / PRD / DLV của Pilot (mục T50R1 "Câu hỏi còn mở") | Như đề xuất đang ghi trong DECISIONS mục B / D |

Cần anh: (1) duyệt nhánh `feat/BE2.9-full-flow` (2 commit) rồi gộp `release/pilot` → `main` khi đồng ý; (2) chốt bảng PROPOSED trên (đặc biệt **thư viện PDF**, **cột `DeliveryOrder.MealQuantity`**, **User ↔ Employee**, **cột liên kết DocumentFile**); (3) UAT toàn luồng trên staging theo `UAT_CHECKLIST.md` mục TOÀN LUỒNG R1 (khởi động lại theo deploy/README.md mục 9 bước 4); (4) cho phép cài openpyxl (hoặc tự cập nhật Catalog theo bảng trên); (5) IIS thật / Docker thật khi có máy. Kế tiếp theo ROADMAP: FE-ALL (dev FE, D-49) → UAT toàn luồng → T48.2 production.

### BE2.7.1 – Mẫu in PDF (PDFsharp-MigraDoc + font Noto Sans) · 2026-11-17 · DONE
Nhánh `feat/BE2.7.1-pdf` (worktree `wt-PDF`, từ `release/pilot` @ `7b0fc43`, chưa push / chưa gộp). Anh chốt 2026-11-17 ("đồng ý"): thêm NuGet **PDFsharp-MigraDoc 6.x (MIT)** + font tiếng Việt giấy phép **SIL OFL** → giải quyết GAP-PRINT-01 / D-56 (b). Chỉ backend; không bảng / cột / SP / seed; không đụng `app-delicare-client/`, `docs/reference/*`.

Kế hoạch:
1. Chọn bản gói trên nuget.org: **`PDFsharp-MigraDoc` 6.2.4** (bản Core đa nền tảng, phát hành 2026-01-06, MIT; phụ thuộc `PDFsharp` 6.2.4 MIT + `Microsoft.Extensions.Logging.Abstractions` / `System.Security.Cryptography.Pkcs` của Microsoft). **Không dùng 6.5.0** (2026-10-14) vì đã đổi license sang **AGPL-3.0** (ghi chú trong csproj).
2. Font: tải Noto Sans Regular + Bold (TTF tĩnh, hinted) + `OFL.txt` vào thư mục tạm trống, kiểm là TTF thật (`00 01 00 00`, bảng name "Noto Sans", © The Noto Project Authors), rồi chép vào `AppDelicare.Services/Fonts/`.
3. `PdfFontResolver` (IFontResolver, font nhúng trong DLL) + `PrintPdfRenderer` vẽ từ cùng `PrintDocumentModel` của Excel / Word; `PrintRenderer.Render` thêm nhánh pdf.
4. Định dạng mặc định: spec 2.5 ghi PDF → PROPOSED D-56 (k) PDF mặc định, cấu hình được.
5. Test (đọc lại PDF bằng PDFsharp), `.http`, handoff, deploy, DECISIONS.

Đã làm:
- **Gói** `AppDelicare.Services.csproj`: `PDFsharp-MigraDoc` **6.2.4** (MIT). Không gói nào khác.
- **Font** `AppDelicare.Services/Fonts/NotoSans-Regular.ttf`, `NotoSans-Bold.ttf` (EmbeddedResource `AppDelicare.Fonts.*`) + `OFL.txt` (chép ra bản build / publish thành `Fonts/NotoSans-OFL.txt` – điều kiện OFL khi phân phối lại). Nguồn: `https://raw.githubusercontent.com/notofonts/notofonts.github.io/main/fonts/NotoSans/hinted/ttf/NotoSans-{Regular,Bold}.ttf`, giấy phép `https://raw.githubusercontent.com/notofonts/latin-greek-cyrillic/main/OFL.txt` (SHA-256 Regular `478c558e…3823`, Bold `1df075a3…a913`).
- **`Services/Implement/PdfFontResolver.cs`** (mới): mọi họ font → Noto Sans (đậm = NotoSans-Bold, nghiêng = mô phỏng), đọc từ tài nguyên nhúng → không phụ thuộc font Windows / Linux; gắn 1 lần / tiến trình (`EnsureRegistered`, khóa).
- **`Services/Implement/PrintPdfRenderer.cs`** (mới): A4 (dọc; > 8 cột → ngang – ghi thẳng kích thước vì `Orientation.Landscape` của MigraDoc 6 vẫn ra trang dọc, đã thử), lề 36 pt; đầu trang pháp nhân (`Company`) + ký hiệu mẫu; tiêu đề; **"BẢN NHÁP – CHƯA CÓ GIÁ TRỊ PHÁP LÝ"** chữ đỏ + **chữ chìm "BẢN NHÁP" mỗi trang** (kiểm kê chưa ghi sổ); số / ngày; thông tin chung 2 cột; bảng dòng (tiêu đề cột màu thương hiệu, lặp lại mỗi trang; số canh phải; dòng tổng đậm; độ rộng cột tối thiểu đủ từ dài nhất – mã NL / số lô không tràn); ghi chú giữ xuống dòng; ô ký; chân trang "<tiêu đề> <số> · Trang x / y" (đếm theo phiếu). In hàng loạt: 1 PDF, mỗi phiếu 1 section bắt đầu trang mới.
- **`PrintRenderer`**: `Formats = [pdf, xlsx, docx]`, `PdfContentType`, `Render` / `ContentTypeOf` thêm pdf. **`PrintService.Format`**: nhận pdf / xlsx / docx; không gửi → `BusinessRules:Common:PrintDefaultFormat` (**`pdf`**, cấu hình sai → pdf); khác → 400 "chỉ pdf, xlsx, docx". Bỏ thông báo `PrintPdfNotSupported`. `CommonRulesOptions.PrintDefaultFormat` + `appsettings.json`.
- Comment 6 action `Print` + `Common/PrintBatch`, `IPrintService`, `PrintBatchRequest.Format` cập nhật. `Common/ExportList` (xuất danh sách) giữ xlsx / csv.
- **Docker / IIS**: font nằm trong DLL → **không cần gói font apt**; Dockerfile chỉ thêm comment; publish Release đã kiểm có `Fonts/NotoSans-OFL.txt` + `PdfSharp*.dll` / `MigraDoc*.dll`. `deploy/README.md` (mục tệp đính kèm / mẫu in) cập nhật.

Thư viện bên thứ ba (repo chưa có danh sách license riêng – ghi tại đây):
| Thành phần | Phiên bản | Giấy phép | Nguồn | Dùng ở |
|---|---|---|---|---|
| PDFsharp-MigraDoc (kéo theo PDFsharp) | 6.2.4 | MIT | nuget.org/packages/PDFsharp-MigraDoc/6.2.4 (empira, github.com/empira/PDFsharp) | `AppDelicare.Services` – PDF mẫu in |
| Noto Sans Regular / Bold (TTF) | notofonts 2022 (latin-greek-cyrillic) | SIL OFL 1.1 (`Services/Fonts/OFL.txt`) | github.com/notofonts/notofonts.github.io | nhúng trong `AppDelicare.Services.dll` |

File chính: `AppDelicare.Services/{AppDelicare.Services.csproj, Fonts/*, Implement/PdfFontResolver.cs (mới), Implement/PrintPdfRenderer.cs (mới), Implement/PrintRenderer.cs, Implement/PrintService.cs, Interface/IPrintService.cs}`, `AppDelicare.Model/{Option/CommonRulesOptions, Helper/ConstantHelper, Request/Custom/CommonCustomRequest}.cs`, `AppDelicare.WebApi/{appsettings.json, Dockerfile, Controllers/{GoodsReceipt, MaterialIssue, StockCount, DeliveryOrder, ProofOfDelivery, OrderReconciliation, Common}Controller.cs}`; test `AppDelicare.Tests/Common/{PdfText.cs (mới), PrintPdfRendererTests.cs (mới), PrintTests.cs, CommonSqlServerTests.cs}`; `docs/api-tests/be2.7-common-print.http`, `docs/frontend-handoff/BE2.7.md`, `deploy/README.md`, `docs/claude/{DECISIONS, ROADMAP, PROGRESS}.md`.

Kiểm tra:
- `dotnet build AppDelicare.sln --no-incremental`: 0 lỗi, 11 cảnh báo (= baseline, không cảnh báo mới; NuGet audit không báo lỗ hổng cho gói mới).
- `dotnet test`: **236/236** (baseline 234 + 2, không bỏ qua):
  - `PdfText` (trợ giúp test): đọc lại PDF bằng chính PDFsharp – `%PDF-`, số trang, khổ trang, font nhúng (`FontFile2`), **giải mã chữ qua bảng ToUnicode** (font Type0) + chuỗi WinAnsi → so được chữ tiếng Việt có dấu.
  - `PrintPdfRendererTests` (2, không DB): 1 phiếu → 1 trang A4 dọc, chỉ font Noto Sans và đã nhúng, đủ chữ tiếng Việt (tiêu đề, pháp nhân, ngày, nhãn, dòng, tổng, ghi chú nhiều dòng, ô ký, "Trang 1 / 1"), không có BẢN NHÁP; 120 dòng × 9 cột + nháp → nhiều trang A4 ngang, mỗi trang có chữ chìm BẢN NHÁP + tiêu đề cột lặp lại; in 3 phiếu → mỗi phiếu trang mới, số trang đếm lại.
  - `PrintTests` (sửa): **không gửi format → PDF**, `format=PDF`, xlsx / docx vẫn đúng, `html` → 400; PDF đủ nội dung 6 phiếu (nhập / xuất / kiểm kê nháp + đã ghi sổ không còn BẢN NHÁP / đối soát / phiếu giao / POD); in hàng loạt PDF theo chuyến: ≥ 3 trang, đúng 3 phiếu ("Trang 1 /" 3 lần), đúng thứ tự (POD → PG dừng 1 → PG dừng 2); quyền In giữ nguyên.
  - `CommonSqlServerTests`: thêm PDF phiếu nhập từ GetDetail thật trên SQL Server dev.
- **Chạy thật trên DB dev** `(localdb)\MSSQLLocalDB` / `AppDelicare` (WebApi Development cổng 5391, trình chạy `.http` nhỏ bằng node – kiểm `responseCode` / Content-Type / `%PDF-`): `be2.7-common-print.http` **47/47** (43 cũ + 4 mới: 6.3 pdf, 6.3b xlsx, 7.1b xlsx, 7.1c gộp 6 loại PDF; "pdf → 400" cũ đổi thành 6.3c "html → 400"). Mở kiểm PDF gộp 6 loại phiếu: 6 trang, đọc được toàn bộ chữ tiếng Việt, bố cục đúng. Tệp mẫu chỉ lưu ở thư mục tạm của phiên, không commit.
- `git status`: không thay đổi `app-delicare-client/`, `docs/reference/`; không file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; không `api/v1` mới; không SP / seed → `deploy/db/scripts.order` không đổi.

AC (BE2.7.1): [x] gói PDFsharp-MigraDoc MIT (duy nhất) · [x] font OFL nhúng, không phụ thuộc font hệ điều hành / Docker · [x] `format=pdf` 6 mẫu + in hàng loạt 1 PDF (trang mới mỗi phiếu) · [x] cùng nội dung Word / Excel (pháp nhân, BẢN NHÁP, ô ký) · [x] A4, tiếng Việt đúng · [x] test + `.http` trên DB dev · [x] Docker không cần thêm gì · [x] tài liệu · [ ] Catalog Excel (máy không có Python / openpyxl – bảng dưới).

Bảng cập nhật `DELICARE_API_Catalog.xlsx` (sheet `API_Catalog`): STT **24–30** → J **Đã xong**, M **BE2.7, BE2.7.1**, K "PDF (mặc định) / xlsx / docx – D-56 b đã chốt, D-56 k"; L giữ như BE2.7.

PROPOSED chờ anh: **D-56 (k)** – PDF là định dạng mặc định khi FE không gửi `format` (đổi lại Excel bằng `BusinessRules:Common:PrintDefaultFormat=xlsx`, không cần sửa code). FE chưa gọi API in (đã kiểm `app-delicare-client`) nên chưa ảnh hưởng; handoff đã ghi.

Cần anh: không có việc DB. Khi gộp BE2.7.1 lên staging chỉ cần build lại (font đi theo DLL).

### T23.1 – Job tự động (AppDelicare.Job, D-48) · 2026-11-17 · DONE
Nhánh `feat/T23-jobs` (worktree `wt-T23`, từ `release/pilot` @ `f648505`, chưa push / chưa gộp). Anh chọn 2026-11-17 ("T23 Job tự động") sau khi Catalog R1 đã làm hết; chỉ làm backend.

**Đã làm**
- **DI dùng chung (D-48):** `AppDelicare.Services/Ioc/ServiceRegistration.AddDelicareCore` (ConfigurationCommon, IClock, Options nghiệp vụ, DbContext, AutoMapper) + `DelicareServicesModule` (Autofac – quy tắc cũ của `WebApi/Ioc/ComponentRegistrar`, thêm `IScheduledJob`). WebApi `Startup` / `ComponentRegistrar` gọi lại 2 hàm này (bỏ `InitConnection`). AutoMapper profile chuyển `WebApi/Infrastructure` → `Services/Mapping` (CodeGen `Program.cs` / `Templates.cs` / README ghi vào chỗ mới). Gói: chỉ thêm tham chiếu **Autofac 6.5.0 (đã có trong solution)** cho Services – không gói NuGet mới.
- **Lõi job (`Services/Jobs`):** `IScheduledJob` + `JobOutcome` (Result 200 → Succeeded, 400 → Skipped), `JobSchedule` (HH:mm giờ VN, chạy bù trong ngày, lần kế tiếp), `JobRunService` (khóa `sp_getapplock` Session theo mã job → ghi `AnalyticsRefreshLog` Running → chạy trong `ICurrentActor.BeginScope` (SCHEDULE = user `system`, MANUAL = người bấm) → cập nhật Succeeded / Skipped / Failed / Locked; SourceWatermark = `<trigger>|<ngày nghiệp vụ>`).
- **5 job (`DailyJobs.cs`)**, mỗi job gọi đúng Service của nút bấm tay: `LockOrders` (Order.LockByMealDate D+1), `Mrp` (MaterialRequirement.Calculate D+1 – đã tạo đề nghị mua → gộp PurchaseProposal 21:00), `LotExpiry` (MaterialLot.ProcessExpired), `ContractExpiry` (**mới** `ContractService.ExpireOverdue`), `MarkAbsent` (ShiftAssignment.MarkAbsent hôm qua).
- **`AppDelicare.Job`** (Microsoft.NET.Sdk + framework ASP.NET Core, Autofac, Serilog – gói đã có): `Program.cs` (cấu hình = `appsettings.shared*.json` link từ WebApi + `appsettings.json` riêng + biến môi trường; `--list`, `--run <mã>`), `JobSchedulerService` (mỗi `JobHost:PollSeconds` = 30 giây), `Dockerfile`; docker-compose thêm service `job`.
- **API (khung cũ):** `JobController` `GetAll` / `Run` – `[AdminOnly]`; `Contract/ExpireOverdue` (preview) – quyền Sửa `crm-contract`.
- **Seed:** `DB/seed/SystemUser_seed.sql` – user `system` Id cố định `061877ad-…f467` (không đăng nhập được), thêm cuối `deploy/db/scripts.order`, DB/README mục 14; `appsettings.json` WebApi: `SystemUsers:JobUserId` + mục `Jobs`. **Đã chạy seed trên DB dev 2 lần** (lần 2 "đã có – giữ nguyên"). Không bảng / cột / SP mới.
- **Sửa lỗi có sẵn:** `MaterialIssue` chi tiết – dòng cùng nguyên liệu trả theo hạn lô (FEFO) rồi số lô; trước đây thứ tự phụ thuộc DB → `FullFlowSqlServerTests` chập chờn (gặp 3 / 4 lần chạy cả bộ khi thêm test mới).

**Kiểm tra:** build 0 lỗi; test **258 / 258** (+13: lịch giờ, DI WebApi + DI host Job không có phần web, quyền admin, ContractExpire, JobRunService trên SQL Server – user system, log, khóa chống trùng, job lỗi, mã sai), cả bộ chạy 3 lần liên tiếp xanh; `docs/api-tests/t23-jobs.http` **6 / 6** trên DB dev (chạy thật ContractExpiry – 0 hợp đồng quá hạn); `AppDelicare.Job --list` / `--run ContractExpiry` (ghi log dưới `system`, ngày VN) / vòng hẹn giờ 25 giây với mọi job tắt; `dotnet publish` Job + chạy bản publish môi trường Staging. **Chưa** chạy Docker (máy không có Docker), chưa cài Task Scheduler trên staging.

**PROPOSED D-59 (a)–(h)** – xem DECISIONS. Cần anh: duyệt gói `Microsoft.Extensions.Hosting.WindowsServices` nếu muốn chạy dạng Windows Service (D-59 g).

Bảng cập nhật `DELICARE_API_Catalog.xlsx` (sheet `API_Catalog`):
| STT | Trạng thái (J) | API thực tế | Task (M) |
|---|---|---|---|
| 81 | Đã xong | Job `ContractExpiry` 00:30 + `POST api/Contract/ExpireOverdue` | T23 |
| 121 | Đã xong | Job `LotExpiry` 00:15 (`MaterialLot/ProcessExpired`) | T23 |
| 97 (phần job 20:00) | Đã xong | Job `LockOrders` 20:00 (`Order/LockByMealDate`) | T23 |
| 106 / 107 (phần job 20:30 / 21:00) | Đã xong | Job `Mrp` 20:30 (`MaterialRequirement/Calculate` – tạo đề nghị mua) | T23 |

### BE3.1 – Biểu đồ + sự kiện giao (Catalog 100, 157, 163, 192, 193) · 2026-11-17 · DONE
Nhánh `feat/BE3.1-charts` (worktree `wt-BE31`, từ `release/pilot` @ `8e12696` = sau gộp T23), chưa push. Đợt BE-3 theo CLAUDE.md / ROADMAP (anh chốt D-57; D-58 không tra cứu cấu trúc DB – BE3.1 không trả tên bảng / cột).

**Đã làm (khung cũ, không bảng / cột / SP / seed mới)**
- **100** `OrderReconciliation/GetList` (query from / to theo ngày ăn – mặc định 30 ngày, status, customerId, searchText, page): danh sách đối soát + `chart` Đặt – Giao – Nhận theo ngày ăn (đủ mọi ngày, 0 khi trống, theo đúng bộ lọc) + `statusCounts` (trước lọc trạng thái).
- **157** `MealWasteRecord/GetSummary` (from / to, groupBy = wasteType | dish | shift | date): suất, kg, giá trị = suất × `StandardCost.CostPerServing` (bản hiệu lực theo ngày), `recordsWithoutCost`.
- **163** `TemperatureLog/GetSeries` (storageZoneId, from / to – mặc định 48 giờ, tối đa 31 ngày): ngưỡng khu vực + điểm; > 2 000 lần đo → gộp theo phút (trung bình, min / max, vượt ngưỡng vẫn giữ).
- **192** `DeliveryOrder/GetEvents` (id): timeline sự kiện của đơn + điểm dừng của đơn + cấp chuyến, có số chuyến / biển số / tài xế / thứ tự điểm / người ghi / nhãn loại.
- **193** `DeliveryEvent/GetVehiclePositions` (deliveryDate, routeId?): vị trí theo sự kiện có tọa độ (GAP-F-05) + vị trí mới nhất mỗi chuyến; `DeliveryEvent/RecordPosition` (tài xế ghi, EventType mới `POSITION`, chỉ chuyến Đang xếp / Đang giao).
- Dùng chung: `ChartRules` (khoảng ngày, dòng sự kiện giao), `BusinessRules:Charts` (PROPOSED), `DeliveryCode.EventLabel`.

**Kiểm tra:** build 0 lỗi; test **264 / 264** (+6 `Charts/ChartTests.cs`); `docs/api-tests/be3.1-charts.http` **22 / 22** trên DB dev (SQL Server – số thật: 12 đối soát, biểu đồ 1000 / 1000 / 990 mỗi ngày có đơn; hao hụt 33 suất = 66 000 đ); ghi vị trí thật trên chuyến Đang giao `[DEMO]` rồi đọc lại; gọi không token → 401.

Bảng cập nhật `DELICARE_API_Catalog.xlsx` (sheet `API_Catalog`, cột N = BE-3):
| STT | Trạng thái (J) | API thực tế (K / L) | Task (M) |
|---|---|---|---|
| 100 | Đã xong | `GET api/OrderReconciliation/GetList` (+ chart, statusCounts) | BE3.1 |
| 157 | Đã xong | `GET api/MealWasteRecord/GetSummary` | BE3.1 |
| 163 | Đã xong | `GET api/TemperatureLog/GetSeries` | BE3.1 |
| 192 | Đã xong | `GET api/DeliveryOrder/GetEvents` | BE3.1 |
| 193 | Đã xong | `GET api/DeliveryEvent/GetVehiclePositions` + `POST api/DeliveryEvent/RecordPosition` | BE3.1 |

### BE3.2 – IoT nhiệt độ (Catalog 166) · 2026-11-17 · DONE
Nhánh `feat/BE3.2-iot` (worktree `wt-BE32`, từ `release/pilot` @ `2aeb84a`), chưa push.

**Đã làm (khung cũ, không bảng / cột / SP / seed mới)**
- `IotTemperatureController.Receive` (`[AllowAnonymous]` + `[SkipPermission]` + `[IotApiKey]`): JSON mảng `[{ deviceCode, measuredAt, temperatureC }]` ≤ 500.
- `Security/IotApiKeyFilter`: header `X-Api-Key` so với `Iot:ApiKeys` (băm SHA-256, thời gian hằng; không log khóa – chỉ 8 ký tự băm), trống = tắt (401), giới hạn `RateLimitPerMinute` / khóa (429, IMemoryCache).
- `IotTemperatureService`: thiết bị → khu vực (`Iot:Devices`: StorageZoneId / StorageZoneCode, StorageLocationId, MeasurementType, Enabled), kiểm giờ đo / nhiệt độ, chống trùng (DeviceCode, MeasuredAt) trong DB + trong lô, ngưỡng khu vực → IsWithinRange, ghi `TemperatureLog` SourceChannel `IOT` trong `ICurrentActor.BeginScope(system)`; dòng lỗi → `rejected` + log cảnh báo.
- Cấu hình `Iot` trong appsettings (ApiKeys rỗng – bí mật đặt bằng biến môi trường), `IotOptions` đăng ký trong `AddDelicareCore`.

**Kiểm tra:** build 0 lỗi; test **268 / 268** (+4 `Iot/IotTemperatureTests.cs`: khóa sai / thiếu / token người dùng → 401; lô 12 dòng – 3 ghi, 1 trùng, 8 từ chối đúng lý do; gửi lại không ghi thêm; giờ UTC → VN; system là người ghi; biểu đồ BE3.1 thấy lần đo IoT; lô rỗng / > 500; 429). `docs/api-tests/be3.2-iot.http` **3 / 3** trên DB dev (khóa ngẫu nhiên đặt lúc chạy API, thiết bị `DEMO-SENSOR-01` → khu vực `DEMOB21HKL`): 3 dòng IOT trong `TemperatureLog` (08:00 đạt, 08:05 7,1 °C vượt ngưỡng, 01:10Z → 08:10), CreatedBy = system; không khóa → 401.

Bảng cập nhật `DELICARE_API_Catalog.xlsx`:
| STT | Trạng thái (J) | API thực tế (K / L) | Task (M) |
|---|---|---|---|
| 166 | Đã xong | `POST api/IotTemperature/Receive` (header `X-Api-Key`) | BE3.2 |

### BE3.3 – Chuông cảnh báo, tìm kiếm toàn cục, tab Liên quan (Catalog 16, 17, 18) · 2026-11-17 · DONE
Nhánh `feat/BE3.3-alerts-search` (worktree `wt-BE33`, từ `release/pilot` @ `5aeaf30`), chưa push.

**Đã làm (khung cũ, thêm vào `CommonController` / `CommonService` của BE2.7; không bảng / cột / SP / seed mới)**
- `Common/GetAlerts` (branchId?): 9 mã – `STOCK_BELOW_MIN`, `APPROVAL_PENDING` (dùng lại `ApprovalRequestService.GetMyPending`), `QC_INCOMING_PENDING`, `LOT_ON_HOLD`, `TEMPERATURE_OUT_OF_RANGE` (48 giờ, cả IoT), `COMPLAINT_OVERDUE`, `COMPLAINT_OPEN`, `OUTBOUND_FAILED`, `LOT_EXPIRING` (2 ngày, còn tồn); chỉ count > 0, HIGH trước; quyền Xem màn của từng cảnh báo; lọc chi nhánh + đơn vị con qua kho / khách; cache `IMemoryCache` 60 giây theo (người, chi nhánh).
- `Common/GlobalSearch` (q 2…100 ký tự, sai → 400): màn hình theo `ObjectMenu` được Xem (không dấu), ≤ 10; bản ghi 8 loại danh sách trắng (`CommonRelations.SearchTypes`), ≤ 5 / loại, chỉ loại được Xem; không trả tên bảng / cột (D-58).
- `Common/GetRelated` (resource, id): 9 nguồn, quan hệ viết cứng (`CommonRelations.Related`, theo LOGICAL_RELATIONS.md), mỗi nhóm = tên màn + route + tổng + ≤ 8 dòng mới nhất (nhãn + trạng thái + tên trạng thái); bỏ nhóm không được Xem; không Xem nguồn → 403; resource ngoài danh sách / bản ghi không có → 400.
- File: `Services/Implement/CommonService.Alerts.cs`, `CommonRelations.cs`, `Model/Model/AlertSearchModels.cs`, `Model/Option/AlertRulesOptions.cs` (`BusinessRules:Alerts`), thông báo `ConstantHelper` khối BE3.3, appsettings.

**Kiểm tra:** build 0 lỗi; test **274 / 274** (+6 `Common/AlertsSearchRelatedTests.cs`: danh sách trắng khớp ScreenMap; từng quy tắc cảnh báo + chi nhánh / đơn vị con / dữ liệu không gắn chi nhánh + quyền + cache; duyệt chỉ hộp của mình; tìm kiếm ≥ 2 ký tự, ≤ 5 / loại, chỉ loại được Xem, màn không dấu, không tên bảng; Liên quan ≤ 8 + count, bỏ nhóm, 403 / 400; truy xuất lô → phiếu nhập / phiếu xuất / mẻ). `docs/api-tests/be3.3-alerts-search.http` **31 / 31** trên DB dev (đủ 9 nguồn Liên quan – truy vấn EF dịch được sang SQL Server; DB dev chưa có đơn vị loại BRANCH nên bước lọc chi nhánh lấy 1 đơn vị bất kỳ).
### BE3.4 – KPI (Catalog 221–225) · 2026-11-17 · DONE
Nhánh `feat/BE3.4-kpi` (worktree `wt-BE34`, từ `release/pilot` @ `5aeaf30`), chưa push.

**Kế hoạch / rà soát code cũ:** đã có CRUD sinh mã `KpiMetricDefinition`, `KpiDailySnapshot`, `KpiMonthlySnapshot`, `AnalyticsRefreshLog` (controller + service + SP GetAll), menu `#/kpi-*` trong `ObjectMenu_seed.sql`, ScreenMap → bổ sung vào đúng các file đó; lock + nhật ký của `JobRunService` (T23) tách ra dùng chung; controller mới không gắn bảng `KpiSnapshotController` (như `DashboardController`).

**Đã làm (khung cũ, không bảng / cột / SP mới; 1 seed mới)**
- **221** `KpiMetricDefinitionService` / Controller: Insert / Update nhận `KpiMetricDefinitionCustomRequest` (+ `TargetValue`, `TargetDirection`); đổi công thức / mục tiêu / đơn vị / tổng hợp → phiên bản mới (VersionNo lớn nhất + 1 kể cả bản xóa mềm), đóng `EffectiveTo` bản cũ, không sửa bản cũ; chỉ sửa / xóa bản mới nhất; xóa khi chưa có số KPI (bản trước mở lại); UNIQUE (MetricCode, VersionNo) kiểm trong Service. Mục tiêu = thẻ `[TARGET >= x]` cuối FormulaDescription (GAP-KPI-01). `GetAll` trả `KpiMetricDefinitionCustomViewModel` (formula, targetValue, targetDirection, isCurrent); `GetById` trả `KpiDefinitionModel`.
- Seed `DB/seed/KpiMetricDefinition_seed.sql` (11 chỉ tiêu v1, PROPOSED; add-only theo MetricCode) + dòng cuối `deploy/db/scripts.order` + dòng 15 `DB/README.md`.
- **224** `KpiCalculationService.Refresh` (`KpiSnapshot/Refresh`, `[AdminOnly]`): KPI_DAILY (date) / KPI_MONTHLY (month yyyy-MM), branchId?; khóa `sp_getapplock` `DELICARE_JOB_<jobCode>` + `AnalyticsRefreshLog` Running → Succeeded / Failed / Locked (RowsProcessed, SourceWatermark `MANUAL|yyyy-MM-dd[|branch=…]`); `Calculate` upsert theo (ngày / tháng, chi nhánh, mã, phiên bản) – dùng lại cả dòng xóa mềm; INCOMPLETE khi mẫu số 0 / thiếu một phần dữ liệu / chưa có công thức. 11 công thức (D-57 y) trên ProductionBatch / ProductionOutput / MealWasteRecord / TemperatureLog CORE / FoodSafetyChecklistResult SAMPLE_RETAINED / DeliveryOrder / StockCount(Line) + MaterialLot.UnitCost / MealCost / PackagingLedger / CustomerComplaint.
- `Services/Jobs/AnalyticsRunGate.cs`: khóa + nhật ký tách từ `JobRunService` (SQL Server: kết nối riêng; InMemory: khóa trong tiến trình + EF); `JobRunService` dùng lại, hành vi T23 giữ nguyên (sửa thêm: ErrorMessage cắt 2000 = độ dài cột, trước là 4000).
- `Services/Jobs/KpiJobs.cs`: job `KPI_DAILY` 23:30 / `KPI_MONTHLY` 23:45 gọi cùng `Calculate`; `Jobs:KPI_DAILY / KPI_MONTHLY` **Enabled = false** (D-45, D-57 aa).
- **222 / 223** `KpiSnapshotService.GetDaily / GetMonthly` → `{ period, from, to, branchId, domainCode, metrics: [{ definition, latest: { value, target, variance, met, … }, series: [{ date, value, target, met }] }] }`; `met` theo chiều mục tiêu (LOWER: WASTE / REJECT / EXPIRY / COST / LOSS). Quyền Xem `#/kpi-daily`, `#/kpi-monthly` gắn rõ trên action.
- **225** `AnalyticsRefreshLogService.GetList` (`AnalyticsRefreshLog/GetList`): jobCode, status, from / to theo StartedAt, phân trang, trigger / ngày nghiệp vụ tách từ watermark, tên người chạy.
- `BusinessRules:Kpi` (OnTimeToleranceMinutes 15, khoảng xem mặc định / tối đa), `ConstantHelper.Message` khối `// BE3.4`, `KpiCode`.

**Kiểm tra:** build 0 lỗi; test **275 / 275** (+7: `Kpi/KpiTests.cs` – phiên bản, 11 công thức trên dữ liệu mẫu + chiều "đạt" + chi nhánh + KPI tháng, upsert chạy lại + dòng xóa mềm + nhật ký / GetList, ngày không dữ liệu → INCOMPLETE + mã chưa có công thức, kiểm tra đầu vào, chỉ admin làm mới / quyền xem; `KpiRefreshSqlServerTests` trên DB dev: khóa sp_getapplock → 400 + dòng Locked, nhật ký Succeeded, upsert không nhân đôi, dọn đúng dòng của test; `JobTests` cập nhật danh sách 7 job). `docs/api-tests/be3.4-kpi.http` **33 / 33** trên DB dev (cổng 5126; chạy lại lần 2 vẫn 33 / 33). `t23-jobs.http`: kỳ vọng 7 job. Seed chạy 2 lần trên DB dev: lần 1 thêm 11, lần 2 thêm 0. Số KPI tháng 11/2026 trên DB dev: CS_COMPLAINT_SLA 64 %, INV_ACCURACY 95 %, PKG_LOSS_RATE 0,11 % (OK); các chỉ tiêu khác INCOMPLETE vì DB dev chưa có mẻ / đơn giao / giá vốn suất trong tháng 11 tới ngày 17.

**Còn lại / cần anh:** duyệt D-57 w–ac (mục tiêu trong FormulaDescription – GAP-KPI-01, công thức 11 chỉ tiêu, chỉ admin làm mới, job KPI đang tắt); deploy: chạy `DB/seed/KpiMetricDefinition_seed.sql` (đã có trong `scripts.order`), thêm `BusinessRules:Kpi` + `Jobs:KPI_*` vào appsettings của môi trường (có mặc định trong code).

Bảng cập nhật `DELICARE_API_Catalog.xlsx`:
| STT | Trạng thái (J) | API thực tế (K / L) | Task (M) |
|---|---|---|---|
| 16 | Đã xong | `GET api/Common/GetRelated?resource=&id=` | BE3.3 |
| 17 | Đã xong | `GET api/Common/GlobalSearch?q=` | BE3.3 |
| 18 | Đã xong | `GET api/Common/GetAlerts?branchId=` | BE3.3 |
| 221 | Đã xong | `KpiMetricDefinition/GetAll / GetById / Insert / Update / Delete` (+ phiên bản, mục tiêu) | BE3.4 |
| 222 | Đã xong | `GET api/KpiSnapshot/GetDaily` | BE3.4 |
| 223 | Đã xong | `GET api/KpiSnapshot/GetMonthly` | BE3.4 |
| 224 | Đã xong | `POST api/KpiSnapshot/Refresh` (chỉ admin; INVENTORY_BALANCE không làm) | BE3.4 |
| 225 | Đã xong | `GET api/AnalyticsRefreshLog/GetList` | BE3.4 |

### BE3.5 – Dashboard điều hành + hành trình đơn hàng (Catalog 231, 232) · 2026-11-17 · DONE
Nhánh `feat/BE3.5-dashboard` (worktree `wt-BE35`, từ `release/pilot` @ `af24e5c`), chưa push.

**Kế hoạch / rà soát code cũ:** đã có `DashboardController` (rỗng, ControllerScreens → `dash-exec`) + `DashboardService` / `IDashboardService` mẫu cũ (chưa dùng) → bổ sung vào đúng 2 file này, không tạo controller / service mới. Dùng lại: `CommonService.GetAlerts` (khối cảnh báo), quy tắc tồn / lô / phản ánh của chuông (tách thành `GetSharedCounts`), trạng thái thực hiện giao của `DeliveryExecution/GetBoard` (tách thành `DeliveryRules.ExecutionStatus`), công thức tiến độ của `ProductionBatch/GetBoard`. Mục menu `#/flow-e2e` chưa có → không seed (D-57 ai).

**Đã làm (khung cũ, không bảng / cột / SP / seed mới)**
- `Dashboard/GetExecutive` (date?, shiftId?, branchId?): `kpi` (plannedMeals, orders {total, byStatus, listStatus}, productionProgress, delivery {delivered, total, byStatus}, stockAlerts, qcHoldReject + qcDetail, staffing, openComplaints), `chart.meals7d`, `shiftProgress`, `kpiSnapshot` (4 chỉ tiêu từ KpiDailySnapshot, met theo chiều chỉ tiêu), `orders` (6 đơn đầu + trạng thái giao), `ordersTotal`, `alerts` (= Common/GetAlerts của người đăng nhập). Cache 60 giây phần chung theo (ngày, ca, chi nhánh); ca / chi nhánh không có, ngày sai → 400. Quyền Xem `#/dash-exec`.
- `Dashboard/GetOrderJourney` (orderId): 8 chặng ORDER → PRODUCTION_PLAN → PRODUCTION → PACKING → DELIVERY → POD → RECONCILIATION → COMPLAINT, mỗi chặng `{ stage, title, route, status, statusName, at, done, documentCount, canView, note, documents [{ id, number, status, statusName, at, route }] }` + `currentStage`; chỉ màn hình + số chứng từ (D-58). Quyền Xem `flow-e2e` / `dash-exec` / `crm-order` (403); chứng từ theo quyền Xem màn của chứng từ.
- File: `Services/Implement/DashboardService.cs`, `Interface/IDashboardService.cs`, `WebApi/Controllers/DashboardController.cs`, `Model/Model/DashboardExecutiveModels.cs`, `Model/Option/DashboardRulesOptions.cs` (`BusinessRules:Dashboard`), `CommonService.Alerts.cs` (+ `GetSharedCounts`, `GetUnitTree`), `DeliveryRules.ExecutionStatus` (+ `DeliveryExecutionService` dùng lại), thông báo `ConstantHelper` khối BE3.5, appsettings.

**Kiểm tra:** build 0 lỗi; test **279 / 279** (+5 `Dashboard/DashboardTests.cs`: từng khối trên dữ liệu seed + so sánh Order / ProductionBatch / DeliveryExecution GetBoard + chuông; lọc chi nhánh (đơn vị con) / ca; 400; cache 60 giây theo khóa nhưng cảnh báo theo người; quyền dash-exec; hành trình 8 chặng + đơn Nháp / Hủy + quyền chứng từ; chiều KPI). `be3.5-dashboard.http` **13 / 13** trên DB dev (37 @expect, 5 chốt 400). Đối chiếu tay ngày 2026-12-01 / 12-02 trên DB dev: đơn / trạng thái, suất, kế hoạch / đạt / số mẻ, giao, tồn, lô, phản ánh, cảnh báo – **12 / 12 khớp** mỗi ngày; số suất 1 650 / 2 000 phần / 998 suất nhận khớp truy vấn SQL.

Bảng cập nhật `DELICARE_API_Catalog.xlsx`:
| STT | Trạng thái (J) | API thực tế (K / L) | Task (M) |
|---|---|---|---|
| 231 | Đã xong | `GET api/Dashboard/GetExecutive?date=&shiftId=&branchId=` | BE3.5 |
| 232 | Đã xong | `GET api/Dashboard/GetOrderJourney?orderId=` | BE3.5 |

### BE3.7 – Gộp, kiểm tra, deploy đợt BE-3 · 2026-11-17 · DONE (chờ anh duyệt)
- **Gộp** (`--no-ff`, xung đột tài liệu giữ cả 2 bên): T23 (8e12696) → BE3.1 (2aeb84a) → BE3.2 (5aeaf30) → BE3.3 (af24e5c) → BE3.4 (a15322d) → tài liệu D-60 của anh → BE3.5 (4cbabee). BE3.3 / BE3.4 / BE3.5 do agent làm trên worktree riêng, em rà diff (không DDL / `sys.*` / SQL ghép chuỗi / bí mật) rồi gộp, build + test lại sau mỗi lần gộp.
- **Test D-58 mới** `Security/NoDatabaseStructureApiTests`: không controller / action tên kiểu tra cứu cấu trúc; mã nguồn WebApi / Services / Data / Model / Job không đọc `INFORMATION_SCHEMA` / `sys.*`. Lưu ý: `MetaData/GetStatuses` (T45B.5) trả khóa bộ trạng thái dạng `Order.Status` (tên thực thể.cột) – **hỏi anh** có coi là vi phạm D-58 không (FE đang dùng để lấy nhãn trạng thái).
- **Test toàn bộ 288 / 288.**
- **`.http` trên DB dev** (WebApi Development, cổng 5125): `full-flow.http` **217 / 217** (`@key=B37C`, `@day=2026-12-05`, `@dow=6` – ngày trống gần nhất; ngày xa như 2028 làm bước giữ chỗ #111 = 0 vì lô thử hạn hôm nay + 20 / 30 ngày); be2.1 **147 / 147** (2026-11-28, dow 6), be2.2 **73 / 73**, be2.3 **58 / 58**, be2.4 **125 / 125** (2026-11-29, dow 7), be2.5 **142 / 142** (2026-12-06, dow 7), be2.6 **65 / 65**, be2.7 **47 / 47**, t23 **6 / 6**, be3.1 **22 / 22** (sửa bước 5.4 dùng Id chuyến cố định – đơn giao đầu danh sách không còn gắn chuyến), be3.2 **3 / 3** (khóa IoT sinh lúc chạy), be3.3 **31 / 31**, be3.4 **33 / 33**, be3.5 **13 / 13**.
- **Staging** (`AppDelicare_Staging`, cổng 5200): `Deploy-Database.ps1` → 131 SP + seed (thêm `SystemUser_seed` – tạo user system, `KpiMetricDefinition_seed` – 11 chỉ tiêu); publish đè `C:\Deploy\delicare-staging` (giữ Angular cũ trong wwwroot); `full-flow.http` **217 / 217** (`@key=B37S`, `@day=2026-11-28`, `@dow=6`), t23 6 / 6, be3.3 31 / 31, be3.4 33 / 33; be3.1 19 / 22 và be3.5 8 / 13 chỉ sai ở bước `@expect` gắn dữ liệu riêng DB dev (số suất 1650, Id chi nhánh dev, khu vực kho đầu danh sách) – API đều trả 200. Chạy xong đã dừng API staging. Job (`AppDelicare.Job`) chưa chạy trên staging (chờ T48.4 Docker – D-60).
- Bí mật: agent BE3.5 lỡ in `dotnet user-secrets list` (khóa JWT dev + license GemBox) ra nhật ký làm việc – không có trong commit; **đề xuất anh đổi khóa JWT dev / staging** (cùng mật khẩu admin staging đã nêu trước).

### T48.4 – Production Linux + Docker (D-60) · 2026-11-17 · DONE (Docker chưa chạy thật)
Nhánh `feat/T48.4-docker` (worktree `wt-T484`).
- **Compose production** `deploy/docker/docker-compose.production.yml`: `nginx` (Dockerfile 2 giai đoạn build Angular + `default.conf.template`: HTTP → HTTPS, `/api`, `/Import`, `/health` → api, `client_max_body_size` theo `MAX_UPLOAD_MB`, header bảo mật, cache file tĩnh), `api`, `job` (1 bản), `db-init` (profile tools, tài khoản quản trị SQL); không SQL Server; bí mật qua `production.env` (`production.env.example` không giá trị thật; `.gitignore` thêm `production.env`, `certs/`); log `json-file` 20 MB × 10.
- **Giờ SQL**: `DatabaseClockCheck` (Services) – `/health` thêm `dbclock` (Degraded khi lệch > 5 phút, kèm mô tả), Job cảnh báo lúc khởi động. Đã thấy thật: LocalDB máy dev lệch −900 phút (giờ máy UTC−8).
- **Tài khoản SQL ứng dụng**: `deploy/db/app-login.sql` (login + user, db_datareader / db_datawriter / EXECUTE, không DDL, idempotent, mật khẩu hex qua file tạm) + `db-init.sh` (`APP_DB_USER`, `APP_DB_PASSWORD`, `APP_DB_RESET_PASSWORD`). Thử trên LocalDB dev: tạo → chạy lại không đổi → quyền đọc / ghi / EXECUTE = 1, CREATE TABLE / ALTER schema = 0 → đăng nhập SQL được → **đã xóa login thử**. Compose staging dùng tài khoản này khi đặt biến (không đặt = sa như cũ).
- **Job**: file nhịp (`JobHeartbeat`, `JobHost:HeartbeatFile`) ghi mỗi vòng + sau mỗi job; `dotnet AppDelicare.Job.dll --healthcheck` (không dựng host) – Dockerfile Job có HEALTHCHECK. Thử thật: chưa có nhịp → unhealthy / exit 1; chạy vòng hẹn giờ 20 giây → healthy / exit 0.
- **Log**: `SerilogSetup` (WebApi + Job) – `Logging:ConsoleOnly=true` (đặt trong Docker) bỏ sink File, chỉ console.
- **Tài liệu**: `deploy/README.md` (Linux + Docker là chuẩn, mục 10 production; bỏ Task Scheduler / Windows Service), `deploy/docker/README.md` mục 5, DECISIONS "T48.4 – đề xuất" (a)–(e).
- **Kiểm tra**: build 0 lỗi; test **292 / 292** (+4 `Deploy/DeployHealthTests.cs`); `db-init.sh` `bash -n` + dry-run; `/health` thật → database Healthy, dbclock Degraded (máy dev). **Chưa**: `docker compose` (không có Docker), kiểm cú pháp YAML bằng công cụ (lệnh tải gói js-yaml bị chặn) – cần chạy bước 7 (dựng staging Docker trên Linux + full-flow + --list + health) khi có máy Docker.


### T48.5 – Tương thích SQL Server 2022 (D-61) · 2026-11-17 · DONE (bước 4 – kiểm trên SQL Server 2022 thật – BLOCKED: máy chưa có Docker)
Nhánh `feat/T48.5-sql2022` (worktree `wt-T485`).
- **Bước 1 – hạ mức DB dev**: LocalDB máy anh là SQL Server 2025 (17.0.4025.3); `AppDelicare` **170 → 160** (chạy `deploy/db/compat-level.sql` 2 lần: lần 2 "= 160"). Chỉ đổi cấu hình DB, không đụng bảng / dữ liệu. **`AppDelicare_Staging` vẫn 170** – chưa hạ (ROADMAP ghi "nếu anh đồng ý"; xem đề xuất dưới). Sau khi hạ: test **316 / 316**, `full-flow.http` **217 / 217** (`@key=C61D`, `@day=2026-12-03`, `@dow=4`), `Deploy-Database.ps1` chạy trọn trên DB dev (131 SP + seed, mức "= 160").
- **Hiệu năng** (đo cả file `.http`, 3 lần / mức, cùng DB dev): be3.3 cảnh báo / tìm kiếm 813–1481 ms (170) vs 817–1106 ms (160); be3.4 KPI 837–1474 vs 843–1203; be3.5 dashboard 465–894 vs 414–465 – **không API nào chậm rõ rệt**. (be2.6 sai 13 / 65 ở **cả hai** mức vì chạy lại không đổi mã dữ liệu – không liên quan mức tương thích.)
- **Bước 2 – script deploy giữ 160**: `deploy/db/compat-level.sql` (> 160 → `ALTER DATABASE … SET COMPATIBILITY_LEVEL = 160` + log; = 160 giữ; < 160 chỉ cảnh báo) – gọi từ `Deploy-Database.ps1` (ngay sau tạo DB, trước DDL) và `deploy/docker/db-init.sh` (`bash -n` + dry-run đạt). Tài liệu: `deploy/README.md` mục 2 + mục 11 mới, `DB/README.md`.
- **Bước 3 – test quét mã** `AppDelicare.Tests/Schema/SqlServer2022CompatibilityTests.cs` (+24 test): quét `DB/**/*.sql` + `deploy/db/**/*.sql` (bỏ comment `--` / `/* */`, chuỗi `'...'`, tên `[...]`) và chuỗi C# trong `AppDelicare.*/**/*.cs` (bỏ code / comment; `PRODUCT()`, kiểu `json` / `vector`, `||` chỉ xét chuỗi "giống SQL"). Danh sách ngoại lệ rỗng. Kèm test tự kiểm bộ quét (bắt đủ 15 mẫu cấm, không báo nhầm comment / dữ liệu / tên bảng `Product` / `JSON_VALUE` / `JSON_OBJECT` / `@json`). Thử cài file `DB/sp/zz_tmp_check.sql` có `REGEXP_LIKE` → test đỏ đúng file → đã xóa. Mã hiện tại: **không vi phạm**.
- **Bước 4 – kiểm trên SQL Server 2022 thật: BLOCKED** (máy dev không có Docker). Lệnh đã ghi `deploy/README.md` mục 11; CLAUDE.md mục 6 bước 6 thêm "bắt buộc trước mỗi lần gộp `main`". Cần chạy khi có máy Docker (cùng bước 7 của T48.4).
- **Đề xuất (D-49)** – **Vấn đề:** `AppDelicare_Staging` trên máy anh còn mức 170, lần `Deploy-Database.ps1` tới sẽ tự hạ về 160. **Đề xuất (em chọn):** hạ luôn staging về 160 ở lần cập nhật staging kế tiếp (chạy `Deploy-Database.ps1` như mục 9) – để staging giống server test / production; không đụng dữ liệu. **Phương án khác:** giữ 170 cho staging (không có lợi gì; lệch chuẩn D-61).
- **2026-11-17 – anh đồng ý:** chạy `Deploy-Database.ps1` lên `AppDelicare_Staging` → **170 → 160** (131 SP + seed, không đụng bảng); `full-flow.http` trên staging (cổng 5200, bản publish hiện có) **217 / 217** (`@key=C61S`, `@day=2026-12-03`, `@dow=4`); đã dừng API staging. Push `release/pilot`; **chưa gộp `main`** – theo CLAUDE.md mục 6 bước 6 (D-61) phải kiểm trên SQL Server 2022 thật trước, đang chờ máy Docker.


### REVIEW-BE-1 – Rà soát backend (anh chọn 2026-11-17) · DONE
Nhánh `fix/REVIEW-BE-1` (worktree `wt-REVIEW`). 3 agent rà chỉ đọc (bảo mật / đúng nghiệp vụ – đồng thời / hiệu năng); em kiểm lại từng lỗi trong code rồi mới sửa. Không đổi bảng / cột (khóa dùng `sp_getapplock`, không rowversion).
- **Bảo mật (nặng):**
  - `User/Insert`, `User/Update` **không nhận `IsAdmin` từ form** (trước đây người có quyền Sửa màn adm-user tự nâng thành admin; sửa thường còn vô tình tước admin) – chỉ `User/SaveIsAdmin` (AdminOnly).
  - Upload ảnh hồ sơ (`ImportService`): chỉ PNG / JPEG thật (kiểm byte đầu tệp), ≤ 5 MB, tên trên đĩa = Guid + đuôi (không dùng tên client → hết "../" và `.html` → hết XSS lưu trữ trên cùng domain), loại thư mục lạ → không ghi. Nhập Excel cũ: tên tệp tạm = Guid, xóa tệp sau khi nhập.
  - `Person/UpdateProfile`: chỉ sửa hồ sơ **của chính người đăng nhập** (Person gắn User hiện tại), thêm commit transaction (trước đây thiếu → thay đổi bị rollback, chỉ tệp được ghi).
  - `User/ChangePassword`: chỉ đổi mật khẩu **của chính mình** (Id khác → 400; trước đây dò mật khẩu user khác không giới hạn). Admin đổi cho người khác dùng `ResetPassword`.
  - `Authentication/Login`: sai ≥ 10 lần / tên đăng nhập hoặc ≥ 50 lần / IP trong 15 phút → **429** tới hết cửa sổ; đúng → xóa bộ đếm của tên đó (bộ nhớ tiến trình, không bảng / gói mới).
  - nginx production: bỏ `add_header` trong `location` (nginx bỏ hết header cấp server → trang Angular mất HSTS / X-Frame-Options / nosniff), dùng `expires`. Swagger tắt khi `Production` (bật lại: `Swagger:Enabled=true`).
- **Đồng thời (nặng):** `DbLocks` (`sp_getapplock`, giữ tới hết transaction, bỏ qua khi InMemory):
  - ghi sổ kho `InventoryLedgerService` khóa theo dòng tồn (kho, vị trí, NL, lô) trước khi đọc → không mất số / âm tồn / 2 dòng tồn; đảo bút toán khóa theo bút toán gốc;
  - khóa theo chứng từ: `MaterialIssue` Post / Cancel, `GoodsReceipt` Post / Cancel, `GoodsReceipt/Create` theo PO (kiểm nhập vượt), `ApprovalRequest` Decide / Cancel (không áp điều chỉnh tồn 2 lần).
  - Test SQL Server thật: A giữ khóa dòng tồn → bút toán B phải chờ; **đã thử gỡ khóa → test đỏ**, trả khóa → xanh.
- **ATTP:** giờ đo nhiệt độ nhập tay (lõi mẻ, xuất phát / bàn giao) chỉ nhận trong 24 giờ trước → 5 phút sau giờ hiện tại, giờ UTC đổi sang giờ VN (`TemperatureTime`) – trước đây 1 lần đo ghi giờ tương lai "che" lần đo thật, cổng ≥ 70 / 60 °C bị vượt.
- **Khác:** `DateTime.Now / Today` → `IClock` (ledger, PricePolicy, UomConversion, Customer / Supplier overview); `MealCost/GetList` không lọc đơn → bắt buộc khoảng ngày (mặc định 31 ngày, tối đa 92); Dapper `QueryMultipleUsingStoreProcAsync` mở / đọc bất đồng bộ; 7 SP `GetAll` (AuditChange, User, UserClaim, UserGroup, Person, ObjectMenu, LocationTracking) chặn `PageSize` ≤ 1000, `Page` ≥ 1 – đã chạy lên DB dev.
- **Kiểm tra:** build 0 lỗi; test **321 / 321** (+5: `Security/BackendReviewFixTests.cs` – IsAdmin, upload, giờ đo, chặn đăng nhập; `LedgerSqlServerTests` khóa song song; sửa 2 test đổi mật khẩu dùng token của chính user); `full-flow.http` **217 / 217** (`@key=RV5D`, `@day=2026-11-20`, `@dow=5`), be3.3 31 / 31, be3.4 33 / 33, be2.7 47 / 47, t23 6 / 6. Ghi chú: các ngày 23/11 → 10/12 trên DB dev đã có kế hoạch SX (lần chạy trước) – chạy lại full-flow phải chọn ngày trống (còn 21–22/11).
- **Chưa sửa – đề xuất** (DECISIONS "Rà soát BE – đề xuất"): 117 API `GetAllData` trả cả bảng; truy vấn lặp (quy đổi ĐVT, hộp duyệt, mẫu đơn, đối soát); phân trang trong bộ nhớ (3 màn); giới hạn nhập Excel; job lỗi chạy lại / chạy bù; `RebuildBalances` gom trên server; xóa code mẫu `Patient*`; thu hồi token khi khóa / reset.

### Gộp release/pilot → main (anh yêu cầu 2026-11-17)
- Gộp T48.5 + REVIEW-BE-1 + docs kiểm lược đồ vào `main` (main đã có PR #1 GHEP-FE – gộp không xung đột, không đụng `app-delicare-client/`). Build 0 lỗi, test **321 / 321** trên bản gộp, gồm `SqlServer2022CompatibilityTests` xanh.
- **Chưa kiểm trên SQL Server 2022 thật** (D-61, deploy/README mục 11) – máy chưa có Docker; anh đồng ý gộp trước. Cần chạy khi có máy Docker / Linux: DB trống → `db-init` 2 lần → `full-flow.http`.
- Không bảng / cột mới; server test chỉ cần chạy `db-init` (7 SP) + build lại API (DB/README).

### BOT-1.1 – Lõi kênh tin nhắn (api) · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-1.1-channel-core`, **chưa push / gộp – chờ anh duyệt**)
- **Seed** `DB/seed/Integration_Telegram_seed.sql` (chỉ thêm, đã vào `deploy/db/scripts.order`): 3 `IntegrationChannel` (`TELEGRAM_VH`, `TELEGRAM_DH`, `TELEGRAM_TRO_LY` – Trợ lý Inactive), 3 `BotConfiguration` (`SecretReference` = tên biến môi trường `Telegram__Bots__<BOT>__Token`, `WebhookPath` `/tg/<BOT>`), menu quyền `#/bot-van-hanh`, `#/bot-dieu-hanh` (nhóm 09, IsActive = 1 vì PermissionService bỏ qua menu tắt) + UserRole PROPOSED (VH: 16 nhóm nghiệp vụ; DH: CEO, CEO-PMO, COO, FIN-ACC – D-62 p15). Đã chạy DB dev 2 lần: 3 kênh, 3 bot, 20 quyền, lần 2 không thêm.
- **Khóa client** `Security/ClientKeyFilter` (`X-Client-Key`, SHA-256 hex = `Clients:<code>:KeyHash`, so thời gian hằng; trống / `Enabled = false` → 401). **`BotChannelController`** (nội bộ, `[AllowAnonymous][SkipPermission][ClientKey(MESSAGING)]`, body JSON như IoT): `ReceiveUpdate`, `ClaimInbound`, `CompleteInbound`, `ClaimOutbound`, `MarkOutbound`. nginx production: `location ~* ^/api/BotChannel(/|$) { return 404; }`.
- **`Services/Implement/Messaging`**: `BotChannelService` (chống trùng `TG:{bot}:{update_id}` – đọc `UPDLOCK, HOLDLOCK` trong transaction; claim `READPAST, UPDLOCK, ROWLOCK` trả dữ liệu thẳng từ `OUTPUT` (không đọc lại bảng – lần đọc lại chạm dòng người khác đang khóa); giữ quá `Messaging:LeaseMinutes` = 5′ → lấy lại; outbound tôn trọng `PayloadJson.notBefore` (GAP-INT-03) và thử lại 1′ / 5′ / 15′ / 60′ / 240′ – giờ thử kế ghi vào `QueuedAt` (D-62 p16); Blocked → định danh Blocked; `IntegrationLog` IN / OUT; ghi dưới tài khoản `system`), `NotificationService.EnqueueAsync` (outbox trong transaction của người gọi, `PayloadJson = {template, data, buttons, scope, level, notBefore}`, chống trùng `IdempotencyKey:IdentityId`, người chưa liên kết → bỏ qua), `NotificationCatalog` (khung + 6 mẫu đầu, bot theo phạm vi), `RecipientResolver` (định danh Active theo bot, fallback bot người đó có; người có quyền menu), `MessagingRules` (giãn cách, che JSON).
- **API web**: `InboundMessage/Reprocess` (216), `OutboundMessage/Enqueue` (217), `Retry` (218), `GetConversation` (219). `IntegrationChannel` GetAll / GetById / GetAllData **che** khóa secret / token / key / password trong `ConfigJson` (`***`); Update giữ bí mật cũ khi form gửi lại `***`. `IntegrationLog/Insert` bỏ (chỉ đọc). Controller / service đã sửa: bỏ header `<auto-generated>` (CodeGen không ghi đè file viết tay).
- **Cấu hình**: appsettings `Messaging` (PROPOSED) + `Clients:MESSAGING` (KeyHash trống – bí mật ở env); compose production `Clients__MESSAGING__KeyHash: ${MESSAGING_CLIENT_KEY_HASH}` + `production.env.example`. `RetryDelaysMinutes` khởi tạo rỗng, mặc định trong code (binder .NET NỐI mảng appsettings vào danh sách khởi tạo → 10 phần tử). `DB/proposed/GAP_INT_02_idempotency_index.sql` (PROPOSED, không tự chạy, không trong scripts.order).
- **Kiểm tra**: build 0 lỗi; test **331 / 331** (+10: `Notifications/BotChannelTests` 7 – khóa client, trùng update, claim / complete / reprocess, outbox + thử lại + Blocked + hội thoại, hết lượt + notBefore, che ConfigJson, log chỉ đọc; `Notifications/BotChannelSqlServerTests` 3 trên DB dev – claim song song không trùng dòng, update trùng gửi cùng lúc chỉ 1 dòng (B chờ khóa của A), rollback nghiệp vụ → không có OutboundMessage); `docs/api-tests/bot-1.1-channel.http` **21 / 21** (2 lần liên tiếp, tự dọn định danh thử); khóa thiếu / sai → HTTP 401 trên API thật. Ghi chú: `CommonApiTests.History_merges…` đỏ 1 lần rồi xanh 3 lần chạy lại (không liên quan BOT-1.1 – theo dõi).
- **Còn lại**: liên kết / token (BOT-1.2), container `messaging` (BOT-1.3). Catalog xlsx cột J–M dòng 214–219 **chưa cập nhật** – máy chưa có openpyxl, lệnh cài bị chặn (cần anh chạy `py -m pip install --user openpyxl` hoặc cho phép).

### BOT-1.2 – Định danh, liên kết, token · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-1.2-identity-link` tách từ BOT-1.1, **chưa push / gộp – chờ anh duyệt**)
- **`Services/Implement/Identity`**: `LinkCodeSigner` (21 byte loại U / E + Id + hạn, HMAC-SHA256 16 byte bằng `Messaging:LinkSigningKey`, base64url 50 ký tự ≤ 64 cho `/start`; sửa chữ ký / hết hạn → từ chối; 1 lần = IntegrationLog LINK theo băm mã), `PhoneMatcher` (chuẩn hóa +84 / 84 / 0 / 0084 / mất số 0; khớp DUY NHẤT ≤ 1 `Employee.Phone` + ≤ 1 `Person.PhoneNumber` → User – Employee và User không có cột nối, cùng số = cùng người, định danh ghi cả hai), `TelegramLinkService`, `UserTokenIssuer` (JWT qua `JwtFactoryService` cũ + overload hạn riêng: 10′, claim `client = MESSAGING`; kiểm định danh dùng được + có UserId + User `IsActive` + quyền vào bot `#/bot-van-hanh` / `#/bot-dieu-hanh`; mỗi lần cấp / từ chối 1 dòng IntegrationLog TOKEN), `BotEmployeeService`.
- **Người có quyền duyệt (mục A) hoặc admin** khớp số → `PendingCode` (PROPOSED D-62 p17): token có `bot_limited = 1` → `PermissionService` (theo claim của chính request) bỏ quyền admin + mọi mục `#/wf-inbox?type=…` → API duyệt trả 403; nhập mã web → `Active`, token đầy đủ. Đã xác nhận mã (log LINK_CODE / LINK_ADMIN) → chặn / mở bot lại vẫn Active.
- **BotChannel** (khóa client): `ResolveIdentity`, `LinkByPhone` (contactUserId = externalUserId), `LinkByCode`, `TouchIdentity` (LastSeenAt; Blocked / mở lại), `IssueUserToken`, `EmployeeGetMyShifts` / `EmployeeGetToday` / `EmployeeCheckIn` / `EmployeeCheckOut` / `EmployeeGetMyRequests` (EmployeeId lấy từ định danh; chấm công qua `AttendanceEventService.CheckForEmployee` – cùng quy tắc tự chấm, ghi dưới system, Note "Nguồn: Telegram – <tên>"). Mỗi người 1 Telegram / bot: liên kết tài khoản Telegram mới → định danh cũ của người đó trên bot đó `Revoked` (D-62 p18).
- **Web**: `TelegramLinkController` – `CreateCode` (chính mình, 15′, deep link `t.me/<BotUsername>?start=<mã>`), `CreateCodeForEmployee` (Sửa `#/adm-employee`, 72 giờ), `GetMine`, `Unlink`; `UserExternalIdentity/Link`, `Unlink` (AdminOnly, Catalog 220). `HrRules.ResolveEmployeeAsync`, `RecipientResolver` coi `PendingCode` như Active (nhận tin, nối nhân viên).
- **Cấu hình**: `Messaging:LinkSigningKey` (BÍ MẬT, env `Messaging__LinkSigningKey` ≥ 32 ký tự; compose production `${MESSAGING_LINK_SIGNING_KEY}` + env mẫu), `UserCodeMinutes` 15, `EmployeeCodeHours` 72, `UserTokenMinutes` 10. Không bảng / cột / seed mới. `ClientCredentialService` (ARCHITECTURE §5.3) chưa tách riêng – `ClientKeyFilter` đã làm việc đó; tách khi có khách thứ 2 (HERMES).
- **Kiểm tra**: build 0 lỗi; test **342 / 342** (+11 `Notifications/BotIdentityTests`: mã round-trip / sửa / hết hạn / khóa khác, chuẩn hóa số 7 ca, khớp số 0 / nhiều / không phải của mình, nhân viên chỉ EmployeeId (không JWT, API hẹp), người duyệt chỉ khớp số → token hạn chế → ProductionPlan/Approve 403 → nhập mã → đầy đủ, dùng lại mã, chặn / mở bot, User bị khóa → không token + log Failed, admin Link / Unlink (non-admin 403), HR cấp mã nhân viên (thiếu quyền 403)); `docs/api-tests/bot-1.2-identity.http` **23 / 23** (2 lần) + `bot-1.1-channel.http` 21 / 21 trên DB dev. DB dev còn 4 định danh thử trạng thái Revoked (đã gỡ ở cuối file .http).
- **Còn lại**: container `messaging` (BOT-1.3). Catalog xlsx dòng 214–220 chưa cập nhật (chưa có openpyxl).

### BOT-1.3 – Container `messaging` (khung) · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-1.3-messaging-host`)
- **Project `AppDelicare.Messaging`** (thêm vào `.sln`) đúng cây BOT_SOURCE_ARCHITECTURE §5.1 (phần khung): **không ProjectReference, không PackageReference, không chuỗi kết nối DB** (test kiểm). `Program.cs` (`MessagingProgram` – tránh trùng tên `Program` của WebApi trong test): `POST /tg/{botCode}`, `GET /health` (không lộ token / secret), `--healthcheck`. Logging = console sẵn có của .NET (không thêm Serilog – không gói mới).
- `Options/` (MessagingHostOptions, TelegramOptions, SendingOptions); `Channels/` (IChannelSender, ChannelSendResult; Telegram: TelegramClient – sendMessage / edit / pin / answerCallback / chatAction / setWebhook / deleteWebhook / setMyCommands / getUpdates (JSON thô) / getFile, TelegramSender (429 → RetryAfter, 403 → Blocked), TelegramUpdateParser (lệnh, @bot trong nhóm, liên hệ, vị trí kể cả trực tiếp, nút, ảnh, tệp, my_chat_member), TelegramSecretValidator (thời gian hằng), TelegramWebhookEndpoint (bot chưa cấu hình 404, secret sai 401 không ghi gì, api tắt 503, update hỏng 200), TelegramPollingService (Mode = Polling, giữ offset khi api tắt), TelegramStartupService (setMyCommands; Webhook: setWebhook + secret), TelegramFormatter (chỉ thoát &amp; &lt; &gt; – HtmlEncode làm chữ Việt thành &amp;#…; – cắt 3.800 ký tự)); `Core/` (DelicareApiClient – BotChannel JSON + X-Client-Key, API nghiệp vụ bằng JWT user; ApiUnavailableException; BotChannelApi; UserTokenCache; BotUserContext; Contracts – DTO tự khai, test so tên + kiểu với Model); `Processing/` (UpdateIngestor dùng chung webhook / polling, InboundPump – xử lý ngay + quét 5″, api tắt giữa chừng → giữ Processing; UpdateDispatcher; OutboundPump + SendRateLimiter – 25 tin / giây / bot, 1 tin / giây / chat, 429 → tạm dừng bot, tin THƯỜNG im lặng / KHẨN có chuông); `Presentation/MessageTemplates` (mẫu trung tính, mẫu lạ → "Có thông báo mới"); `Experience/KeyboardBuilder` (3 nút cố định, nút chia sẻ số); `Audiences/Internal/Chung/` OnboardingActions (`/start`, `/start CHAMCONG`, `/start <mã>` hoặc gõ mã, chia sẻ số – chỉ số của chính mình, người duyệt → nhắc mã web, nút mở bot DH, `/huylienket`), HelpActions (`/help`, `/menu`, ⚡ Ca của tôi bằng JWT user / API nhân viên), BotReply.
- **api**: thêm `BotChannel/UnlinkIdentity` (`/huylienket` – cả người chỉ có EmployeeId).
- **Triển khai**: `AppDelicare.Messaging/Dockerfile` (HEALTHCHECK `--healthcheck`); service `messaging` trong `deploy/docker/docker-compose.production.yml` (Webhook), `docker-compose.staging.yml` và `docker-compose.yml` gốc (Polling mặc định) + biến khóa ở api (staging / gốc); nginx production `/tg/` → messaging (resolver Docker + biến – nginx không chết khi messaging tắt); `production.env.example`, `staging.env.example`, `.env.example` thêm biến placeholder; `deploy/README.md` mục 12. `tools/bot-smoke/` (Telegram giả + kịch bản).
- **Kiểm tra**: build 0 lỗi; test **364 / 364** (+22 `Tests/Messaging/MessagingHostTests`: không ProjectReference / gói / chuỗi DB, 14 cặp DTO khớp Model, parser, formatter + mẫu tin, webhook 401 / 404 / 200 / 503 + /health không lộ bí mật, đăng ký đủ luồng, InboundPump quét lại sau khởi động + Ignored / Failed, OutboundPump Sent / Blocked / 429 tạm dừng, giới hạn 1 tin / giây / chat). Test thật trên DB dev bằng Telegram giả + messaging Polling + api: QR cổng → nút số → số người khác bị từ chối → số trùng 4 hồ sơ bị từ chối → admin khớp số → PendingCode, nhắc mã → ⚡ Ca của tôi (JWT bot hạn chế) → web Enqueue → bot gửi (im lặng) → hội thoại IN Processed / OUT Sent → /huylienket: **ĐẠT**; tắt api giữa chừng → update chưa mất, khởi động lại api + messaging → được xử lý; 11 update cũ lấy lại → api báo trùng, không xử lý lại. DB dev có thêm vài dòng InboundMessage / OutboundMessage / IntegrationLog thử (định danh thử đã gỡ).
- **Chưa làm được trên máy này**: Docker (build image / compose thật) – máy chưa có Docker; polling với **bot DEV thật** – cần anh tạo bot qua @BotFather và đặt token vào user-secrets (deploy/README mục 12). Catalog xlsx chưa cập nhật (openpyxl).

### BOT-1.4 – Khung trải nghiệm + duyệt · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-1.4-experience-approval`)
- **messaging** – `Audiences/Internal/BotAction` + `BotActionRegistry` (chức năng theo bot / phân hệ / mục ObjectMenu / điều kiện; provider theo nhóm – `Chung/ChungActionProvider`: ⚡ Ca của tôi, 📍 Chấm công `/chamcong`, 📝 Đơn của tôi `/donct`, ⏳ Chờ tôi duyệt `/cho` (chỉ khi có mục Duyệt được cấp rõ), ❓ Trợ giúp). `Experience/`: `KeyboardBuilder` (≤ 6 chức năng → bàn phím phẳng; nhiều hơn / admin → 3 nút cố định [⚡ Ca của tôi] [📂 Chức năng] [📍 Chấm công – xin vị trí]), `MenuNavigator` (phân hệ → chức năng bằng SỬA CÙNG TIN, ⬅️; ≤ 2 lần bấm; bấm chức năng kiểm lại quyền), `ContextKeyboard` (bàn phím theo việc + ⬅️ Thoát), `CodeSearch` (tiền tố mã đang sinh trong DocumentNumberService – PH, XN, PA, PO, DN, NL, ME, CX, KH, VS, HD – hoặc chữ ≥ 3 ký tự → `Common/GlobalSearch` bằng JWT user), `CommandScopeSync` (`setMyCommands` scope chat, chỉ lệnh được phép, gọi khi danh sách đổi), `InputWidgets` (nút số, bước ±, checklist 1 tin), `TaskCard` (sửa đồng loạt thẻ cùng `CorrelationId`). `Callback/CallbackData` (`loại:hành động:id` ≤ 64 byte, Guid N). `Conversations/ReplyContext` (ForceReply – bộ nhớ, 30′). `Processing/UndoWindow` (`Sending:UndoSeconds` 10″; bấm lặp → 1 việc; hoàn tác → không gọi api).
- **Duyệt theo mức rủi ro** (`Chung/ApprovalActions`, BOT_TELEGRAM §4, `Messaging:ApprovalRiskLow` – mặc định LEAVE_REQUEST, SHIFT_SWAP, MENU_CHANGE; còn lại = Cao): danh sách (`GetMyPending?explicitOnly=true`), chi tiết (`GetDetail`), thấp = 1 chạm → "Đang duyệt… [↩️ Hoàn tác]" → `Decide`, duyệt hàng loạt có xem trước; cao = xác nhận 2 bước; từ chối = nút + lý do bắt buộc (ForceReply). Bấm 2 lần → 1 quyết định (UndoWindow + khóa trong tiến trình + api khóa sp_getapplock / bước đã quyết). Xong → sửa thẻ mình + thẻ cùng sự việc `APR:<Id>` ở người khác. api 401 / 403 (token hạn chế PendingCode / thiếu quyền) → bot báo rõ cách lấy mã web.
- **Chấm công bằng vị trí** (`Chung/SelfServiceActions` – kéo từ BOT-1.5 vì nút cố định 📍 cần chạy): chặn vị trí chuyển tiếp (`forward_origin`) và vị trí cũ > 120″; vào / ra theo `AllowedActions` (web: `AttendanceEvent/GetMine` → `CheckIn|CheckOut` bằng JWT user, SourceChannel GPS, ExternalEventId `TG:<bot>:<update_id>`; chỉ EmployeeId: `BotChannel/EmployeeCheckIn|Out`). 📝 Đơn của tôi: `LeaveRequest/GetMine` + `ShiftSwapRequest/GetMine` / `EmployeeGetMyRequests`.
- **api**: `ApprovalRequest/GetMyPending?explicitOnly=true` (bỏ quyền admin – chỉ bước được cấp rõ; web giữ nguyên mặc định); `BotChannel/ResolveIdentity` trả thêm `menus`, `approveScreens` (PendingCode → rỗng), `isAdmin`; `BotChannel/GetCorrelated` (tin Sent có message_id cùng CorrelationId).
- **Kiểm tra**: build 0 lỗi; test **376 / 376** (+12: `Tests/Messaging/MessagingExperienceTests` 10 – CallbackData, admin 3 nút + mọi chức năng 2 lần bấm, ≤ 6 phẳng, lệnh "/" theo chat + gọi 1 lần, menu sửa cùng tin + kiểm lại quyền, duyệt thấp 1 chạm + bấm 2 lần = 1 quyết định + thẻ người khác đổi trạng thái + hoàn tác không gọi api, cao 2 bước, từ chối + lý do qua reply, 403 báo rõ, vị trí chuyển tiếp / cũ / vào ca nhân viên / ra ca web, gõ mã tra cứu, InputWidgets; `ApprovalTests.Explicit_only…`; `BotChannelTests.Correlated…`). Test BOT-1.3 chuyển sang dựng bằng DI thật (`MessagingTestHost`). Sửa test lúc đỏ lúc xanh `CommonApiTests.History_merges…`: bí mật mẫu "abc" trùng chuỗi hex trong Guid ngẫu nhiên → đổi chuỗi không phải hex. Luồng thật DB dev (`tools/bot-smoke/flow-experience.mjs`, Telegram giả): admin /start <mã web> → bàn phím phẳng 4 nút → /menu → /cho ẩn (admin không có mục Duyệt cấp rõ – không ngập) → vị trí chuyển tiếp / cũ bị chặn → gõ mã → /huylienket: **ĐẠT**, không ghi quyết định duyệt nào.
- **Còn lại / ghi chú**: thẻ duyệt chủ động (outbox `APPROVAL_PENDING` khi gửi / chuyển cấp, CorrelationId `APR:<Id>`) phát ở BOT-1.7 – hiện thẻ đồng bộ chạy cho tin outbox có CorrelationId đó; "duyệt có cảnh báo vượt định mức → Cao" chờ api trả cờ cảnh báo trong GetDetail (BOT-1.7); lịch ca 7 ngày, xin nghỉ / đổi ca từng bước: BOT-1.5. DB dev chưa có user được cấp rõ quyền Duyệt có liên kết Telegram → luồng duyệt thật chỉ kiểm bằng test tự động.

### BOT-1.5a – Bot Vận hành: chung + Bếp + ATTP · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-1.5a-vanhanh-bep-attp`; 1.5b Giao vận + điểm phát, 1.5c còn lại – TODO)
- **Chung**: QR cổng `/start CHAMCONG` khi đã liên kết → mở thẳng chấm công (xin vị trí); 📅 Lịch ca `/lichca` (web: `ShiftAssignment/GetMine` mới; chỉ EmployeeId: `BotChannel/EmployeeGetMyShifts`); 🏖 Xin nghỉ `/nghi` từng bước bằng nút (`Conversations/Flows/LeaveRequestFlow`: loại nghỉ `BotChannel/GetLeaveTypes` → hôm nay / mai / ngày kia / gõ dd/MM → 1 / 2 / 3 / gõ số ngày → lý do ForceReply → web `LeaveRequest/Create` bằng JWT user, chỉ EmployeeId `BotChannel/EmployeeCreateLeave` – quy tắc đơn nghỉ giữ ở api). `ConversationStore` (trạng thái nhiều bước, bộ nhớ 30′). Bàn phím chung nay 6 chức năng (+ ⏳ khi có quyền duyệt → 7 → 3 nút cố định).
- **Bếp** (`VanHanh/BepActions`, mục `#/prod-batch`): 🍳 Mẻ cần làm `/me` (`ProductionBatch/GetBoard` hôm nay, giờ VN) → thẻ mẻ theo trạng thái: ▶️ Bắt đầu (`Start`), 🌡 Nhiệt độ lõi (nút 70 / 72 / 75 / 78 / 80 / 85 hoặc gõ – `RecordCoreTemperature`, ngưỡng kiểm ở api), 🧪 Lưu mẫu (`RecordSampleRetained`), ✅ Xong mẻ (số suất **điền sẵn theo kế hoạch** 1 chạm / −2% / −5% / gõ – `Finish`), 🔄 Làm mới; 📈 Tiến độ ca `/tiendo` (đếm theo trạng thái, suất đạt / kế hoạch).
- **ATTP** (`VanHanh/AttpActions`, mục `#/qc-checklist-result`): 🧽 Checklist ATTP `/checklist` – chọn bộ (`FoodSafetyChecklist/GetAllData`) → `FoodSafetyChecklistResult/GetRun` hôm nay → **checklist trong 1 tin** (kết quả đã ghi điền sẵn, ❗ mục trọng yếu, bấm ✅ / ❌ từng mục) → ✔️ Hoàn tất → `Record` (JSON); ⛔ Mẻ đang giữ `/giu` (QC HOLD / REJECT / không đạt hôm nay).
- **api**: `ShiftAssignment/GetMine` (tự phục vụ, 7 ngày, ≤ 31); `LeaveRequestService.CreateForEmployee` (nhân viên từ định danh – quy tắc như Create); `BotChannel/GetLeaveTypes`, `EmployeeCreateLeave` (ghi dưới system, Lý do "[Telegram] …"). messaging: `IDelicareApiClient.PostJsonAsUserAsync`, `Core/VnTime`, `Audiences/Internal/WebActions` (lấy token / nói / sửa tin / hỏi ForceReply / lỗi 401–403 dễ hiểu).
- **Kiểm tra**: build 0 lỗi; test **383 / 383** (+7: `Tests/Messaging/MessagingVanHanhTests` 6 – QR cổng → chấm công + lịch ca, xin nghỉ web từng bước (chưa lý do → chưa gọi api), xin nghỉ chỉ EmployeeId + gõ ngày sai / đúng, thẻ mẻ đủ nút + nhiệt độ nút / gõ "73,5" + xong mẻ điền sẵn + lưu mẫu + bắt đầu + tiến độ, 403 báo rõ + không quyền màn thì không gọi api Bếp, checklist 1 tin điền sẵn / bấm / Hoàn tất JSON PASS-FAIL + mẻ đang giữ; `BotIdentityTests.Employee_only_leave…`). Luồng thật DB dev (`tools/bot-smoke/flow-vanhanh.mjs`): menu có Bếp + ATTP, mẻ hôm nay (0), tiến độ, mở checklist thật (5S sau ca, 4+ mục), lịch ca / xin nghỉ đủ bước → api từ chối có lời báo (admin không gắn nhân viên): **ĐẠT**, số dòng LeaveRequest / FoodSafetyChecklistResult / TemperatureLog trước = sau.
- **Chưa làm (1.5c)**: 🔁 Đổi ca (cần API mới "ca có thể đổi" – nhân viên không có quyền xem lưới phân ca, PROPOSED p24); Bếp: ai chưa vào (tổ), chấm hộ, chia suất / nấu thêm / thừa, NL của ca, truy xuất mẻ, lô vệ sinh hộp khay; ATTP: lô chờ QC đầu vào, nhiệt độ, truy xuất (gõ mã ME- / NL- đã tra được qua CodeSearch).

### DB-MIGRATIONS – Cấu trúc bảng bằng migration, đồng bộ mọi môi trường · DONE 2026-11-18 (nhánh `feat/DB-MIGRATIONS`, chưa push / gộp)
- **Cách làm** (DECISIONS mục DB-MIGRATIONS): `DB/migrations/*.sql` chạy theo thứ tự tên file, mỗi file 1 lần / DB. Loại khai ở dòng đầu: `baseline` (`20261113_00_baseline_124_tables.sql` = nguyên văn DDL chuẩn 124 bảng của PO, chỉ chạy trên DB trống), `legacy-upgrade` (script PO 91 → 124, chỉ chạy khi thiếu bảng), `normal` (về sau, chạy 1 lần). Ghi nhận bằng extended property `DelicareMigration:<file>` = `RUN|ADOPT|giờ UTC|sha256` – **không thêm bảng**. File đã chạy mà bị sửa → cảnh báo, không chạy lại.
- **Bộ chạy**: `deploy/db/Migrations.ps1` (Deploy-Database.ps1: thêm `-ListMigrations`, `-MigrationsOnly`; DB trống vẫn cần `-CreateSchemaIfEmpty`) và `deploy/db/migrations.sh` (db-init.sh: chạy migration + kiểm thiếu bảng / cột so với baseline; migrate.sh / compose gốc: migration mới trên DB có bảng cần `DB_AUTO_MIGRATE=1` + backup). Không còn script nào tạo bảng trực tiếp từ `docs/reference`. Git Bash trên Windows: `cygpath -w` cho thư mục tạm (Linux không đổi).
- **Kiểm tra**: test **387 / 387** (+4 `Schema/MigrationFilesTests`: quy ước tên / loại / baseline đứng đầu, baseline = DDL chuẩn từng ký tự, migration mới chạy lại được + không DROP / TRUNCATE / DELETE, 2 bộ chạy cùng quy tắc). DB trống qua PowerShell: baseline RUN + legacy ADOPT → 124 bảng / 2264 cột, **khớp từng cột** với DB dev; chạy lại: 0 việc. DB trống qua bash `db-init.sh` đầy đủ: migration + 131 SP + seed đạt, chạy lần 2: 0 migration. Migration `normal` giả lập: chạy 1 lần, chạy lại bỏ qua, sửa file → cảnh báo. Xóa thử 1 cột → kiểm cấu trúc báo đúng cột. DB dev `AppDelicare` + staging `AppDelicare_Staging`: `-MigrationsOnly` → ghi nhận 2, chạy 0, vẫn 124 / 2264. DB thử đã xóa.
- **Chưa kiểm**: nhánh legacy-upgrade trên DB cũ 91 bảng thật (không có DB mẫu); chạy trong Docker / Linux và SQL Server 2022 thật (máy không có Docker).

### BOT-1.5b – Bot Vận hành: Giao vận + Điểm phát · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-1.5b-giaovan-diemphat`; 1.5c còn lại – TODO)
- **Tài xế** (`VanHanh/GiaoVanActions`, mục `#/log-stop`): 🚚 Chuyến của tôi `/chuyen` (api mới `DeliveryRoute/GetMine` – DECISIONS p26; 1 chuyến → mở thẳng thẻ) → thẻ chuyến (`GetDetail`: xe, tài xế, giờ, suất, nhiệt độ xuất phát, điểm dừng, ⛔ điều kiện xuất phát còn thiếu) → 🚀 Xuất phát (nút nhiệt độ ≥ ngưỡng / gõ; đã đo đạt → 1 chạm không đo lại – `Dispatch`) → thẻ điểm: 📍 Đã đến (`DeliveryStop/Arrive`), ✅ Giao (**số suất điền sẵn = đã xếp lên xe**, rồi kế hoạch / −2% / −5% / gõ → nhiệt độ bàn giao nút / "Không đo" → tên người nhận gõ tay – `Deliver`), ❌ Không giao được (4 lý do chọn sẵn – `Fail`). Nút theo `AllowedActions` của api. 📥 Nhận hộp về `/hop` (mục `#/log-container-return`: điểm đã giao trên chuyến của tôi → gõ số hộp – `ContainerReturn/Record`).
- **Điều phối**: 🗺 Giao vận hôm nay `/giaovan` (mục `#/log-route` – `GetBoard`: chuyến · xe · giờ · trạng thái · điểm xong / tổng · tài xế → mở thẻ chuyến).
- **Điểm phát** (`VanHanh/DiemPhatActions`, mục `#/crm-reconciliation` – p28): 🚚 Xe đến hôm nay `/xeden` (mọi điểm dừng hôm nay theo giờ dự kiến); 🧮 Đối soát suất `/doisoat` (`OrderReconciliation/GetPendingOrders` hôm nay → **số suất phục vụ điền sẵn = số khách nhận khi bàn giao (POD)** → `Reconcile`). Ký nhận tại điểm = ✅ Giao của tài xế.
- **api**: `DeliveryRoute/GetMine?deliveryDate=` (SkipPermission, mặc định hôm nay). messaging: `CallbackData.Kinds.Delivery` "dlv", `Reconciliation` "rcn"; đăng ký ở Program + UpdateDispatcher. Không bảng / cột / seed mới.
- **Kiểm tra**: build 0 lỗi; test **393 / 393** (+6 `Tests/Messaging/MessagingGiaoVanTests`: chuyến của tôi → xuất phát nút nhiệt độ; xuất phát 1 chạm khi đã đo đạt + gõ "66,5"; thẻ điểm → giao điền sẵn 98 (đã xếp) → 65 °C → tên người nhận; đến điểm, không giao được kèm lý do, nhận hộp gõ số (sai → hỏi lại); điều phối + ẩn chức năng khi không có quyền màn; điểm phát đối soát điền sẵn số POD). `tools/bot-smoke/flow-giaovan.mjs` đạt trên DB dev (không ghi dữ liệu): hình dạng JSON GetBoard / GetDetail / DeliveryStop/GetById / GetPendingOrders, GetMine của admin báo "chưa gắn nhân viên", menu có 🚚 Giao vận + 🍱 Điểm phát, thẻ chuyến mẫu CX-280201-DEMOB37B-01.
- **Chưa làm (1.5c)**: 📷 ảnh ký nhận, 🔁 đổi xe / tài xế, 📦 hộp khay hôm nay (điều phối), 🧭 lịch xe, 📣 ghi phản ánh tại điểm, hành động khắc phục khi nhiệt độ thấp (p27); vị trí trực tiếp: BOT-2.4.

### BOT-1.5c – Bot Vận hành: phần còn lại · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-1.5c-vanhanh-con-lai`)
- **🔁 Đổi ca** `/doica` (`Conversations/Flows/ShiftSwapFlow`, tài khoản web): ca còn mở của tôi 14 ngày → api mới `ShiftAssignment/GetSwapCandidates?assignmentId=` (SkipPermission – DECISIONS p24: ca còn mở của đồng nghiệp cùng đơn vị ± 7 ngày từ hôm nay + danh sách đồng nghiệp, chỉ tên + ca) → đổi chéo / 🤝 nhường ca → lý do (≥ 3 ký tự) → `ShiftSwapRequest/Create` ("[Telegram] …", vào duyệt như web). Người chỉ có EmployeeId: chưa (p30).
- **📣 Phản ánh** (`VanHanh/PhanAnhActions`, mục `#/log-complaint`): 📋 đang mở `/phananh` (Mới / Đang xử lý, sắp hạn trước), ⏱ sắp / đã quá hạn `/saphan` (quá hạn + còn ≤ 4 giờ) → thẻ (`GetDetail`): 🙋 Nhận xử lý (`Assign` về mình), ✅ Đã xử lý (cách xử lý gõ tay – `Resolve`), 💬 Ghi chú (`AddComment`) theo AllowedActions; 📣 Ghi phản ánh `/ghiphananh` (cũng ở 🍱 Điểm phát): gõ tên / mã khách → chọn (`Customer/GetAll`) → mức độ → mô tả → `Create` (SourceChannel TELEGRAM).
- **👥 Nhân sự** (`VanHanh/NhanSuActions`, mục `#/hr-shift-assign`): Ai chưa vào ca `/chuavao` (`GetGrid` hôm nay: ca còn mở, đã qua giờ bắt đầu ≥ 10′, chưa chấm, không nghỉ phép) → ✍️ Chấm hộ (chỉ khi có mục `#/hr-attendance`; lý do bắt buộc – `AttendanceEvent/CheckIn` SourceChannel PROXY, api kiểm quyền Thêm).
- **Chỉ xem** (`VanHanh/XemNhanhActions`, ≤ 15 dòng / tin, ẩn theo mục menu): Kho – 📦 Tồn dưới mức `/ton`, ⌛ Cận date `/candate`, 🥬 NL ngày mai + thiếu hụt `/nlmai`, 🧾 PO sắp về / trễ `/po`, 🚛 Hàng về hôm nay `/hangve`; Kế hoạch – 📋 Kế hoạch ngày mai `/kehoach`, 📅 Thực đơn đang áp dụng `/thucdon`, ♻️ Hao hụt 7 ngày `/haohut`, 💰 Giá thành suất `/giathanh`; Kinh doanh – 📦 Đơn ngày mai `/donmai`, ⏰ Khách chưa chốt `/chuachot`; Kế toán – 📦 Hộp khay thất thoát `/thatthoat`, 🧾 PO đã nhận `/podanhan`; QA – 🧪 Lô chờ QC đầu vào `/chokiem`, 🌡 Cảnh báo ATTP `/canhbao` (chuông cảnh báo); Quản trị (admin) – 🖥 Tình trạng hệ thống `/hethong` (job + tin gửi lỗi). Chỗ thiếu API: p29.
- **Khác**: phần chung nay 7 chức năng → bàn phím 3 nút cố định (⚡ Ca của tôi · 📍 Chấm công · 📂 Chức năng – thiết kế BOT-1.4). Callback mới `swp`, `cmp`, `hr`. Không bảng / cột / seed mới.
- **Kiểm tra**: test **403 / 403** (+16: `MessagingSwapTests` 3, `MessagingPhanAnhNhanSuTests` 3, `MessagingXemNhanhTests` 4 – gồm kiểm toàn danh mục chức năng: mã / lệnh không trùng, lệnh hợp lệ Telegram; sửa kỳ vọng bàn phím ở `MessagingHostTests`). `tools/bot-smoke/flow-15c.mjs` đạt trên DB dev (không ghi dữ liệu): 22 lệnh trả số liệu thật (vd. 14 lô cận / hết hạn, KH-261119 thiếu 2 NL, 6 PO sắp về, 31 phản ánh đang mở, 7 job); chạy thử bắt được 1 lỗi tên trường (`complaintStatus`) – đã sửa + test.
- **Để sau**: 📷 ảnh ký nhận, 🔁 đổi xe / tài xế, 🧭 lịch xe, 📦 hộp khay hôm nay (điều phối); Bếp – chia suất / nấu thêm / thừa, NL của ca, ai chưa vào **của tổ**, lô vệ sinh hộp khay, truy xuất mẻ (đã tra được bằng gõ mã ME-); 🔄 lập lại kế hoạch, ✏️ đề xuất đổi món; 📥 nhập file máy chấm công; đổi ca cho người chỉ có EmployeeId (p30); API lọc theo p29.

### BOT-1.6 – Bot Điều hành · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-1.6-dieuhanh`)
- **Chức năng** (`Audiences/Internal/DieuHanh/DieuHanhActions`, phân hệ 📊 Điều hành, chỉ bot DH – DECISIONS p31): 📊 Tổng quan hôm nay `/tongquan` (`Dashboard/GetExecutive`, mục `#/dash-exec`: suất, sản xuất, giao, vào ca, QC, tồn, phản ánh, tiến độ từng ca, cảnh báo) · 🚨 Việc đang mở `/viecmo` (`Common/GetAlerts` – mọi cảnh báo của màn được xem) · 🎯 KPI 7 ngày `/kpi` (`KpiSnapshot/GetDaily`) · 📅 KPI tháng `/kpithang` (`GetMonthly`; ✅ đạt / ⚠️ chưa đạt / ⚪ chưa có số) · 📈 Tiến độ bếp `/tiendobep` · 🗺 Giao vận trực tiếp `/giaovantt` · 📦 Tồn & thiếu hụt `/tonthieu` (dùng lại BepActions / GiaoVanActions / XemNhanhActions) · 🔍 Truy xuất `/truyxuat` (gõ mã – CodeSearch). ⏳ Chờ tôi duyệt + phần chung có sẵn ở mọi bot; nút mở bot DH từ bot VH có từ BOT-1.2.
- **Sửa**: chức năng Bếp / ATTP của 1.5a thiếu giới hạn bot → lọt sang bot DH (test mới bắt được) – nay chỉ VH. Không API / bảng / cột / seed mới.
- **Kiểm tra**: test **406 / 406** (+3 `MessagingDieuHanhTests`: tổng quan, KPI đạt / chưa đạt / chưa có, việc đang mở + chức năng DH không có trên VH, bot DH không lẫn phân hệ VH). `tools/bot-smoke/flow-dieuhanh.mjs` (chỉ bật bot DH) đạt trên DB dev: 26 phản ánh quá hạn + 46 lần đo nhiệt độ ngoài ngưỡng ở 🚨, 11 chỉ tiêu KPI (tháng: phản ánh đúng hạn 62.07% / mục tiêu 95% ⚠️), thiếu hụt KH-261119.
- **Để sau**: bản tin 7h / báo cáo 17h / tuần / tháng tự gửi (BOT-2.3), tin liên phòng theo vai (BOT-1.7), việc liên phòng nhận xử lý (BOT-2.2).

### BOT-1.7 – Thông báo nghiệp vụ (sự kiện tức thời) · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-1.7-thong-bao`)
- **Danh mục** `NotificationCatalog` đủ BOT_TELEGRAM §5.1 (34 mã – phạm vi → bot đích, mức KHẨN có chuông / THƯỜNG, TỔNG HỢP im lặng); mã cũ đổi theo spec (SHIFT_CONFIRMED, APPROVAL_RESULT, FOODSAFETY_CRITICAL). messaging `MessageTemplates`: câu chữ mọi mã + **hiển thị nút của tin outbox** (`PayloadJson.buttons`, bỏ nút sai / callback > 64 byte).
- **Phát tin** (`Messaging/BusinessNotifier` – chỉ thêm xếp tin trong transaction nghiệp vụ, lỗi không làm hỏng nghiệp vụ – p33; người nhận theo quyền menu không kèm admin, quyền Thêm khi cần – p32): APPROVAL_PENDING (gửi duyệt + chuyển cấp → người duyệt cấp đó, nút 📄 Xem & duyệt), APPROVAL_RESULT (người gửi + nhân viên của đơn nghỉ / đổi ca), SHIFT_CONFIRMED (ShiftAssignment/Confirm – 1 tin / nhân viên gom ca, **`notificationSent` thật** – Catalog 204), FOODSAFETY_CRITICAL (checklist lỗi trọng yếu – Catalog 162, thay câu "làm ở Release 2"), CORE_TEMP_LOW, IOT_OUT_OF_RANGE (1 tin / khu / giờ), TRIP_DEPARTED (Catalog 181 → điểm phát), COMPLAINT_NEW (CSKH; mức cao → bot DH), MENU_PUBLISHED (Catalog 85), QC_INCOMING_FAIL, PLAN_READY (job LockOrders), SHORTAGE_MRP (job Mrp), SYSTEM_JOB_FAILED (job lỗi → admin).
- **Không** bảng / cột / seed mới; không đổi nghiệp vụ (chỉ thêm xếp tin + câu cảnh báo ATTP).
- **Kiểm tra**: test **410 / 410** (+4 `Notifications/BusinessNotificationTests` (DB trong bộ nhớ, gọi API thật): đề nghị mua 2 cấp → tin tới đúng người duyệt cấp 1 (VH) → cấp 2 (DH, không gửi 2 bot cùng người) → kết quả về người gửi; xác nhận phân ca 1 tin / nhân viên, gọi lại không trùng, notificationSent; phản ánh mức cao → CSKH có quyền Thêm ở DH, không tới người tạo / người chỉ xem; mọi mã có câu riêng + nút). Test job trên DB dev dùng thông báo rỗng (không xếp tin thật).
- **Chưa làm (1.7d)**: QR `start=DOC_…` trên phiếu in + bot mở đúng chứng từ; nhóm chat theo `ConfigJson.groups`; DELIVERY_TEMP_LOW (nhiệt độ giao thấp). Tin theo lịch (mẻ trễ, xe chưa rời bếp, điểm trễ, chưa vào ca, 19:00 khách chưa chốt, PO trễ, bản tin 7h / 17h): BOT-2.3. Chưa chạy thử gửi thật trên DB dev (sẽ ghi OutboundMessage vào DB dev – làm khi anh đồng ý / trên staging).

### BOT-2.1 – Thẻ ca / Thẻ ngày · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-2.1-the-ca`)
- **api**: `Messaging/ShiftCardService` – `BotChannel/SyncShiftCards` (khóa client): ca Kế hoạch / Xác nhận / Đã vào ca có giờ bắt đầu − 15′ ≤ bây giờ ≤ giờ kết thúc + 30′ → xếp SHIFT_CARD vào outbox cho nhân viên (bot VH, chống trùng `SHIFT_CARD:<ca>`, tài khoản hệ thống) + trả thẻ đã gửi kèm nội dung mới; `BotChannel/MyShiftCard` (⚡). Nội dung: ca + giờ, đã / chưa vào ca, chuyến của tài xế hôm nay, giai đoạn (trước / trong / cuối ca); nút theo giai đoạn ∩ quyền menu (📍 Chấm công, 👥 Ai chưa vào, 📋 Kế hoạch ca / 🍳 Mẻ cần làm, 🧽 Checklist, 🚚 Chuyến của tôi, 📈 Tiến độ ca, ♻️ Hao hụt, 🔄 Làm mới). Job `DayCard` 07:00: Thẻ ngày (bot DH) cho người có mục #/dash-exec – chờ tôi duyệt, cảnh báo đang mở, số liệu hôm nay; nút Tổng quan / Chờ tôi duyệt / Việc đang mở. Mã mẫu SHIFT_CARD, DAY_CARD.
- **messaging**: `Experience/ShiftCards` + `ShiftCardPump` (60 giây): sửa thẻ khi nội dung đổi, ≤ 1 lần / 5′; OutboundPump ghim thẻ sau khi gửi; ⚡ Ca của tôi = làm mới thẻ ghim (chưa có → gửi + ghim; không có ca → màn ca cũ); nút thẻ `sc:run:<chức năng>` chạy như gõ lệnh (tin mới, không đè thẻ, kiểm lại quyền), `sc:refresh` sửa chính thẻ. DECISIONS p34.
- **Kiểm tra**: test **416 / 416** (+6: `Messaging/MessagingShiftCardTests` 4 – ghim chỉ thẻ, sửa khi đổi + ≤ 1 lần / 5′ với đồng hồ giả, ⚡ sửa thẻ ghim / gửi + ghim / về màn cũ, nút thẻ thành tin mới; `Notifications/ShiftCardServiceTests` 2 – 15′ trước ca mới xếp, 1 thẻ / ca, nút theo quyền, thẻ đã gửi trả nội dung mới (trong ca, đã vào ca), hết ca + 30′ thôi làm mới; Thẻ ngày tới quản lý ở DH, chạy lại không trùng). Sửa danh sách job trong test DI (+DayCard).
- **Để sau**: mẻ phụ trách / hàng về trong Thẻ ca (cần nối mẻ ↔ ca), bàn giao ca cuối ca (BOT-3.1); chưa chạy thật trên DB dev (ghi OutboundMessage).

### BOT-2.2 – Sự việc liên phòng: nhận xử lý, ghi chú, leo thang · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-2.1-the-ca`)
- **api**: `Messaging/IncidentService` – `BotChannel/IncidentAction` (claim / note / done trên CHÍNH tin được bấm; khóa theo sự việc; trạng thái trong PayloadJson.incident của mọi tin cùng CorrelationId – p35; trả tin cần sửa), `BotChannel/RunEscalation` (chưa ai nhận quá mốc → INCIDENT_UNCLAIMED tới nhóm bậc kế ở bot DH, chống trùng theo bậc, dừng khi đã có người nhận – p36). Tin KHẨN liên phòng (FOODSAFETY_CRITICAL, CORE_TEMP_LOW, IOT_OUT_OF_RANGE, QC_INCOMING_FAIL…) tự có nút 🙋 Nhận xử lý / 💬 Ghi chú / ✅ Đã xử lý. Job `LeaveHandover` 16:00: người có đơn nghỉ đã duyệt từ ngày mai mà còn việc chờ mình duyệt → nhắc (LEAVE_HANDOVER).
- **messaging**: `Chung/IncidentActions` (nút `inc:*`, ghi chú gõ tay, sửa tin ở mọi người, báo người đến sau tên người đã nhận); `EscalationPump` 60 giây; `MessageTemplates` hiện người nhận / ghi chú / đã xử lý, bỏ nút không còn hợp lệ.
- **Kiểm tra**: test **420 / 420** (+4: `Notifications/IncidentServiceTests` 2 – người bấm trước được ghi, người sau thấy tên, ghi chú + xong hiện ở mọi người, không thêm tin; leo thang 10′ → COO ở DH đúng mốc với đồng hồ giả, không trùng, dừng khi đã nhận; `Messaging/MessagingIncidentTests` 2). Danh sách job trong test DI +LeaveHandover.
- **Để sau**: leo thang duyệt quá hạn / phản ánh quá SLA (BOT-2.3 theo lịch), bảng sự việc riêng (GAP-INT-04), nhóm "trưởng bộ phận" cho bậc 2.

### BOT-2.3 – Giờ yên lặng theo ca + tin theo lịch · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-2.1-the-ca`)
- **Giờ yên lặng** (`Messaging/QuietHours`, cấu hình `Messaging:QuietHours` – PROPOSED p37): NotificationService tính notBefore RIÊNG từng người nhận theo ShiftAssignment (PlannedStart / PlannedEnd, bỏ ca Vắng). 🟡 / ⚪: trong ca hoặc ≤ 60′ trước ca → gửi ngay; ngoài ca → hẹn 60′ trước ca kế tiếp (tìm 3 ngày); không có phân ca → chỉ gửi 07:00–21:00, ngoài khung hẹn 07:00. 🔴: không tới người có phân ca đang ngoài ca; nếu vì vậy không còn ai nhận → gửi tất cả (không mất tin khẩn); tin leo thang (`NotificationRequest.Escalation`) bỏ qua giờ yên lặng. Hẹn giờ vẫn ở `PayloadJson.notBefore` (GAP-INT-03 – không thêm cột).
- **Kiểm tra định kỳ** (`Messaging/BotWatchService` – `BotChannel/RunWatch`, messaging `WatchPump` mỗi `Messaging:WatchIntervalSeconds` = 300 giây; ngưỡng `Messaging:Watch` – PROPOSED p38): BATCH_LATE (mẻ chưa bắt đầu / chưa xong quá 15′), TRIP_NOT_DEPARTED (quá 10′, sự việc – leo thang 10′ → COO), STOP_LATE (quá 15′, sự việc), NOT_CHECKED_IN (quá 10′, 1 tin / ca kèm danh sách), COMPLAINT_SLA (còn ≤ 60′ + lần 2 khi quá hạn), PO_LATE (quá ngày hẹn, 1 lần / PO), APPROVAL_OVERDUE (`ApprovalRequestService.NotifyOverdueAsync`: quá hạn → người duyệt của cấp; quá thêm 24 giờ → nhóm COO ở bot DH). Mỗi việc chống trùng bằng IdempotencyKey; một việc lỗi không chặn việc khác; chỉ đọc nghiệp vụ.
- **Job theo lịch** (`Jobs/BotJobs.cs`, khai báo trong `Jobs` của appsettings – đổi giờ / tắt từng bản tin): MorningBrief 07:00 (kết quả hôm qua + kế hoạch hôm nay, bot DH, người có #/dash-exec), DailyReport 17:00 (kết quả hôm nay + đơn ngày mai), RemindUnconfirmedOrders 19:00 (đơn ngày mai Nháp / Đã gửi → NV KD phụ trách khách `Customer.UserAssignId`, khách chưa có người phụ trách → người có quyền Thêm đơn hàng – thay phần gửi tin của T23.2), IngredientsTonight 21:00 (nhu cầu NL ngày mai, phần còn thiếu, PO hẹn giao ngày mai → thủ kho). DayCard / LeaveHandover nay cũng khai báo trong appsettings. Mẫu mới: APPROVAL_OVERDUE, INGREDIENTS_TONIGHT.
- **Không** bảng / cột / seed mới; không đổi nghiệp vụ.
- **Kiểm tra**: test **424 / 424** (+4 `Notifications/ScheduledNotificationTests` – đồng hồ giả: giờ yên lặng theo ca từng người (trong ca / ca tối / văn phòng, 🔴 bỏ người ngoài ca + gửi khi không còn ai, 22:30 → 07:00, leo thang bỏ qua); kiểm tra định kỳ đúng ngưỡng (xe trễ 15′ có, trễ 5′ chưa), mẻ trễ, SLA sắp / quá hạn, duyệt quá hạn tới người duyệt (trừ người gửi), gọi lại không trùng; 19:00 nhắc đúng NV KD, đơn đã xác nhận không nhắc, chạy lại không trùng; bản tin sáng tới bot DH, người ca tối được hẹn 13:00). Test cũ: giờ yên lặng TẮT mặc định trong `InMemoryApiFactory`.
- **Để sau**: PERIOD_REPORT tuần / tháng; khách CHƯA có đơn ngày mai (cần lịch giao theo hợp đồng – chờ BA); "cấp trên trực tiếp" cho duyệt quá hạn (chưa có dữ liệu – đang dùng nhóm COO); ShiftReminder riêng (Thẻ ca 15′ trước ca đã thay); chưa chạy thật trên DB dev (ghi OutboundMessage).

### BOT-2.4 – QR vật thể + vị trí trực tiếp tài xế · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-2.4-qr-vitri`)
- **QR vật thể** (`Chung/QrActions`, BOT_TELEGRAM §1.3): `/start LINE_<mã>` → mẻ đang nấu / chờ nấu hôm nay của dây chuyền / bàn nấu (1 mẻ → thẻ mẻ [▶️ Bắt đầu] [✅ Xong mẻ]); `VEH_<mã xe | biển số bỏ dấu>` → chuyến hôm nay của xe (chuyến của tôi trước, rồi bảng giao vận nếu có quyền) [🚀 Xuất phát]; `LOC_<mã>` → tồn theo lô tại vị trí kho (hết / cận hạn lên đầu); `CLEAN` → lô vệ sinh hộp khay hôm nay. Quyền = JWT của người quét; người chưa liên kết → đăng ký trước; tham số QR không bị hiểu nhầm là mã liên kết.
- **api** (chỉ thêm – FE: `docs/frontend-handoff/BOT-2.4.md`): `ProductionBatch/GetBoard` + `LineCode`, `InventoryBalance/GetByLot` + `storageLocationCode`, điểm dừng + `latitude / longitude` (CustomerDeliveryLocation), `DeliveryRoute/GetMine` + `listKitchen` (bàn / trạm bếp có tọa độ).
- **Vị trí trực tiếp** (`GiaoVanActions.LiveLocationAsync`; tin vị trí có live_period / cập nhật edited_message – vị trí gửi 1 lần vẫn là chấm công): so với bếp + điểm giao (bán kính `Messaging:LiveLocation:RadiusMeters` = 150 m PROPOSED, bếp có RadiusMeters riêng thì dùng). Rời bếp + chuyến chưa xuất phát: đã đo nhiệt độ đạt → tự ghi xuất phát (Dispatch kèm tọa độ), chưa đo → hỏi nhiệt độ. Vào bán kính điểm giao của chuyến đang giao → tự ghi Đã đến đúng 1 lần (Arrive kèm tọa độ) + gửi thẻ điểm [✅ Giao] [❌ Không giao được]. Không lưu từng điểm vị trí (chỉ trạng thái trong bộ nhớ messaging); nút 📍 / 🚀 vẫn dùng khi mất tín hiệu.
- **Kiểm tra**: test **431 / 431** (+7: `Messaging/MessagingQrLiveTests` 5 – phân tích tham số QR, LINE_ mở mẻ có nút Bắt đầu, VEH_ (biển số bỏ dấu) mở chuyến có Xuất phát, LOC_ tồn tại kệ, người chưa liên kết → đăng ký, vị trí trực tiếp: rời bếp → Dispatch, vào 55 m điểm giao → Arrive 1 lần; `Notifications/QrLiveApiTests` 2 – GetMine có tọa độ + bếp (GetBoard không), lọc mẻ theo mã dây chuyền / bàn nấu). Chạy thử API chỉ đọc trên DB dev (GetBoard + LineCode, GetByLot + storageLocationCode, CleaningBatch, GetBoard giao vận) – phát hiện và sửa listKitchen gắn nhầm vào GetBoard.
- **Để sau**: in ảnh QR (FE), ghi kết quả vệ sinh hộp khay trên bot (hiện chỉ xem), chưa thử với Telegram thật (vị trí trực tiếp cần điện thoại).

### BOT-1.7d – QR trên phiếu in, nhóm chat, nhiệt độ giao thấp · D-62 · DONE 2026-11-18 (nhánh `feat/BOT-1.7d-qr-phieu-nhom`)
- **QR phiếu in** (BOT_TELEGRAM §1.3): `PrintDocumentModel.BotLink` = `t.me/<bot VH>?start=DOC_<GR|MI|SC|DO|POD|RC>_<Id N>` (bot VH đang bật có BotUsername) + dòng ghi chú trên bản in; ảnh QR do FE vẽ (không thêm thư viện BE). API mới `Common/GetPrintDocument` (quyền Xem màn của loại phiếu). Bot: `/start DOC_…` → nội dung phiếu (số, ngày, thông tin chính, dòng hàng) bằng JWT người quét. FE: `docs/frontend-handoff/BOT-1.7d.md`.
- **Nhóm chat** (`Messaging/ChatGroups`, PROPOSED p40): `IntegrationChannel.ConfigJson.groups` = [{ code, chatId, templates, mode }]; mẫu trong templates → 1 tin vào nhóm, `replace` (mặc định – người trên cùng bot không nhận tin riêng) / `copy`. Không bao giờ vào nhóm: tin cá nhân / duyệt, thẻ ghim, tin gửi tay. Bot: nút trên tin trong nhóm – 🙋 / ✅ sự việc ghi ngay (thông báo nhỏ cho người bấm, sửa tin của nhóm + mọi người), 💬 hỏi trong chat riêng; nút khác mở trong chat riêng; người chưa liên kết → nhắc liên kết; tin thường trong nhóm bỏ qua.
- **DELIVERY_TEMP_LOW** (`DeliveryRules.NotifyTempLowAsync`): lần đo nhiệt độ xuất phát / bàn giao dưới ngưỡng (đã ghi khắc phục) ở DeliveryRoute/RecordTemperature, Dispatch, DeliveryStop (Arrive / Deliver) → 🔴 điều phối (quyền Thêm #/log-route, trừ người đo), sự việc (Nhận xử lý, leo thang 10′ → COO).
- **Không** bảng / cột / seed mới.
- **Kiểm tra**: test **437 / 437** (+6: `Messaging/MessagingDocGroupTests` 4, `Notifications/GroupAndTempTests` 2; `Common/PrintTests` thêm BotLink + GetPrintDocument quyền Xem / 403). Chạy thử `Common/GetPrintDocument` trên DB dev (3 phiếu nhập thật đọc được; loại sai → 400; BotLink trống vì bot VH dev chưa có BotUsername).
- **Để sau**: nút thao tác theo phiếu (ký nhận POD, duyệt) khi mở bằng QR – hiện chỉ xem; nhóm chat chưa thử với Telegram thật.

### MA-1 – Quy tắc mã toàn hệ thống + khách hàng theo loại · D-63 · DOING 2026-11-19 (nhánh `feat/MA-1-code-rules`)
Kế hoạch: 1) DocumentNumberService dạng danh mục + tách tiền tố §11 2) khách hàng theo loại 3) danh mục G1 sinh mã 4) bảng phân nhóm + API + test chặn 5) bot 6) luồng .http 7) script đánh lại mã 8) FE (MA-1.8).
Rà soát code cũ: `DocumentNumberService` (T11) đã có sp_getapplock + MAX → thêm dạng danh mục vào chính service này; Insert / Update danh mục ở đúng Service của entity; API quy tắc mã thêm vào `CommonController` / `CommonService` (không controller mới).
Đã làm:
- **MA-1.1** `DocumentKind` mới: danh mục KHB / KHC / NC / NL / MA / KO / XE / NV / DP / BG (`TIỀNTỐ-NNNNN`, quá 99.999 → 6 số, `NextCodeAsync`); chứng từ DH / HD / NU / DM / MH / PO / PN / PX / KK / GD / LO / KS / LS / ME / DG / GH / CX / VS / PA / XN / DN / DC / QC / MR (bỏ `Voucher` PH). Số cũ khác tiền tố → không tính vào dãy mới. Phiếu QC có số QC- riêng (trước dùng số phiếu nhập); nhu cầu NVL 1 số MR- / kế hoạch (giữ ở lần tính lại).
- **MA-1.2** `CustomerService`: loại BUSINESS / INDIVIDUAL (`CustomerTypes`), mã + CIF máy chủ sinh, Name / DisplayName / CompanyId (công ty của nhân viên gắn tài khoản – GAP-HR-02), kiểm bắt buộc + định dạng theo loại, MST / CCCD không trùng, xóa trống trường khác loại, đổi loại theo D-63 i, thông báo kèm mã.
- **MA-1.3** Supplier / Ingredient / Dish / Warehouse / Vehicle / Employee / CustomerDeliveryLocation / PricePolicy / Menu (+ Clone): Insert luôn sinh mã, Update giữ mã. Hợp đồng / đề nghị mua / lô nhập tay: số luôn máy chủ sinh (trước: giữ số gửi lên).
- **MA-1.5** bot `CodeSearch`: `^(tiền tố)-d{5,6}` + danh sách tiền tố mới, giữ PH / KH. `full-flow.http`, `pilot-flow.http` + 2 test luồng C# theo mã mới. `uspVehicle_GetAll` tìm cả biển số (đã chạy DB dev).
- **MA-1.7** `CodeRules` (Services/Implement): G1 lấy từ `DocumentNumberService.Kinds`, G2 / G3 liệt kê; `GET api/Common/GetCodeRules?entity=`; `CodeRulesTests`: mọi thuộc tính *Code / *No / *Number của mọi entity phải có nhóm, tiền tố duy nhất + không lồng nhau, `NormalizeUserCode` (G2).
- **MA-1.4** `DB/scripts/MA1_renumber_customer_supplier.sql`: loại cũ → Industry + BUSINESS; đánh lại KHB / KHC / NC theo ngày tạo, CIF = Code; ghi AuditChange (system); bắt buộc `-v DryRun=1|0`; in bảng đối chiếu (sqlcmd -o csv).
- Handoff FE: `docs/frontend-handoff/MA-1.md`.
Kiểm tra: `dotnet build` 0 lỗi; `dotnet test` **472 / 472** (build ra `AppDelicare/ma1art` – API dev của anh đang chạy khóa dll trong bin). API bản mới chạy cổng 5299 trên DB dev: `seed-demo` (full-flow.http) **218 / 218** (DEMOMA1E, ngày 2026-12-12; các lần MA1A–MA1D thử trước đó để lại dữ liệu [DEMO]). Script MA-1.4 trên DB dev: chạy thử → 80 khách (FACTORY) + 53 NCC; thử "chạy thật + chạy lại" trong 1 giao dịch rồi ROLLBACK → lần 2 = 0 / 0 / 0, DB dev không đổi. **Chưa kiểm SQL Server 2022 thật (máy không có Docker).**
AC: [x] 2 khách cùng loại → mã liên tiếp (sp_getapplock như T11) · [x] KHB thiếu MST / KHC thiếu Họ → lỗi rõ trường · [x] Code / CIF gửi lên bị bỏ qua, CIF = Code · [x] đổi loại khi đã có đơn → lỗi · [x] chứng từ mới ra tiền tố §11, số cũ vẫn tìm được (bot nhận PH / KH) · [x] script chạy thử in bảng đối chiếu, lần 2 không đổi · [x] entity mới có cột mã chưa xếp nhóm → test đỏ · [ ] form chung không cho gõ mã G1 (MA-1.8) · [x] test xanh, full-flow xanh · [ ] build FE.
PROPOSED – BA/PO CONFIRM (chưa ghi DECISIONS vì file đang có thay đổi chưa commit của anh): (1) "Chi nhánh phục vụ" chỉ bắt buộc khi đã có OrganizationUnit loại BRANCH (DB dev chưa có chi nhánh nào); (2) lô nhập tay không có NCC → hậu tố `TDK`; (3) số hợp đồng giấy của khách không còn lưu ở ContractNumber → ghi vào Ghi chú / Tên; (4) bộ lọc `QualityStatus` của `uspMaterialLot_GetAll` dùng LIKE ("DAT" khớp cả "KHONG_DAT") – chưa sửa.
Bổ sung (lần 2, cùng ngày):
- **MA-1.3 import** `MasterDataImportService`: cột mã của 8 danh mục G1 không còn bắt buộc – trống → máy chủ sinh mã, có mã → khớp theo mã như cũ; khách: loại cũ → Industry + BUSINESS, CIF = Code (bỏ cột CIF riêng).
- **MA-1.7 (2) G2**: 15 service (Company, OrganizationUnit, Position, Shift, DishCategory, IngredientCategory, ProductionLine, KitchenWorkstation, StorageZone, StorageLocation, BotConfiguration, IntegrationChannel, AdministrativeProvince / District / Ward) chuẩn hóa mã chữ hoa không dấu A–Z 0–9 - _ khi thêm / sửa; sửa mã khi bản ghi đã được tham chiếu (ReferenceGuard / IsReferencedAsync) → lỗi "đã được dùng". Chưa áp: UnitMeasure, SupplierMaterial, FoodSafetyChecklist / Item, KpiMetricDefinition (không có mẫu kiểm trùng mã chuẩn – KPI đã tự viết hoa).
- Test **473 / 473**.
- **MA-1.8 FE – DỪNG:** bắt đầu sửa `core/store.js` (quy tắc mã từ `Common/GetCodeRules`, danh mục loại khách, `Store.save` ghi chờ API – chưa commit, chưa build) thì thao tác sửa FE bị chặn: luật "chỉ backend, không sửa app-delicare-client" (anh dặn 2026-11-17) mâu thuẫn D-63 l (giao làm FE cho MA-1). Chờ anh xác nhận.
Bổ sung (lần 3 – anh đồng ý làm FE + 4 điểm PROPOSED, 2026-11-19):
- **MA-1.8 FE** (khung mockup `assets/delicare/js`): `store.js` – quy tắc mã từ `Common/GetCodeRules` (thử lại khi chưa đăng nhập; DEMO dùng bảng G1 tối thiểu), danh mục `Customer.CustomerType`, `Store.userCode` (G2), `Store.save` ghi chờ API (không gửi cột G1); `ui.js` – `UI.form` ô Mã G1 chỉ đọc "(tự sinh khi lưu)", G2 tự chữ hoa không dấu, chỉ đóng form + báo thành công (kèm mã từ thông báo máy chủ) khi API trả 200, lỗi hiện ngay trong form, form khách hàng chọn loại → ô theo loại (D-63 g), loại khóa khi sửa, ẩn CIF ở form / hồ sơ (`UI.hidden`); `menu.js` nhân bản không nhập mã; `sales.js` số hợp đồng tự sinh, cột Loại tiếng Việt. `npm run build` xanh; nạp thử util / store / ui trong Node (DOM giả) chạy đúng. **Chưa thử trên trình duyệt với API thật.**
- D-63 m–q ghi vào DECISIONS (chi nhánh không bắt buộc khi chưa có, hậu tố TDK, số HĐ giấy vào Ghi chú, lọc QualityStatus, import); `uspMaterialLot_GetAll` lọc `QualityStatus` so khớp đúng (đã chạy DB dev).
Còn lại: các `.http` khác (be2.x, p3–p8, r1.x… còn gửi mã tự đặt / kỳ vọng PH-); MA-1.6 tài liệu (user-guide 91 / 02 / 20 là file của anh); Catalog xlsx (máy không có python); ROADMAP đổi trạng thái MA-1.
Cần anh: duyệt nhánh; chốt 4 điểm PROPOSED; backup rồi chạy `MA1_renumber_customer_supplier.sql -v DryRun=0` trên DB dev / staging khi đồng ý.

### UI-1 – Thẻ chỉ số kiểu mới + dải thẻ khách hàng · D-64 · DONE 2026-11-19 (nhánh `feat/MA-1-code-rules`)
Đã làm: `UI.kpi` mới (biểu tượng trái, nhãn + số chính + số phụ = tham số thứ 3 cũ, không chú thích chân, "–" khi rỗng, liên kết có viền khi rê); CSS `.kpis` 4 / 2 / 1 cột, màu `lime` / `gray`, giao diện tối `[data-theme-version=dark]`; ô KPI tùy biến màn KPI giữ kiểu cũ (`.panel .kpi-value`, `.kpi-foot`); rà 52 lời gọi `UI.kpi` (script phân tích tham số) – bỏ chú thích kỹ thuật, giữ số phụ ngắn; biểu tượng mới `check-circle`, `file`. BE: `Customer/GetSummary(branchId, customerType, searchText)`; `GetOverview` + activeContractCount / openComplaintCount / complaintOnTimePct / reconciledOrderCount; suất TB / ngày chỉ tính 7 ngày đã qua. FE: `genericList` nhận `opt.kpis` / `opt.afterKpis`; `crm-customer` 4 thẻ (số liệu máy chủ theo chi nhánh làm việc, DEMO tính tại chỗ); cột Suất TB/ngày ÷ 7; hồ sơ 360 4 thẻ mới. HDSD 02, 13.
Kiểm tra: test 474 / 474 (+1), `npm run build` xanh, `GetSummary` / `GetOverview` qua FE 4300 → API 5299 trên DB dev. Chưa: mở trình duyệt 3 cỡ màn + giao diện tối (cần anh xem).

### UI-2 – Màn chi tiết khách hàng 360 · D-66 · DONE 2026-11-19 (nhánh `feat/MA-1-code-rules`)
BE: `GetOverview` thêm branchName, userAssignName, nearestContractEndDate / daysToContractEnd, overdueComplaintCount, containersAtCustomer, packagingLossRatePct / ThresholdPct, listAlert (CONTRACT_EXPIRING ≤ 30 ngày, COMPLAINT_OVERDUE, PACKAGING_LOSS, ORDER_NOT_LOCKED từ (giờ chốt − 1), NO_DELIVERY_LOCATION, NO_PRIMARY_CONTACT – PROPOSED ngưỡng); mới `Customer/GetTrend(id, from, to)` (đặt = Σ dòng đơn chưa hủy, giao / nhận = POD, phục vụ = đối soát; cơ cấu loại suất; mặc định 30 ngày, ≤ 366); `Order/GetAll` + FromMealDate / ToMealDate (`uspOrder_GetAll` – đã chạy DB dev). Test +1 (InMemory: cảnh báo + xu hướng) +1 (SQL Server: lọc ngày ăn); `full-flow.http` bước 15b.1–15b.3.
FE: `modules/customer360.js` (màn ẩn menu, quyền = crm-customer qua `PERM_ALIAS`, giữ `?id=` trên URL – `KEEP_ID`), phần đầu + nút theo quyền + 4 thẻ (`APP.customerKpis` dùng chung với ngăn xem nhanh) + 10 tab tải khi mở (lọc ngày / trạng thái, bấm dòng mở chứng từ, tải tài liệu lên theo quy tắc adm-document); danh sách khách hàng bấm dòng → màn 360; ngăn xem nhanh có nút **Mở hồ sơ đầy đủ**; CSS lưới 5 / 4 / 3 rồi 5 / 7, xếp dọc màn hẹp, in hồ sơ ẩn menu. HDSD 02.
Chưa: mở thử trình duyệt (DN + CN, 3 cỡ màn); nút Tạo đơn / Ghi phản ánh / Tạo hợp đồng mở màn tương ứng kèm `?customer=` nhưng các màn đó chưa tự điền khách (ghi FE-E); suất TB theo từng điểm giao chưa có (không có API theo điểm).

### FE-E.1 → FE-E.3 – Việc FE tồn đọng (bot) · D-65, D-67 · DONE 2026-11-19 (nhánh `feat/MA-1-code-rules`)
- **FE-E.1 (FE-BOT-1):** `UI.telegramLink` – menu tài khoản → Liên kết Telegram (danh sách bot đã liên kết, Tạo mã 15′, Gỡ); khối dùng chung `UI.telegramBox` với màn Việc của tôi; `adm-employee` nút Tạo mã liên kết bot trên dòng (chỉ khi có quyền Sửa; ngăn chi tiết cũng ẩn nút khi không có quyền). HDSD 12, 20.
- **FE-E.2 (FE-BOT-3) – D-67 anh chọn backend vẽ QR:** NuGet QRCoder 1.6.0 (MIT); `QrCodeRenderer` (SVG cho giao diện, BMP cho PDF – PDFsharp 6 Core không đọc PNG / base64, đã thử); `POST Common/GetQrBatch`, `GET Common/GetFieldQrList` (CHAMCONG / LINE_ / VEH_ / LOC_ / CLEAN, lọc quyền Xem từng màn); PDF phiếu in có ảnh QR mở phiếu trên bot; FE `UI.qrSlot` / `UI.fillQr` / `UI.printFieldQr`, nút In QR ở prod-line, prod-workstation (+ cổng chấm công), log-vehicle, scm-location, qc-cleaning (cần quyền In). Test PrintTests mở rộng. Thử trên DB dev: GetQrBatch ra SVG, GetFieldQrList 153 mã.
- **FE-E.3 (FE-BOT-2):** bỏ ẩn int-channel / int-bot / int-log (máy chủ đã che khóa trong ConfigJson; SecretReference chỉ là tên tham chiếu); int-outbound nút Gửi lại trên dòng Lỗi / Đã hủy; cảnh báo OUTBOUND_FAILED đã trỏ int-outbound (BE3.3); hội thoại đã có (tab trong ngăn định danh / nhân viên – OutboundMessage/GetConversation).
- **Cần anh:** D-68 (quyền màn tích hợp – seed đang cấp Xem cho hầu hết nhóm); máy chạy thử thiếu bí mật `Messaging:LinkSigningKey` (≥ 32 ký tự) nên nút Tạo mã liên kết báo lỗi cấu hình – anh đặt bằng `dotnet user-secrets set Messaging:LinkSigningKey <khóa> --project AppDelicare/AppDelicare.WebApi`.

### UG-1 – mục Cao (C-01, C-06, C-07, C-08) · DONE 2026-11-19 (nhánh `feat/MA-1-code-rules`)
- **C-01:** nút đã có từ GHEP-FE (`gx-order-change`) nhưng hiện cả trước giờ chốt → đổi nhãn **Yêu cầu sửa sau chốt** và chỉ giữ khi `Order/GetDetail.allowedActions` có `requestChange`.
- **C-06:** `adm-user` (chế độ thật) – nút **Tạo người dùng** (tên đăng nhập, hồ sơ người, **Nhóm quyền** chọn nhiều → `User/Insert`, hiện mật khẩu tạm 1 lần + Sao chép), nút **Gán nhóm** trên dòng (`User/GetById` → `User/Update` với ListUserGroupId). **Sửa lỗi BE:** `UserService.Update` không gửi ListUserGroupId (form sửa chung) trước đây xóa hết nhóm → nay giữ nguyên; test UserAccountTests mở rộng.
- **C-07:** bot `BepActions.FinishAsync` = Finish rồi RecordOutput (dừng nếu Finish lỗi); web mẻ Chờ QC chưa có sản lượng có nút **Ghi sản lượng**; test MessagingVanHanh mở rộng.
- **C-08:** hộp kết luận QC đầu vào: ô **Số lượng đạt** khi chọn HOLD (kiểm > 0 và < số nhận trước khi gửi), ô **Xác nhận không rã đông** cho hàng FROZEN; gửi acceptedQuantity / noThawConfirmed.
- HDSD 01, 02, 06, 07, 12, 20 + `90_CHENH_LECH.md` đánh ✅.

### UG-1.4 – C-03, C-04, C-10, C-14, C-21 · DONE 2026-11-19 (nhánh `feat/MA-1-code-rules`)
- **C-10:** `Model/Helper/StatusCatalogEffective.cs` – mã + nhãn trạng thái lấy từ các lớp `*Status.cs` (hằng có `Label(mã) ≠ mã`), cột chưa có lớp giữ theo `StatusCatalog` (STATUS_CATALOG.md); thêm DeliveryOrder, DeliveryLoading, CleaningBatche. `MetaData/GetStatuses`, thống kê trạng thái (`CommonRegistry`), tab Liên quan (`CommonRelations`) dùng danh mục này; giao diện chế độ thật nạp `MetaData/GetStatuses` sau khi đăng nhập (store.js). Test `StatusCatalogEffectiveTests`. HDSD 91 §1 cập nhật (Phiếu nhập *Không đạt*, *Đã xếp lên xe*, thêm Dự báo, Thực đơn, Yêu cầu đổi thực đơn, Nhu cầu NL, Giữ hàng, Xếp hàng lên xe).
- **C-21:** chữ bot – Trợ giúp "Vào ca bằng vị trí / QR", nút Mở bot Điều hành không mở thẳng chấm công, gợi ý tra mã theo số mới; thông báo xác nhận ca ghi rõ đã gửi bot.
- **C-03:** màn engine `sys-import` *Nhập dữ liệu gốc (Excel)* (ghep.js): `ImportExcel/GetTemplateList` lọc theo quyền Nhập của màn đích, **File mẫu** (`GetTemplate`), **Nạp file** (`Import`) → hộp kết quả (đọc / thêm / cập nhật / bỏ qua + bảng dòng lỗi). `sys-import` thêm vào OPEN_SCREENS (delicare-host.ts) – máy chủ vẫn kiểm tra quyền Import. HDSD 01 §4.8b.
- **C-04, C-14:** sửa REVIEW_BUSINESS_FLOWS.md (GetHistory / GetStatusSummary đã gọi; Gửi NCC chỉ đổi trạng thái – gửi email / Zalo là việc sau). 90 đánh ✅ C-03, C-04, C-10, C-14, C-21.

### UG-1.3 – mục TB (C-05, C-09, C-11, C-12, C-13, C-15, C-16, C-20) · DONE 2026-11-19 (nhánh `feat/MA-1-code-rules`)
- **C-05:** crm-order nút **Chốt đơn theo ngày** (quyền Sửa, xác nhận 2 lần) → `Order/LockByMealDate`, báo số đã chốt / chưa xác nhận / chờ duyệt.
- **C-09:** hộp kết thúc nấu: ngưỡng lấy `ProductionBatch/GetDetail.coreTempMinC`, bỏ mặc định 82,5, ô **Hành động khắc phục** khi dưới ngưỡng (gửi CorrectiveAction); bot: máy chủ báo cần khắc phục → hỏi (flow `bat-fix`) → ghi lại kèm CorrectiveAction; test MessagingVanHanh mở rộng.
- **C-11:** hộp phê duyệt thêm PURCHASE_ORDER, ORDER_DEVIATION, ORDER_CHANGE_AFTER_CUTOFF, STOCK_ADJUSTMENT; chi tiết yêu cầu sửa đơn sau chốt có bảng **Đề xuất sửa đơn** (đọc PayloadJson).
- **C-12:** đề nghị mua Nháp → **Sửa dòng nguyên liệu** (Insert / Update / Delete PurchaseRequestLine tuần tự, dừng ở lỗi đầu); PO Nháp đã có Sửa dòng hàng (GHEP-FE).
- **C-13:** đã xử lý ở FE-E.3 (int-* hiện lại theo ma trận); siết quyền D-68 chờ anh.
- **C-15:** ngăn mẻ thêm tab **Công đoạn** (Bắt đầu / Xong theo thứ tự – StartStage / FinishStage). **Chưa:** nút công đoạn trên bot `/me`.
- **C-16:** `Store.canApprove(màn)` = mục `wf-inbox?type=<màn>` Xem + Sửa (giống PermissionService Approve); ẩn Duyệt kế hoạch (prod-plan), chuyển thực đơn sang Đã duyệt (menu-menu), Khóa & áp dụng công thức (menu-recipe).
- **C-20:** nút chuyển DEMO trong hộp Hướng dẫn chỉ cho quản trị; flow-e2e ẩn nút demo ở chế độ thật.
- HDSD 00, 02, 04, 07, 08, 12, 13 + 90 (✅).
