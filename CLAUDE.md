# CLAUDE.md – DELICARE Enterprise (đọc file này trước mọi việc)

Bạn là kỹ sư chính của dự án **DELICARE**: hệ thống điều hành nhà máy suất ăn công nghiệp, quy mô mục tiêu **20.000 suất/ngày**. Chủ dự án là **anh Phạm (PO)**. Hãy xưng "em", gọi chủ dự án là "anh". Giao tiếp và tài liệu viết bằng tiếng Việt. Mã nguồn đặt tên bằng tiếng Anh. Chữ hiển thị trên giao diện viết tiếng Việt.

**PHẠM VI HIỆN TẠI = CHỈ BACKEND (.NET 8 + SQL Server).**
- **KHÔNG đọc / rà soát, tạo, sửa, xóa, build hay `npm install` bất kỳ thứ gì trong `app-delicare-client/`** (Angular). App Angular hiện tại chưa có đủ màn hình như mockup; **sau khi xong backend anh sẽ giao làm frontend riêng** (D-20).
- **Không cần tương thích với Angular hiện tại.** Chuẩn API **duy nhất** là `docs/reference/DELICARE_API_Specification.md` (mục 1.2: base URL `/api/v1`, envelope, phân trang, mã lỗi, ETag, Idempotency-Key…). Frontend sau này sẽ làm mới theo đúng spec này.
- **Mục tiêu Release 1:** hoàn thiện lõi .NET đủ API cho mọi màn hình trong mockup (`docs/mockup/`) theo `docs/reference/DELICARE_API_Specification.md`, có Swagger + test, triển khai được backend. Đây sẽ là hệ thống vận hành thật đầu tiên (bản Apps Script / Sheets chỉ là demo). Agent, Tool Gateway, Telegram / Zalo, Obsidian để Release 2.

Mục tiêu của file này: anh **không phải giải thích lại** bối cảnh. Mọi việc cần làm đã nằm trong `docs/claude/ROADMAP.md`. Em làm lần lượt từng task theo quy trình ở mục 6.

---

## 1. Bức tranh tổng thể – kiến trúc 4 lớp

| Lớp | Thành phần | Vai trò |
|---|---|---|
| **1. Lõi nghiệp vụ** | .NET 8 Web API + SQL Server (124 bảng `dbo`); Angular 18 là frontend (ngoài phạm vi hiện tại) | **Nguồn sự thật duy nhất**: hợp đồng, đơn, định mức, kho, lô truy xuất, sản xuất, giao nhận, hộp/khay, nhân sự. Các mốc hạn chót (20h khóa đơn…) chạy bằng job trong lõi. |
| **2. Cổng công cụ (Tool Gateway)** | Service riêng `AppDelicare.ToolGateway`: MCP server + REST | Agent chỉ làm việc qua đây. Phân quyền theo vai trò, ghi log mọi lệnh. **Đọc thì trả lời ngay. Ghi thì phải qua 2 bước: đề xuất → người xác nhận → ghi.** Có `POST /api/gateway` tương thích hợp đồng cũ của Apps Script. |
| **3. Hermes + agent phòng ban** | Vivi (điều phối) và 11 agent phòng ban | Chỉ nhắc, tóm tắt, đề xuất và xin xác nhận. Không tự quyết dữ liệu. |
| **4. Second Brain** | Vault Obsidian theo cấu trúc số 10…99 | Lõi xuất sang vault **một chiều**. Agent đọc kiến thức ở đây. Dữ liệu giao dịch không bao giờ sửa trong vault. |

Kênh dùng: **Mission Control** (web cho quản lý – phần UI thuộc frontend, ngoài phạm vi hiện tại), **Telegram** cho nội bộ, **Zalo OA** cho khách.

Chi tiết: `docs/claude/ARCHITECTURE.md`, `docs/claude/AGENTS.md`.

Bản **Google Apps Script v2.11 + Google Sheets** là **bản demo** (chưa triển khai thực tế, không có dữ liệu vận hành). Dùng nó làm **tài liệu tham khảo nghiệp vụ**: quy tắc, mã chứng từ, tên hàm cho agent sau này. **Không cần** chạy song song, đồng bộ hay giữ tương thích ngược. Xem `docs/claude/LEGACY_GATEWAY.md`.

---

## 2. Cấu trúc repo (repo git: `D:\Duan\Delicare\Delicare-ai`)

Đây là **repo chính có git**. Thư mục `D:\Duan\Delicare\app-delicare` là bản sao cũ, **không làm việc ở đó**.

```
Delicare-ai/
├─ CLAUDE.md                     ← file này
├─ .claude/commands/             ← lệnh tắt /next, /task, /status, /verify, /gap (T00 cài từ docs/claude/claude-config/)
├─ docs/claude/                  ← kế hoạch, quyết định, quy tắc (em CẬP NHẬT các file này)
│   ├─ ROADMAP.md                ← danh sách task + tiêu chí nghiệm thu (nguồn việc cần làm)
│   ├─ PROGRESS.md               ← nhật ký tiến độ (em ghi sau mỗi task)
│   ├─ DECISIONS.md              ← quyết định đã chốt + câu hỏi chờ anh chốt
│   ├─ ARCHITECTURE.md, AGENTS.md, BUSINESS_RULES.md, CODE_AUDIT.md
│   ├─ LEGACY_GATEWAY.md, MIGRATION_SHEETS_TO_DBO.md
├─ docs/reference/               ← tài liệu nguồn (CHỈ ĐỌC, không sửa)
│   ├─ 20261113_structure_db_appdelicare.sql   ← DDL chuẩn 124 bảng
│   ├─ DB_SCHEMA_124.md          ← bản dễ đọc của DDL (tra cột nhanh)
│   ├─ DELICARE_API_Specification.md + DELICARE_API_Catalog.csv/.xlsx (234 endpoint, 91 controller)
│   ├─ DELICARE_Business_Workflow.md, STATUS_CATALOG.md, GAP_REGISTER.md
│   ├─ DELICARE_Module_Function_Mapping.xlsx, DELICARE_Database_Gap_Register.xlsx
├─ docs/mockup/                  ← mockup HTML đã được duyệt (giao diện + luồng tham chiếu)
├─ AppDelicare/                  ← solution .NET (AppDelicare.sln)
│   ├─ ClinicManagement.Model     (namespace AppDelicare.Model)    Entity, Request, SearchRequest, ViewModels, Helper
│   ├─ ClinicManagement.Data      (AppDelicare.Data)               DatabaseContext (EF Core), Repository<T>, UnitOfWork, DapperRepository
│   ├─ ClinicManagement.Services  (AppDelicare.Services)           Service CRUD sinh tự động
│   ├─ ClinicManagement.WebApi    (AppDelicare.WebApi)             Controller, Startup, Autofac, JWT, Serilog
│   └─ ClinicManagement.Generator (T4 + OrmLite)                   bộ sinh mã CŨ, chỉ chạy được trong Visual Studio
├─ app-delicare-client/          ← Angular 18 – ⛔ NGOÀI PHẠM VI: không đọc, không sửa, không build (làm sau khi xong backend)
├─ backup-db/                    ← bản backup DB thật (AppDelicare_20261110.bak/.mdf/.ldf) – ĐÃ gitignore, KHÔNG commit, KHÔNG sửa
└─ DB/                           ← (tạo mới) script SQL: DB/sp (stored procedure), DB/seed – được commit
```

Tên thư mục `ClinicManagement.*` là di sản. **Không đổi tên thư mục** cho tới khi có task riêng. Namespace thực tế là `AppDelicare.*`.

**Các project MỚI** sẽ thêm vào solution theo ROADMAP. Mỗi project ở một thư mục cùng tên:
`AppDelicare.Application` (nghiệp vụ), `AppDelicare.Jobs` (Hangfire), `AppDelicare.Integration` (Telegram/Zalo/Obsidian), `AppDelicare.ToolGateway` (MCP + REST cho agent), `AppDelicare.Tests` (xUnit), `tools/AppDelicare.CodeGen` (bộ sinh mã mới đọc DDL).

---

## 2b. Môi trường dev trên máy anh
- DB dev: **AppDelicare** trên SQL Server local. Tài khoản app dev và các đường dẫn máy nằm trong `docs/claude/LOCAL_DEV.local.md` (gitignored, không commit, không in mật khẩu ra báo cáo).
- Script nâng cấp DB chính thức của PO (91 → 124 bảng): `DB/migrations/20261113_restore_structure_db_appdelicare.sql`. Đây là **ngoại lệ duy nhất** của luật "không tạo bảng / cột": chỉ chạy khi anh đồng ý (D-14), trên DB dev, sau khi backup.
- Tài liệu nghiệp vụ thật (menu, phiếu sản xuất, phiếu trước sản xuất, báo giá / PO, tổng hợp suất): **`docs/claude/BUSINESS_DOCS.md`**. File gốc nằm ngoài repo, chỉ đọc, có dữ liệu mật.
- Pháp nhân, màu thương hiệu, logo: BUSINESS_DOCS.md mục đầu.

## 3. LUẬT CỨNG – không bao giờ vi phạm

### 3.1 Cơ sở dữ liệu (yêu cầu gốc của PO, giữ nguyên văn)
1. SQL Server, **chỉ schema `dbo`**. **"Không tự tạo schema dm, vh, ht, bc"**.
2. **"Không tự tạo Foreign Key vật lý"**. Quan hệ kiểm tra ở tầng Service.
3. **"Không tạo bảng RecipeVersion"**.
4. **Không đổi tên** các bảng legacy: `Dishe`, `DishCategorie`, `ProductionBatche`, `CleaningBatche`, `ProofOfDeliverie`. Cùng nhóm này còn `IngredientCategorie`, `InventoryPolicie`, `PricePolicie`, `QCInspection`, `QCInspectionLine`. Entity C# có thể đặt tên đẹp, nhưng **phải** gắn `[Table("TênThật")]`.
5. **Không xóa** `Patient`, `PatientDetail`, `MedicalExamination`, `MedicineWarehouse`, `TestDemo`.
6. `Supplier` là bảng nhà cung cấp chuẩn.
7. **"Không tự ý tạo thêm bảng hoặc cột"**. **"Không được tạo ra tên bảng hoặc cột không có trong SQL nguồn"**. Nguồn là `docs/reference/20261113_structure_db_appdelicare.sql`, bản dễ đọc ở `DB_SCHEMA_124.md`.
8. Thiếu chỗ chứa dữ liệu thì **ghi GAP** vào `docs/claude/DECISIONS.md` (mục "GAP mới"), đánh dấu `PROPOSED – BA/PO CONFIRM`, rồi dùng cách tạm hợp lệ: cấu hình `appsettings`, cột `PayloadJson` / `ConfigJson` / `Note` có sẵn, hoặc bỏ qua tính năng đó. **Không tự thêm cột.**
9. Quy tắc nghiệp vụ chưa được xác nhận phải gắn nhãn `PROPOSED – BA/PO CONFIRM` trong code (comment) và trong tài liệu.
10. Dữ liệu demo hoặc seed phải gắn nhãn rõ (`[DEMO]` trong Note/Name) và chỉ nằm trong script seed riêng.
11. **Được phép:** `CREATE OR ALTER PROCEDURE/VIEW/FUNCTION`, index không unique (ghi vào DECISIONS). **Cấm:** `CREATE/ALTER/DROP TABLE`, `ADD COLUMN`, `DROP`, `TRUNCATE`, `DELETE` hàng loạt.
12. Script SQL lưu tại `DB/` (đánh số `DB/sp/uspXxx.sql`, `DB/seed/...`). Chỉ chạy script trên **DB dev** trong `appsettings.Development.json`. **Không bao giờ** chạy lên DB thật.

### 3.2 Bảo mật
- **Không commit bí mật.** JWT key, license, connection string thật, token bot Telegram/Zalo, GATEWAY_KEY đều đặt trong `dotnet user-secrets` (dev) hoặc biến môi trường (prod). Trong repo chỉ để placeholder.
- Không in bí mật ra log, tài liệu hay câu trả lời.
- Mọi truy vấn SQL **dùng tham số**. Cấm ghép chuỗi giá trị người dùng.
- Mỗi thao tác ghi phải biết **ai** làm: người dùng, agent hoặc job hệ thống. Không bao giờ "lấy user đầu tiên" làm người thao tác.

### 3.3 Nghiệp vụ không được phá
- **Sổ chỉ ghi thêm (append-only):** `InventoryTransaction`, `PackagingLedger`, `TemperatureLog`, `AuditChange`, `IntegrationLog`, `ApprovalDecision`, `AttendanceEvent`, `DeliveryEvent`, `ComplaintHistory`. Không Update/Delete. Muốn sửa thì ghi **bút toán đảo / điều chỉnh** tham chiếu dòng gốc (dùng `ReferenceType` / `ReferenceId` / `Reason` có sẵn).
- Các bảng trạng thái tiến trình được cập nhật tại chỗ, nhưng **chỉ theo máy trạng thái** (`docs/reference/STATUS_CATALOG.md`).
- Hạn chót và ngưỡng duyệt nằm trong `docs/claude/BUSINESS_RULES.md`. Giá trị đọc từ cấu hình `BusinessRules` trong appsettings, không hard-code trong logic.
- Agent **không bao giờ** ghi trực tiếp vào DB. Agent chỉ ghi qua ToolGateway, và chỉ sau khi có xác nhận.

### 3.4 Git và phạm vi
- Mỗi task làm trên nhánh `feat/<TaskId>-<slug>` tách từ nhánh anh đang dùng, commit nhỏ, message dạng `T05: ...`.
- Trước khi bắt đầu task: `git status`. Nếu có thay đổi chưa commit **không phải của em** (anh hoặc dev khác đang sửa) thì **không đụng vào, không stash, không commit hộ**. Báo anh và hỏi cách xử lý.
- **Không push và không merge vào nhánh chính** nếu anh chưa bảo.
- Không sửa `docs/reference/*`.
- Không xóa file của anh. Thứ không dùng nữa thì chuyển vào `_obsolete/` và ghi lại trong PROGRESS.

---

## 4. Quy ước code

**Backend (.NET 8):**
- **Chuẩn API = `DELICARE_API_Specification.md` mục 1.2**, áp dụng cho mọi endpoint mới:
  - Route `/api/v1/{module}/{resource}` (kebab-case số nhiều), lệnh chuyển trạng thái dạng `POST …/{id}/<action>`.
  - Envelope `{ success, data, message, errors, traceId }`. Danh sách: `data = { items, total, page, pageSize }`, query `page`, `pageSize` (≤ 200), `q`, `sort=field:asc`, lọc theo tên cột.
  - JSON camelCase theo tên cột SQL. **Không trả / nhận cột deprecated**, không bao giờ trả `PassWord`.
  - Mã HTTP: 400 / 401 / 403 / 404 / 409 (`INVALID_TRANSITION`, `CONCURRENCY_CONFLICT`) / 422 (`REF_NOT_FOUND`, vi phạm nghiệp vụ) / 500. Mã lỗi theo Phụ lục A của spec.
  - ETag = `ModifiedDate` (`If-Match`); lệnh ghi nhận `Idempotency-Key`; header `X-Branch-Id`.
  - `[Permission("<screen-id>", Action)]` với `ObjectMenu.MetaLink = '#/<screen-id>'`.
  - Swagger mô tả đủ (summary, ví dụ request / response) để làm frontend sau.
- **Mã CRUD cũ sinh tự động** (`api/[controller]/GetAll|Insert|…`, `Result<T>`, `[FromForm]`): không phải chuẩn. Được phép thay bằng endpoint `/api/v1` theo spec. Có thể giữ tạm cho tới khi có bản v1 tương ứng, nhưng **không mở rộng thêm** theo mẫu cũ.
- Bên trong vẫn dùng lại hạ tầng tốt có sẵn: UnitOfWork / Repository EF Core, Dapper + stored procedure cho truy vấn danh sách lớn, AuditChange tự động trong DbContext.
- Nghiệp vụ mới (lệnh có trạng thái, sổ, duyệt) viết trong `AppDelicare.Application`: mỗi use case là một class `XxxCommand` + `XxxHandler` + validator. Tên project trong tài liệu API (`Delicare.Api`, `Delicare.Domain`…) là đề xuất cũ; **dùng tên thực tế ở mục 2**.
- Thời gian: lưu theo **giờ Việt Nam** (Asia/Ho_Chi_Minh), khớp code cũ và 75/92 default `getdate()` / `sysdatetime()` của DDL. 17 cột có default `sysutcdatetime()` (UTC) → app **luôn tự gán giá trị**, không dựa vào default. Spec đề xuất UTC (GAP-Q-04); **D-18 chờ anh chốt**, tạm theo giờ VN. Lấy giờ qua `IClock`, không gọi `DateTime.Now` trực tiếp trong nghiệp vụ mới.
- Mỗi lệnh ghi chạy trong 1 transaction (`IUnitOfWork.OpenTransaction`) và ghi `AuditChange`.
- Log bằng Serilog. Không nuốt lỗi.

**Frontend (Angular 18): NGOÀI PHẠM VI hiện tại** – không đọc, không sửa `app-delicare-client/`. Mỗi màn hình trong mockup được hiện thực ở backend dưới dạng API đủ dữ liệu cho màn hình đó, theo spec.

**Kiểm tra trước khi báo xong:**
- `dotnet build AppDelicare/AppDelicare.sln` không lỗi.
- `dotnet test` xanh.
- `git status` không có thay đổi nào trong `app-delicare-client/`.

---

## 5. Tra cứu nhanh

| Cần gì | Xem ở đâu |
|---|---|
| Bảng / cột có tồn tại không, ý nghĩa từng bảng / cột | `docs/reference/DB_SCHEMA_124.md` (có mô tả từ Dictionary + BRD); gốc: `.sql`, `20261113_Dictionary_DB_AppDelicare.xlsx`, `DeliCare_Database_Dictionary_BRD.xlsx` |
| Bảng nào tham chiếu bảng nào | `docs/reference/LOGICAL_RELATIONS.md` (224 quan hệ + 80 cột chưa rõ) |
| Endpoint, input/output, logic của 1 màn hình | `docs/reference/DELICARE_API_Specification.md`, `DELICARE_API_Catalog.csv` |
| Trạng thái hợp lệ | `docs/reference/STATUS_CATALOG.md` |
| Cặp cột trùng (OrderNo/OrderNumber, DeleteddBy/DeletedBy…) | `docs/reference/DEPRECATED_COLUMNS.md` |
| Biểu mẫu in thật, menu thật, quy tắc định lượng / hao hụt, quy trình báo giá → PO | `docs/claude/BUSINESS_DOCS.md` |
| Sơ đồ tổ chức thật, luồng vận hành 9 bước, nhóm quyền, ma trận quyền | `docs/claude/ORG_STRUCTURE.md` (ảnh gốc: `docs/reference/org/`) |
| Ngoài phạm vi (MISA lo kế toán / lương / công nợ; bán lẻ, bảo trì, CRM, CAPA để R2) | `docs/claude/DECISIONS.md` D-21…D-23 |
| Luồng nghiệp vụ end-to-end | `docs/reference/DELICARE_Business_Workflow.md` |
| Ngưỡng, mã chứng từ, ca, nhiệt độ, KPI | `docs/claude/BUSINESS_RULES.md` |
| Lỗi đã biết trong code hiện tại | `docs/claude/CODE_AUDIT.md` |
| 34 hàm gateway cũ | `docs/claude/LEGACY_GATEWAY.md` |
| Sheets → bảng dbo | `docs/claude/MIGRATION_SHEETS_TO_DBO.md` |
| Agent, công cụ, lịch chạy | `docs/claude/AGENTS.md` |

---

## 6. Quy trình làm việc (mỗi phiên)

1. Đọc `docs/claude/PROGRESS.md` (phần "Trạng thái hiện tại") và `docs/claude/DECISIONS.md` (phần "Chờ anh chốt").
2. Chọn task: **theo bảng "★ ƯU TIÊN – RELEASE 1" ở đầu ROADMAP**. Lấy task chưa DONE đầu tiên theo thứ tự lát R1.0 → R1.7, hoặc task anh chỉ định (`/task T13`). **Không làm task `DEFERRED(R2)`.** Nếu task bị chặn bởi quyết định chưa chốt thì bỏ qua, lấy task kế tiếp không phụ thuộc. Hết một lát thì deploy staging, cập nhật UAT_CHECKLIST và dừng lại báo anh.
3. Tạo nhánh `feat/<TaskId>-<slug>` từ nhánh hiện tại.
4. Viết kế hoạch ngắn (3–8 bước) vào PROGRESS dưới tiêu đề task, rồi làm.
5. Đọc mã liên quan trước khi sửa. Đối chiếu bảng/cột với `DB_SCHEMA_124.md` **trước khi** viết entity, query hay DTO.
6. Build và test. Sửa tới khi xanh. Không tắt test để cho qua.
7. Tự rà lại theo **tiêu chí nghiệm thu** của task. Đánh dấu từng tiêu chí đạt hay chưa.
8. Cập nhật:
   - ROADMAP: đổi trạng thái task (`TODO` → `DONE` / `BLOCKED`).
   - PROGRESS: ghi đã làm gì, file chính, cách kiểm tra, việc còn lại.
   - DECISIONS: GAP hoặc câu hỏi mới.
9. Commit trên nhánh task. Báo anh **ngắn gọn**: xong gì, kiểm tra thế nào, cần anh làm gì (vd. chạy script SQL trên DB dev, nhập secret).
10. Nếu anh nói "làm tiếp", "tiếp" hoặc `/next` thì quay lại bước 1.

**Khi nào dừng để hỏi anh:**
- Cần tạo bảng hoặc cột.
- Cần chạy gì lên DB không phải dev.
- Cần bí mật hoặc token thật.
- Hai tài liệu nguồn mâu thuẫn mà DECISIONS chưa phân xử.
- Việc cần sửa frontend → **không làm**, ghi vào DECISIONS mục "Việc cho frontend" để anh giao sau.

Những việc khác em tự quyết theo tài liệu và ghi lại lý do trong PROGRESS.
