# DECISIONS – Quyết định đã chốt, câu hỏi chờ anh, GAP mới

## A. Đã chốt (PO đồng ý)
| Mã | Quyết định | Ngày |
|---|---|---|
| D-13 | Các entity không có bảng trong 124 bảng (Area, Region, Branch, Department, Leads, Opportunity, Products, AppConfig, Employees): **chuyển `_obsolete`**, không cần xét Angular (D-20). | 2026-11-14 |
| D-01 | Lược đồ chuẩn là **124 bảng `dbo`** trong `20261113_structure_db_appdelicare.sql`. Lược đồ 42 bảng `dm/vh/ht/bc` (F1) chỉ dùng làm nguồn đọc khi chuyển dữ liệu. | 2026-11-14 |
| D-02 | Đơn vị tính: dùng `UnitId` → `UnitMeasure`. | 2026-11-13 |
| D-03 | `ObjectMenu` = menu chức năng hệ thống (P0 – phân quyền). Vai trò = `UserGroup`. `OrganizationUnit` = chi nhánh / phòng ban. | 2026-11-13 |
| D-04 | Bỏ qua nghiệp vụ các bảng bệnh viện (Patient…) nhưng **không xóa** bảng. | 2026-11-13 |
| D-07 | Cặp cột trùng: dùng cột mới (DEPRECATED_COLUMNS.md). Mã đề xuất viết PascalCase, giữ nguyên chữ hoa/thường của nguồn. Pha P0–P8 theo mockup. | 2026-11-13 |
| D-08 | Danh mục trạng thái theo STATUS_CATALOG.md (đề xuất được duyệt; vẫn mang nhãn PROPOSED cho tới khi BA chốt). | 2026-11-13 |
| D-21 | **Điểm bán (bộ phận 1.5, bước 8) hiện là mô hình (a)**: điểm phát suất / căng tin tại khách hàng, phục vụ suất theo hợp đồng → dùng `CustomerDeliveryLocation`, POD, `OrderReconciliation` (suất thực tế), phản ánh. Không thêm bảng, làm trong R1. **Bán lẻ (b)** có thể mở sau → GAP-POS-01, R2. | 2026-11-14 (anh chốt) |
| D-22 | Ghi GAP, làm ở **Release 2**: bảo trì thiết bị (GAP-MNT-01), CRM / marketing – lead, cơ hội (GAP-CRM-01), sổ NCR/CAPA riêng (GAP-QA-01). R1 chỉ có báo cáo NCR tổng hợp từ bảng sẵn có. | 2026-11-14 (anh chốt) |
| D-23 | **Kế toán, công nợ, thanh toán, lương, BHXH đang theo dõi trên MISA** → không xây trong hệ thống. R1 không làm. R2 có thể làm xuất dữ liệu sang MISA (doanh thu theo hợp đồng / đơn, giá vốn NVL xuất kho, chấm công) – GAP-ACC-01. | 2026-11-14 (anh chốt) |
| D-24 | Xuất kho: **FEFO** cho nguyên liệu có hạn dùng, **FIFO** cho hàng không quản lý hạn – theo cột `InventoryPolicie.UseFefo` của từng nguyên liệu / kho. Mặc định FEFO. | 2026-11-14 (em đề xuất theo sơ đồ vận hành ghi FIFO) |
| D-25 | Cơ cấu tổ chức, nhóm quyền, ma trận quyền seed theo `docs/claude/ORG_STRUCTURE.md` (sơ đồ tổ chức thật). Ma trận quyền là PROPOSED, anh duyệt khi UAT-1. | 2026-11-15 |
| D-20 | **Phạm vi hiện tại CHỈ BACKEND (.NET + SQL), làm theo `DELICARE_API_Specification.md`.** Không đọc / rà soát / sửa / build `app-delicare-client/` (Angular chưa đủ màn hình như mockup). Không cần tương thích với Angular hiện tại. Xong backend anh sẽ giao làm frontend. | 2026-11-14 (anh chốt) |
| D-16 | **App Apps Script + Google Sheets là bản DEMO, chưa triển khai thực tế.** Không có dữ liệu hay người dùng thật. Release 1 (backend .NET, sau đó frontend) là hệ thống vận hành đầu tiên: không chạy song song, không đồng bộ, không cắt chuyển; dữ liệu danh mục thật nhập mới qua T49. Bản demo chỉ là tài liệu tham khảo nghiệp vụ. | 2026-11-14 (anh chốt) |
| D-17 | **Ưu tiên Release 1:** lõi .NET đủ API theo mockup / spec trước, rồi frontend; agent / gateway / Obsidian / chuyển dữ liệu toàn bộ để Release 2. | 2026-11-14 (anh chốt) |
| D-14 | **Đã rõ, không cần chạy script nâng cấp:** T00 kiểm tra (chỉ đọc) DB dev `AppDelicare` trên `(localdb)MSSQLLocalDB` có đủ **124 bảng `dbo`**; tên bảng, cột, kiểu, NULL khớp 100% DDL 20261113 (chỉ khác `sysname` ≡ `nvarchar(128)`). Thêm bảng hệ thống `sysdiagrams`. Không có FK vật lý. | 2026-11-14 (kết quả T00) |
| D-28 | (T03) Lỗi của route `/api/v1` cũng trả **envelope** `{success:false, data:null, message, errors:[{code, field, message}], traceId}` kèm mã HTTP thật (không dùng ProblemDetails). | 2026-11-14 (anh chốt) |
| D-09 | Kiến trúc 4 lớp: nâng cấp dần solution hiện có (không viết lại); sổ chỉ ghi thêm; hạn chót do job lõi chạy, agent chỉ thông báo / xin xác nhận; ToolGateway là service riêng, mỗi agent một tài khoản dịch vụ. | 2026-11-14 – **giả định đã đồng ý khi anh yêu cầu giao việc cho Claude Code; anh sửa tại đây nếu khác** |
| D-10 | Quy tắc nghiệp vụ theo bản demo Apps Script (BUSINESS_RULES.md) khi mâu thuẫn với mockup / BRD: nhiệt độ lõi 70°C, giao 60°C, ca TRUA / TOI / DEM_23H / SANG_6H, kết quả QC DAT / DAT_MOT_PHAN / KHONG_DAT, KPI 95 / 55 / 3%. | 2026-11-14 – giả định như D-09 |

## B. Chờ anh chốt (Claude Code: task phụ thuộc thì để BLOCKED, làm task khác)
| Mã | Câu hỏi | Đề xuất của em | Ảnh hưởng |
|---|---|---|---|
| D-29 | (T48.1) **Mật khẩu đang băm MD5 không salt** (CODE_AUDIT A19). | Làm 1 task nhỏ trước go-live: băm PBKDF2 (có sẵn trong .NET) + tự nâng hash khi user đăng nhập thành công, tương thích hash cũ. | T48.2 |
| D-26 | (T00) **Endpoint nguy hiểm `GET api/AuditChange/DeleteData`** (CODE_AUDIT A14): mọi user đăng nhập đều xóa vật lý được bản ghi đã xóa mềm ở mọi bảng, kể cả sổ append-only. SP `uspSystem_BackupDB` thực chất là RESTORE ghi đè DB (A16). | Gỡ endpoint (và `LocationTracking/DeleteData`) – không cần xét Angular (D-20). `uspSystem_BackupDB`: giữ file tham khảo, không chạy; xóa khỏi DB dev do anh quyết. | T05 |
| D-27 | (T04) **AutoMapper 13.0.1 có 3 lỗ hổng DoS mức cao.** Bản vá chỉ từ 15.1.4, mà từ v15 AutoMapper là **license thương mại**. | Thay AutoMapper bằng Mapster (MIT) / map tay khi làm CodeGen T05; hoặc anh mua / đăng ký license rồi em nâng 15.1.4+. Ngắn hạn giữ 13.0.1 (chỉ map entity nội bộ). | T05 |
| D-05 | Hangfire lưu job ở đâu? Luật cấm tạo bảng trong DB nghiệp vụ. | Một **database riêng** `DelicareOps` cho Hangfire. Trước khi chốt thì dùng InMemory. | T23 |
| D-06 | Định danh khách Zalo: `UserExternalIdentity` cần `UserId`, mà khách không phải User. | Tạo User loại "khách" (không có quyền web) cho mỗi đầu mối khách; hoặc chấp nhận GAP-INT-01. | T41 |
| D-11 | Ngưỡng giá trị PO cần GĐ duyệt? | PROPOSED: > 50 triệu VND thì GĐ duyệt, còn lại TP vận hành. | T15 |
| D-12 | MRP có cộng tồn an toàn không? `InventoryPolicie` đã có `SafetyStock`, `ReorderPoint`, `MinStock`/`MinimumStock`. (câu hỏi còn mở trong tài liệu API) | Có: thiếu hụt = nhu cầu + SafetyStock − khả dụng. | T14 |
| D-19 | Ánh xạ buổi ăn (SÁNG / TRƯA / TỐI / ĐÊM trong file menu thật) ↔ ca giao (SANG_6H / TRUA / TOI / DEM_23H của bản demo). Dùng bộ nào làm `Shift.Code`? | Dùng `SANG_6H`, `TRUA`, `TOI`, `DEM_23H` làm mã; tên hiển thị Sáng / Trưa / Tối / Đêm. | T21.1 |
| D-15 | Triển khai lên đâu: IIS trên Windows Server hay Docker (Linux)? Domain, máy chủ SQL production? **Anh chưa quyết định (2026-11-14).** | Không chặn task: T48.1 chuẩn bị cả IIS lẫn Docker ở mức tối thiểu, staging chạy trên máy anh. Chỉ T48.2 (production) cần chốt. | T48.2 |
| D-18 | Múi giờ lưu DB: DDL lẫn lộn `getdate()` / `sysdatetime()` (giờ máy chủ) và 17 cột `sysutcdatetime()` (UTC). | Lưu **giờ VN** cho mọi cột; app luôn tự gán thời gian, không dựa default; máy chủ đặt múi giờ VN. PROPOSED – anh xác nhận. | T02, T05 |

## C. GAP (thiếu chỗ chứa dữ liệu – KHÔNG tự thêm cột)
| Mã | Mô tả | Cách tạm | Trạng thái |
|---|---|---|---|
| GAP-CFG-01 | Không có bảng cấu hình chung (AppConfig không có trong 124 bảng) | `appsettings` → section `BusinessRules` | PROPOSED – BA/PO CONFIRM |
| GAP-PKG-01 | Ngưỡng thất thoát hộp riêng theo khách | Mặc định 3%; ngoại lệ theo khách đặt trong `Customer.ExternalData` (JSON) nếu PO đồng ý | PROPOSED |
| GAP-PRD-02 | `ProductionPlan` không có cột phiên bản | `Note` có `v=<n>` + `ApprovalRequest.PayloadJson` | PROPOSED |
| GAP-INT-01 | Liên kết khách Zalo ↔ CustomerContact | Xem D-06 | PROPOSED |
| GAP-SUP-01 | Điểm đánh giá NCC (giá / chất lượng / đúng hẹn) | Tính từ SupplierIssue / PO lịch sử, không lưu | PROPOSED |
| GAP-POS-01 | Bán lẻ tại điểm bán (thu tiền, POS, doanh số ca) | Chưa làm; cần bảng bán hàng do PO duyệt bổ sung lược đồ | R2 – D-21 |
| GAP-MNT-01 | Thiết bị, lịch bảo trì, sửa chữa, kế hoạch thiết bị | Chưa làm | R2 – D-22 |
| GAP-CRM-01 | Lead / cơ hội bán hàng / marketing (code cũ Leads, Opportunity không có bảng) | Chưa làm | R2 – D-22 |
| GAP-QA-01 | Sổ NCR / CAPA riêng (người chịu trách nhiệm, hạn đóng, hiệu lực) | R1: báo cáo tổng hợp từ SupplierIssue, CustomerComplaint, CorrectiveAction của checklist / nhiệt độ | R2 – D-22 |
| GAP-ACC-01 | Kết nối / xuất dữ liệu sang MISA | Chưa làm | R2 – D-23 |
| (thêm mới ở dưới) | | | |

Ngoài ra còn GAP-F / C / Q / RD trong `docs/reference/GAP_REGISTER.md` (từ giai đoạn phân tích).

## E. Việc cho frontend (ghi lại, KHÔNG làm – chờ anh giao)
<!-- Mẫu: | FE-NN | màn hình / file Angular | việc cần đổi | lý do (API mới / thay đổi) | -->

## D. GAP mới phát sinh khi code (Claude Code ghi tiếp vào đây)
<!-- Mẫu: | GAP-XXX-NN | mô tả | cách tạm | PROPOSED – BA/PO CONFIRM | task |  -->
| Mã | Mô tả | Cách tạm | Trạng thái | Task |
|---|---|---|---|---|
| GAP-AUD-01 | `AuditChange` không có cột loại người thao tác (USER / SYSTEM / AGENT), mã agent, CorrelationId. | Ghi ở dòng đầu cột `Changes`: `- Actor: USER:<id> | CorrelationId: <id>`. | PROPOSED – BA/PO CONFIRM | T02 |
| GAP-AUD-02 | Job hệ thống cần một User dịch vụ để điền CreatedBy (cột `uniqueidentifier`). | Cấu hình `SystemUsers:JobUserId` (trống = job không được ghi). Tạo 1 bản ghi `User` dịch vụ khi làm T23. | PROPOSED – BA/PO CONFIRM | T02, T23 |
| GAP-IDEM-01 | `IntegrationLog` không có cột lưu body response → gửi lại cùng `Idempotency-Key` chỉ trả lại mã HTTP + `Idempotency-Replayed: true` + `originalTraceId`, không trả lại `data` (vd. Id vừa tạo). | Client tra lại bằng GET danh sách / mã. | PROPOSED – BA/PO CONFIRM | T03 |
| GAP-IDEM-02 | Hai request cùng key chạy song song: IntegrationLog là sổ chỉ ghi thêm (không có trạng thái "đang chạy"), không có UNIQUE trên key. | Khóa trong tiến trình (đủ cho 1 instance). Chạy nhiều instance: dùng `sp_getapplock` (T48). | PROPOSED | T03, T48 |
| GAP-API-01 | `If-Match` có bắt buộc cho PUT / DELETE không? | Hiện **không bắt buộc**: có gửi mà lệch → 409; không gửi → cho qua. | PROPOSED – BA/PO CONFIRM | T03 |
| GAP-HR-01 | `Shift.Status` chưa có danh mục; `Shift` có giờ bắt đầu = giờ kết thúc. | Status: `Active` / `Inactive` (mặc định Active); bắt đầu = kết thúc → 422 BUSINESS_RULE. | PROPOSED – BA/PO CONFIRM | T03, T21.1 |
| GAP-PERM-01 | `UserRole` không có cột quyền **duyệt** (cột "A" của ma trận ORG_STRUCTURE). | `[Permission(screen, Approve)]` kiểm tra nhóm của user ∈ `appsettings Permissions:ApproverGroups` theo cột ma trận (CodeGen in ra danh sách từ ORG_STRUCTURE). | PROPOSED – BA/PO CONFIRM | T07 |
| GAP-PERM-02 | Ma trận quyền chỉ có V / E / A / X. | Quy đổi: V → AllowAccess; **E → Add + Edit + Delete + Import**; X → Print + Export. Ghi chú trong ô (vd. "V (đề nghị xuất)") áp cho cả nhóm màn hình – tinh chỉnh từng màn khi UAT-1. | PROPOSED – BA/PO CONFIRM | T07 |
| GAP-PERM-03 | `UserGroup` không có cột Code. | `UserGroup.Name` = mã vai trò (CEO, OPS-SCM…, ORG_STRUCTURE mục 2). 20 nhóm cũ trong DB dev ("1"…"17", "123", "Administrator") **không xóa** – anh dọn khi UAT. | PROPOSED | T07 |
| GAP-PERM-04 | Bảng không có màn hình riêng trong mockup (Patient, Person, DocumentFile, bảng hệ thống…). | Quyền theo màn `dbx-explorer` (Database Explorer). | PROPOSED – BA/PO CONFIRM | T07 |
| GAP-INV-01 | Bút toán đảo: loại giao dịch và số chứng từ. | `TransactionType = ADJUSTMENT`, `ReferenceType = REVERSAL`, `ReferenceId` = dòng gốc, `Reason` bắt buộc; số = số gốc + "-R" (mã chuẩn ở T11). Không đảo 2 lần, không đảo bút toán đảo; đảo phiếu nhập đã xuất bớt → 422 INSUFFICIENT_STOCK. Quyền: `Edit` trên `#/scm-inv-txn`. | PROPOSED – BA/PO CONFIRM | T08 |
| GAP-DEP-01 | DB staging mới cần schema + tài khoản admin ban đầu. | `Deploy-Database.ps1 -CreateSchemaIfEmpty` chạy **DDL chuẩn của PO** chỉ khi DB **rỗng** (không bao giờ sửa bảng có sẵn); admin tạo bằng `DB/seed/00_Admin_seed.sql`, mật khẩu truyền lúc chạy. Trên máy anh đã tạo `AppDelicare_Staging` (LocalDB) theo cách này. | PROPOSED – anh xác nhận cách dựng DB staging / UAT | T48.1 |
| GAP-API-02 | Quyền của API dropdown `/api/v1/meta/lookups/{table}`. | Chỉ cần đăng nhập (dropdown dùng chung nhiều màn; chỉ trả id / nhãn / mã / trạng thái). **Chặn** các bảng: User, UserClaim, UserRole, UserExternalIdentity, Person, AuditChange, IntegrationLog, LocationTracking, Inbound / OutboundMessage, BotConfiguration, CoreDataMergeConflict, Patient*, MedicalExamination, MedicineWarehouse, bảng _Legacy. Status NULL coi như Active. | PROPOSED – BA/PO CONFIRM | T10 |
