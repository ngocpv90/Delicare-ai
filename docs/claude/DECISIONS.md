# DECISIONS – Quyết định đã chốt, câu hỏi chờ anh, GAP mới

## A. Đã chốt (PO đồng ý)
| Mã | Quyết định | Ngày |
|---|---|---|
| D-01 | Lược đồ chuẩn là **124 bảng `dbo`** trong `20261113_structure_db_appdelicare.sql`. Lược đồ 42 bảng `dm/vh/ht/bc` (F1) chỉ dùng làm nguồn đọc khi chuyển dữ liệu. | 2026-11-14 |
| D-02 | Đơn vị tính: dùng `UnitId` → `UnitMeasure`. | 2026-11-13 |
| D-03 | `ObjectMenu` = menu chức năng hệ thống (P0 – phân quyền). Vai trò = `UserGroup`. `OrganizationUnit` = chi nhánh / phòng ban. | 2026-11-13 |
| D-04 | Bỏ qua nghiệp vụ các bảng bệnh viện (Patient…) nhưng **không xóa** bảng. | 2026-11-13 |
| D-07 | Cặp cột trùng: dùng cột mới (DEPRECATED_COLUMNS.md). Mã đề xuất viết PascalCase, giữ nguyên chữ hoa/thường của nguồn. Pha P0–P8 theo mockup. | 2026-11-13 |
| D-08 | Danh mục trạng thái theo STATUS_CATALOG.md (đề xuất được duyệt; vẫn mang nhãn PROPOSED cho tới khi BA chốt). | 2026-11-13 |
| D-20 | **Phạm vi hiện tại CHỈ BACKEND (.NET + SQL).** Không sửa / build `app-delicare-client/` (Angular); frontend anh giao riêng sau. Route cũ Angular đang gọi phải giữ tương thích. | 2026-11-14 (anh chốt) |
| D-16 | **App Apps Script + Google Sheets là bản DEMO, chưa triển khai thực tế.** Không có dữ liệu hay người dùng thật. Release 1 (.NET + Angular) là hệ thống vận hành đầu tiên: không chạy song song, không đồng bộ, không cắt chuyển; dữ liệu danh mục thật nhập mới qua T49. Bản demo chỉ là tài liệu tham khảo nghiệp vụ. | 2026-11-14 (anh chốt) |
| D-17 | **Ưu tiên Release 1:** lõi .NET + Angular theo mockup, triển khai cho người dùng trước; agent / gateway / Obsidian / chuyển dữ liệu toàn bộ để Release 2. | 2026-11-14 (anh chốt) |
| D-09 | Kiến trúc 4 lớp: nâng cấp dần solution hiện có (không viết lại); sổ chỉ ghi thêm; hạn chót do job lõi chạy, agent chỉ thông báo / xin xác nhận; ToolGateway là service riêng, mỗi agent một tài khoản dịch vụ. | 2026-11-14 – **giả định đã đồng ý khi anh yêu cầu giao việc cho Claude Code; anh sửa tại đây nếu khác** |
| D-14 | **Đã rõ, không cần chạy script nâng cấp:** T00 kiểm tra (chỉ đọc) DB dev `AppDelicare` trên `(localdb)\MSSQLLocalDB` (đúng connection string trong appsettings.json) có đủ **124 bảng `dbo`**, tên bảng, tên cột, kiểu, NULL khớp 100% DDL 20261113 (chỉ khác `sysname` ≡ `nvarchar(128)` ở `CoreDataMergeConflict`). Thêm bảng hệ thống `sysdiagrams` của SSMS. Không có FK vật lý. | 2026-11-14 (kết quả kiểm tra T00) |
| D-10 | Quy tắc nghiệp vụ theo bản demo Apps Script (BUSINESS_RULES.md) khi mâu thuẫn với mockup / BRD: nhiệt độ lõi 70°C, giao 60°C, ca TRUA / TOI / DEM_23H / SANG_6H, kết quả QC DAT / DAT_MOT_PHAN / KHONG_DAT, KPI 95 / 55 / 3%. | 2026-11-14 – giả định như D-09 |

## B. Chờ anh chốt (Claude Code: task phụ thuộc thì để BLOCKED, làm task khác)
| Mã | Câu hỏi | Đề xuất của em | Ảnh hưởng |
|---|---|---|---|
| D-05 | Hangfire lưu job ở đâu? Luật cấm tạo bảng trong DB nghiệp vụ. | Một **database riêng** `DelicareOps` cho Hangfire. Trước khi chốt thì dùng InMemory. | T23 |
| D-06 | Định danh khách Zalo: `UserExternalIdentity` cần `UserId`, mà khách không phải User. | Tạo User loại "khách" (không có quyền web) cho mỗi đầu mối khách; hoặc chấp nhận GAP-INT-01. | T41 |
| D-11 | Ngưỡng giá trị PO cần GĐ duyệt? | PROPOSED: > 50 triệu VND thì GĐ duyệt, còn lại TP vận hành. | T15 |
| D-12 | MRP có cộng tồn an toàn không? `InventoryPolicie` đã có `SafetyStock`, `ReorderPoint`, `MinStock`/`MinimumStock`. (câu hỏi còn mở trong tài liệu API) | Có: thiếu hụt = nhu cầu + SafetyStock − khả dụng. | T14 |
| D-21 | (T00) **Endpoint nguy hiểm `GET api/AuditChange/DeleteData`** (CODE_AUDIT A14): mọi user đăng nhập đều xóa vật lý được bản ghi đã xóa mềm ở mọi bảng. Và SP `uspSystem_BackupDB` thực chất là RESTORE ghi đè DB (A16). | Gỡ endpoint (trả 404 / bỏ route) trong T01 hoặc T02 vì Angular không cần chức năng này (em sẽ kiểm tra Angular có gọi không trước khi gỡ). `uspSystem_BackupDB`: chỉ giữ file tham khảo, không chạy; việc xóa SP khỏi DB dev do anh quyết. | T01/T02 |
| D-19 | Ánh xạ buổi ăn (SÁNG / TRƯA / TỐI / ĐÊM trong file menu thật) ↔ ca giao (SANG_6H / TRUA / TOI / DEM_23H của bản demo). Dùng bộ nào làm `Shift.Code`? | Dùng `SANG_6H`, `TRUA`, `TOI`, `DEM_23H` làm mã; tên hiển thị Sáng / Trưa / Tối / Đêm. | T21.1 |
| D-15 | Triển khai lên đâu: IIS trên Windows Server hay Docker (Linux)? Domain, máy chủ SQL production? **Anh chưa quyết định (2026-11-14).** | Không chặn task: T48.1 chuẩn bị cả IIS lẫn Docker ở mức tối thiểu, staging chạy trên máy anh. Chỉ T48.2 (production) cần chốt. | T48.2 |
| D-18 | Múi giờ lưu DB: DDL lẫn lộn `getdate()` / `sysdatetime()` (giờ máy chủ) và 17 cột `sysutcdatetime()` (UTC). | Lưu **giờ VN** cho mọi cột; app luôn tự gán thời gian, không dựa default; máy chủ đặt múi giờ VN. PROPOSED – anh xác nhận. | T02, T05 |
| D-22 | (T04) **AutoMapper 13.0.1 có 3 lỗ hổng DoS mức cao** (đệ quy không giới hạn khi map). Bản vá chỉ có từ **15.1.4** trở lên, mà từ v15 AutoMapper chuyển sang **license thương mại** (cần license key; có bản Community miễn phí cho doanh nghiệp nhỏ nhưng phải đăng ký). | Ngắn hạn: giữ 13.0.1 (rủi ro thấp vì chỉ map entity nội bộ, không map đồ thị do người dùng gửi lên). Dài hạn: (a) anh đăng ký license AutoMapper rồi em nâng 15.1.4+, **hoặc** (b) thay AutoMapper bằng Mapster (MIT) / map tay khi làm CodeGen T05. Em đề xuất (b) gộp vào T05. | T05 |
| D-13 | Các entity không có bảng (Area, Region, Branch, Department, Leads, Opportunity, Products, AppConfig, Employees): Angular / chức năng nào đang dùng? | Chuyển `_obsolete` sau khi xác minh. | T05 |

## C. GAP (thiếu chỗ chứa dữ liệu – KHÔNG tự thêm cột)
| Mã | Mô tả | Cách tạm | Trạng thái |
|---|---|---|---|
| GAP-CFG-01 | Không có bảng cấu hình chung (AppConfig không có trong 124 bảng) | `appsettings` → section `BusinessRules` | PROPOSED – BA/PO CONFIRM |
| GAP-PKG-01 | Ngưỡng thất thoát hộp riêng theo khách | Mặc định 3%; ngoại lệ theo khách đặt trong `Customer.ExternalData` (JSON) nếu PO đồng ý | PROPOSED |
| GAP-PRD-02 | `ProductionPlan` không có cột phiên bản | `Note` có `v=<n>` + `ApprovalRequest.PayloadJson` | PROPOSED |
| GAP-INT-01 | Liên kết khách Zalo ↔ CustomerContact | Xem D-06 | PROPOSED |
| GAP-SUP-01 | Điểm đánh giá NCC (giá / chất lượng / đúng hẹn) | Tính từ SupplierIssue / PO lịch sử, không lưu | PROPOSED |
| (thêm mới ở dưới) | | | |

Ngoài ra còn GAP-F / C / Q / RD trong `docs/reference/GAP_REGISTER.md` (từ giai đoạn phân tích).

## E. Việc cho frontend (ghi lại, KHÔNG làm – chờ anh giao)
<!-- Mẫu: | FE-NN | màn hình / file Angular | việc cần đổi | lý do (API mới / thay đổi) | -->

## D. GAP mới phát sinh khi code (Claude Code ghi tiếp vào đây)
<!-- Mẫu: | GAP-XXX-NN | mô tả | cách tạm | PROPOSED – BA/PO CONFIRM | task |  -->
| Mã | Mô tả | Cách tạm | Trạng thái | Task |
|---|---|---|---|---|
| GAP-AUD-01 | `AuditChange` không có cột loại người thao tác (USER / SYSTEM / AGENT), mã agent, CorrelationId. | Ghi ở **dòng đầu cột `Changes`**: `- Actor: USER:<id> | CorrelationId: <id>`. | PROPOSED – BA/PO CONFIRM | T02 |
| GAP-AUD-02 | Job hệ thống cần một User dịch vụ để điền CreatedBy (cột `uniqueidentifier`). | Cấu hình `SystemUsers:JobUserId` (trống = job không được ghi). Anh tạo 1 bản ghi `User` dịch vụ (vd. `system.job`, không cho đăng nhập web) khi làm T23. | PROPOSED – BA/PO CONFIRM | T02, T23 |
