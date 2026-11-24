# CLAUDE.md – DELICARE Enterprise (đọc file này trước mọi việc)

Bạn là kỹ sư chính của dự án **DELICARE**: hệ thống điều hành nhà máy suất ăn công nghiệp, quy mô mục tiêu **20.000 suất/ngày**. Chủ dự án là **anh Phạm (PO)**. Hãy xưng "em", gọi chủ dự án là "anh". Giao tiếp và tài liệu viết bằng tiếng Việt. Mã nguồn đặt tên bằng tiếng Anh. Chữ hiển thị trên giao diện viết tiếng Việt.

**PHẠM VI HIỆN TẠI = HOÀN THIỆN TOÀN BỘ LUỒNG NGHIỆP VỤ R1 (D-44, 2026-11-16): em làm **backend** (BE-2 đã xong; **hiện tại: đợt BE-3** – biểu đồ, cảnh báo, tìm kiếm, IoT, KPI, dashboard – D-57, ROADMAP mục BE-3; **không** làm tra cứu cấu trúc database – D-58). **Frontend: em làm toàn bộ (D-65, 2026-11-19 – thay D-49)** – giao diện thật là engine `app-delicare-client/src/assets/delicare/js` (mỗi `APP.register('<screen-id>')` là một màn); task BE có đổi API thì sửa luôn giao diện trong cùng task; việc FE tồn đọng ở ROADMAP FE-E / DECISIONS mục E. KHÔNG chạy Pilot riêng. ĐỂ SAU: tích hợp bot, Hermes + quản lý agent, báo cáo, job tự động (D-45), refresh token (D-47).**
- **Frontend `app-delicare-client/` (Angular 18):** theo **khung Angular cũ** (mục 4.4). Hiện **tạm dừng** cho tới khi xong đợt BE-2 (D-40, D-43, D-44); sau đó làm **mọi màn hình của luồng nghiệp vụ** (trừ dashboard / báo cáo / bot / agent), chỉ màn có API đã xong.
- **⚠ KHUNG CHUẨN = CODE CŨ (D-30, anh chốt 2026-11-15).** Mọi API viết theo đúng mẫu và quy ước của code cũ trong solution (`api/[controller]/<Action>`, Controller → Service → UnitOfWork / Dapper SP, `Result<T>` / `Results<T>`, `[FromForm]`, try/catch + Serilog). **KHÔNG sinh thêm API `/api/v1`**, không envelope mới, không CRUD generic, không Command / Handler. Code cũ **đã có** người dùng, nhóm quyền (vai trò), ma trận quyền, menu, đăng nhập → **rà soát và bổ sung trên cái đã có**, không viết lại song song. Chi tiết: mục 4.
- `docs/reference/DELICARE_API_Specification.md` + Catalog chỉ còn là **danh sách yêu cầu nghiệp vụ** (màn hình cần chức năng gì, dữ liệu gì, quy tắc gì). **Không** dùng URL, envelope, mã lỗi, ETag / Idempotency-Key… ở mục 1.2 của spec.
- **Mục tiêu Release 1:** hoàn thiện lõi .NET đủ API (theo khung cũ) cho mọi màn hình trong mockup (`docs/mockup/`), đủ nghiệp vụ mô tả trong spec, có Swagger + test, triển khai được backend. Đây sẽ là hệ thống vận hành thật đầu tiên (bản Apps Script / Sheets chỉ là demo). Agent, Tool Gateway, Telegram / Zalo, Obsidian để Release 2.

Mục tiêu của file này: anh **không phải giải thích lại** bối cảnh. Mọi việc cần làm đã nằm trong `docs/claude/ROADMAP.md`. Em làm lần lượt từng task theo quy trình ở mục 6.

---

## 1. Bức tranh tổng thể – kiến trúc 4 lớp

| Lớp | Thành phần | Vai trò |
|---|---|---|
| **1. Lõi nghiệp vụ** | .NET 8 Web API + SQL Server (124 bảng `dbo`); Angular 18 là frontend (dev FE khác làm – D-49) | **Nguồn sự thật duy nhất**: hợp đồng, đơn, định mức, kho, lô truy xuất, sản xuất, giao nhận, hộp/khay, nhân sự. Các mốc hạn chót (20h khóa đơn…) chạy bằng job trong lõi. |
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
│   ├─ AppDelicare.Model     (namespace AppDelicare.Model)    Entity, Request, SearchRequest, ViewModels, Helper
│   ├─ AppDelicare.Data      (AppDelicare.Data)               DatabaseContext (EF Core), Repository<T>, UnitOfWork, DapperRepository
│   ├─ AppDelicare.Services  (AppDelicare.Services)           Service theo entity (sinh tự động) + method nghiệp vụ bổ sung tay ← NƠI VIẾT NGHIỆP VỤ
│   ├─ AppDelicare.WebApi    (AppDelicare.WebApi)             Controller `api/[controller]`, Startup, Autofac, JWT, Serilog, Security (phân quyền)
│   ├─ AppDelicare.Application (AppDelicare.Application)        ⛔ lớp v1 làm thử (CRUD generic, Command / Handler) – KHÔNG mở rộng; T45R chuyển nghiệp vụ về Services rồi đưa vào `_obsolete/`
│   ├─ AppDelicare.Tests     (AppDelicare.Tests)              xUnit
│   └─ AppDelicare.Generator (T4 + OrmLite)                        bộ sinh mã T4 cũ – CHỈ THAM KHẢO, chỉ chạy được trong Visual Studio (đổi tên ở T45R.6)
├─ app-delicare-client/          ← Angular 18 – khung Angular cũ (mục 4.4); làm sau đợt BE-2 (D-44)
├─ backup-db/                    ← bản backup DB thật (AppDelicare_20261110.bak/.mdf/.ldf) – ĐÃ gitignore, KHÔNG commit, KHÔNG sửa
└─ DB/                           ← (tạo mới) script SQL: DB/sp (stored procedure), DB/seed – được commit
```

Ngày 2026-11-14 (theo yêu cầu của anh) 4 thư mục `ClinicManagement.Model / Data / Services / WebApi` đã đổi thành `AppDelicare.*` cho khớp tên project và namespace. Bộ sinh T4 cũ cũng **đã đổi thành `AppDelicare.Generator`** (T45R.6, anh đồng ý 2026-11-15). **Không đổi tên thư mục khác** nếu không có task riêng.

**Bộ sinh mã chuẩn = `tools/AppDelicare.CodeGen`** (D-32): đọc DDL, sinh đúng mẫu code cũ, chạy bằng dòng lệnh nên test được. Bộ T4 giữ để tham khảo / đối chiếu mẫu, không dùng để sinh bảng mới.

**Các project MỚI** sẽ thêm vào solution theo ROADMAP. Mỗi project ở một thư mục cùng tên:
`AppDelicare.Job` (Worker Service – container riêng, hẹn giờ gọi Service cũ, không Hangfire, không thêm bảng – D-48, ARCHITECTURE.md mục 1b; **triển khai Linux + Docker – D-60**, không Windows Service; làm sau khi hoàn thiện luồng), `AppDelicare.Messaging` (**D-62**: container `messaging` riêng chứa **tất cả bot** – Telegram Vận hành / Điều hành, sau này Zalo / Facebook; **không tham chiếu Services / Data / Model, không chạm DB**, gọi `api` qua HTTP – BOT_SOURCE_ARCHITECTURE.md; Obsidian exporter nằm trong Services + Job, không còn `AppDelicare.Integration`), `AppDelicare.ToolGateway` (MCP + REST cho agent), `AppDelicare.Tests` (xUnit), `tools/AppDelicare.CodeGen` (bộ sinh mã mới đọc DDL).

---

## 2b. Môi trường dev trên máy anh
- DB dev: **AppDelicare** trên SQL Server local. Tài khoản app dev và các đường dẫn máy nằm trong `docs/claude/LOCAL_DEV.local.md` (gitignored, không commit, không in mật khẩu ra báo cáo).
- Script nâng cấp DB chính thức của PO (91 → 124 bảng): `DB/migrations/20261113_restore_structure_db_appdelicare.sql`. Đây là **ngoại lệ duy nhất** của luật "không tạo bảng / cột": chỉ chạy khi anh đồng ý (D-14), trên DB dev, sau khi backup.
- **Cấu trúc bảng = MIGRATION (2026-11-18):** `DB/migrations/*.sql` là nguồn duy nhất, chạy theo thứ tự tên, mỗi file 1 lần (`deploy/db/Migrations.ps1` + `deploy/db/migrations.sh`, ghi nhận bằng extended property `DelicareMigration:<file>`). Thay đổi bảng / cột anh đã duyệt = file mới `YYYYMMDD_NN_<tên>.sql` chạy lại được; **không sửa file đã có** – xem `DB/README.md` mục Migration.
- Tài liệu nghiệp vụ thật (menu, phiếu sản xuất, phiếu trước sản xuất, báo giá / PO, tổng hợp suất): **`docs/claude/BUSINESS_DOCS.md`**. File gốc nằm ngoài repo, chỉ đọc, có dữ liệu mật.
- Pháp nhân, màu thương hiệu, logo: BUSINESS_DOCS.md mục đầu.

## 3. LUẬT CỨNG – không bao giờ vi phạm

### 3.1 Cơ sở dữ liệu (yêu cầu gốc của PO, giữ nguyên văn)
0. **Phiên bản chuẩn = SQL Server 2022, mức tương thích 160 (D-61).** Máy anh chạy SQL Server 2025 nhưng DB dev đặt `COMPATIBILITY_LEVEL = 160`; máy dev FE, server test, production là 2022. **Chỉ dùng cú pháp có trong 2022** – cấm (trong SQL, SP, seed và SQL inline trong C#): kiểu `json`, `vector`; `REGEXP_*`; `JSON_OBJECTAGG` / `JSON_ARRAYAGG` / `JSON_CONTAINS`; `EDIT_DISTANCE*`, `JARO_WINKLER_*`; `UNISTR`, `PRODUCT()`, `BASE64_ENCODE` / `BASE64_DECODE`, `CURRENT_DATE`, toán tử nối `||`; `AI_GENERATE_*`, `VECTOR_*`, `sp_invoke_external_rest_endpoint`. Mức 160 **không chặn hết** các hàm này (một số hàm regex vẫn chạy ở mọi mức) → test quét mã (T48.5) là chốt chặn. **Không chuyển dữ liệu giữa các máy bằng `.bak`** (bản 2025 không khôi phục được trên 2022) – chỉ bằng script (`deploy/db/scripts.order`).
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
- **Mã định danh (D-63, BUSINESS_RULES §11):** mọi cột mã thuộc 1 trong 3 nhóm – G1 máy chủ tự sinh qua `IDocumentNumberService` (danh mục `TIỀNTỐ-NNNNN`, chứng từ `TIỀNTỐ-YYMMDD-NNNN`; bỏ qua giá trị gửi lên, không đổi sau khi cấp), G2 mã người đặt (chữ hoa không dấu, không trùng, khóa khi đã dùng), G3 không phải mã định danh. **Thêm bảng / cột mã / màn mới → xếp nhóm trong bảng phân nhóm (test chặn) và đăng ký tiền tố G1 không trùng ở §11**; giao diện đọc `Common/GetCodeRules`, không cho gõ mã G1. Không bảng đếm, không SEQUENCE.
- Agent **không bao giờ** ghi trực tiếp vào DB. Agent chỉ ghi qua ToolGateway, và chỉ sau khi có xác nhận.

### 3.4 Git và phạm vi
- Mỗi task làm trên nhánh `feat/<TaskId>-<slug>` tách từ nhánh anh đang dùng, commit nhỏ, message dạng `T05: ...`.
- Trước khi bắt đầu task: `git status`. Nếu có thay đổi chưa commit **không phải của em** (anh hoặc dev khác đang sửa) thì **không đụng vào, không stash, không commit hộ**. Báo anh và hỏi cách xử lý.
- **Không push và không merge vào nhánh chính** nếu anh chưa bảo.
- Không sửa `docs/reference/*`.
- Không xóa file của anh. Thứ không dùng nữa thì chuyển vào `_obsolete/` và ghi lại trong PROGRESS.

---

## 4. Quy ước code – KHUNG CHUẨN LÀ CODE CŨ (D-30)

### 4.1 Nguyên tắc
1. **Code cũ trong solution là khung chuẩn.** Mọi chức năng mới hay bổ sung viết **giống hệt mẫu cũ** (mục 4.2). Mẫu để chép: `WarehouseController.cs` + `WarehouseService.cs`.
2. **KHÔNG tạo API `/api/v1`.** Không dùng / mở rộng: `Controllers/V1/**`, `Api/ApiControllerBase`, envelope `{success,data,…}`, `EnvelopeResultFilter`, `IdempotencyFilter`, `BranchContext`, `PageQuery`, `CrudService` / `ResourceRegistry` / `TableMetadata.Generated.cs`, `IDispatcher` / Command / Handler trong `AppDelicare.Application`. Lớp này sẽ được T45R chuyển nghiệp vụ về khung cũ rồi đưa vào `_obsolete/`.
3. **Rà soát trước, bổ sung sau.** Trước khi viết bất kỳ chức năng nào, tìm trong code cũ: Controller, Service (Interface + Implement), SP `DB/sp/usp<Entity>_*.sql`, Request / SearchRequest / ViewModel / Custom của entity đó và các service liên quan.
   - Đã có → dùng lại; thiếu thì **thêm method vào đúng Service đó và thêm action vào đúng Controller đó**.
   - Chưa có → tạo mới **theo đúng mẫu** (CodeGen cho bảng; tay cho service / controller nghiệp vụ không gắn bảng, đặt tên `XxxService` / `XxxController` như `InventoryLedgerService`, `DashboardController`, `ImportExcelController`).
   - **Không** tạo service / controller thứ hai làm cùng việc với cái đã có.
4. **Người dùng, vai trò, phân quyền, menu, đăng nhập ĐÃ CÓ trong code cũ** – dùng và bổ sung, không viết lại:

   | Chức năng | Đã có ở |
   |---|---|
   | Đăng nhập, JWT, trả thông tin + menu theo quyền | `AuthenticationController.Login` → `LoginService`, `JwtFactoryService`, `TokenFactoryService` |
   | Người dùng: danh sách, thêm / sửa / xóa, gán nhóm (`ListUserGroupId` → `UserClaim`), reset mật khẩu, khóa / mở (`SaveIsActive`), cấp admin (`SaveIsAdmin`), đổi mật khẩu, tạo user sau khi tạo Person (`InsertUserAfter`) | `UserController` / `UserService`, `UserClaimService` |
   | Vai trò = nhóm người dùng | `UserGroupController` / `UserGroupService` (bảng `UserGroup`) |
   | Ma trận quyền nhóm × menu (`AllowAccess/Add/Edit/Delete/Print/Export/Import`) | `UserRoleController.GetByUserGroupId` / `Update` → `UserRoleService` (SP `uspUserRole_GetByUserGroupId`) |
   | Menu chức năng (cây 2 cấp, `MetaLink = '#/<screen-id>'`) | `ObjectMenuController` / `ObjectMenuService` |
   | Kiểm tra quyền phía server | `Security/PermissionFilter` + `PermissionConvention` (T07, đọc chính các bảng trên – giữ, là hạ tầng chứ không phải API v1) |

   Quy tắc chi tiết: **mục 4.3**.
   | Người thao tác | `ICurrentActor` (`Data/Share/CurrentActor.cs`) |

5. **Spec API = yêu cầu nghiệp vụ.** Với mỗi endpoint trong `DELICARE_API_Specification.md` / Catalog: hiểu chức năng → tìm chỗ tương đương trong code cũ → bổ sung action kiểu cũ `api/<Entity>/<Action>`. Ghi ánh xạ "màn hình → chức năng spec → action cũ" trong `docs/frontend-handoff/` để làm frontend sau.

### 4.2 Mẫu từng tầng (chép đúng, kể cả cách đặt tên, thông báo, log)
- **Controller** `AppDelicare.WebApi/Controllers/<Entity>Controller.cs` – mỗi file 1 controller:
  - Kế thừa `BaseController` (`[Route("api/[controller]")]`, `[ApiController]`, `[Authorize]`); primary constructor inject `I<Entity>Service`.
  - Action chuẩn: `GetAll([FromQuery] <Entity>SearchRequest)` (GET), `Insert([FromForm] <Entity>Request)` (POST), `Update([FromForm])` (PUT), `Delete(Guid id)` (DELETE), `GetById(Guid id)`, `GetAllData()` (dropdown).
  - Thân action: kiểm `ModelState` / null → `BadRequest(ConstantHelper.Message.ModelStateMessage)`; `try { return Ok(await _service.X(...)); } catch (Exception ex) { Log.Error(string.Concat("[", GetType().Name, "]", " <Action>: ", ex.ToString())); return StatusCode(500, ConstantHelper.Message.InternalServer); }`.
  - **Action nghiệp vụ** đặt trong controller của entity chính, tên động từ PascalCase như mẫu cũ (`ResetPassword`, `SaveIsActive`, `ChangePassword`). Ví dụ: `RecipeController.Submit / Lock / CalculateCost`, `OrderController.Submit / Confirm / Cancel`, `ApprovalRequestController.GetMyPending / Decide`, `InventoryTransactionController.Reverse`. Đọc = GET, ghi = POST / PUT. Request ghi dùng `[FromForm]` như cũ; chỉ khi request có danh sách lồng nhau (dòng đơn, dòng công thức…) mới dùng `[FromBody]` (như `AuthenticationController.Login`) và comment lý do.
  - **Không bao giờ** inject `DatabaseContext` / `IUnitOfWork` vào controller; không đặt logic trong controller.
- **Service** `AppDelicare.Services/Interface/I<Entity>Service.cs` + `Implement/<Entity>Service.cs`:
  - Inject `IUnitOfWork`, `IDapperRepository`, `IMapper`, `IAuditChangeService`, và service khác khi cần (như `UserService` gọi `UserClaimService`).
  - Trả `IResult<T>` (`Result<T> { ResponseCode, ResponseMessage, Data }`) / `IResults<T>` (`Results<T> { ResponseCode, ResponseMessage, ListData, TotalRecords }`). Thông báo lấy `ConstantHelper.Message` (thêm hằng mới vào đó nếu cần, tiếng Việt).
  - Ghi: `Save(request, typeSave)` mở `using var tran = await _unitOfWork.OpenTransaction()`, gọi private `Insert / Update / Delete` trả `string` lỗi (rỗng = OK) → `CommitTransaction` / `RollbackTransaction` như mẫu; lỗi nghiệp vụ trả `ResponseCode = 400` + message. **Lệnh nghiệp vụ mới viết cùng kiểu**: 1 method public, transaction, kiểm tra quy tắc → message lỗi, ghi, commit.
  - Xóa: xóa mềm qua `CommitAsync(TypeSave.Delete)`, kiểm tham chiếu bằng `_auditChangeService.IsReferencedAsync(...)` (ReferenceGuard).
- **Danh sách / báo cáo:** SP `usp<Entity>_GetAll` (tham số `@SearchText`, các lọc, `@Page`, `@PageSize`, `@SortField`, `@SortOrder`; trả cột `TotalRow`), gọi `_dapperRepository.QueryMultipleUsingStoreProcAsync<<Entity>ViewModel>(...)`, gán `STT`. Truy vấn phức tạp khác: SP `usp<Entity>_<Mục đích>` (`CREATE OR ALTER`, tham số hóa) trong `DB/sp/`.
- **DTO:** `Model/Request/<Entity>Request.cs`, `SearchRequest/<Entity>SearchRequest.cs`, `ViewModels/<Entity>ViewModel.cs` (sinh). Bổ sung: `Model/Request/Custom/<Entity>CustomRequest.cs` (hoặc `<Entity><Action>Request.cs`), `Model/ViewModels/Custom/<Entity>CustomViewModel.cs`, `Model/Model/<X>Model.cs` (như `UserRoleModel`). Map bằng AutoMapper (`Infrastructure/AutoMapperConfiguraion.cs`, `AutoMapperCustomConfiguraion.cs`). Không bao giờ trả `PassWord`; không nhận / trả cột deprecated.
- **Entity / Repository / UnitOfWork / DbContext:** sinh bằng `tools/AppDelicare.CodeGen` (Entity và `*.Generated.cs` không sửa tay). Service / Controller sau khi sinh là **file của dev**: bổ sung tay; chỉ chạy lại CodeGen với `--only <Table>` (bảng mới) hoặc `--skip-existing`, không ghi đè file đã sửa.
- **Phân quyền:** `PermissionConvention` tự gắn quyền cho controller kế thừa `BaseController` theo `ScreenMap` (controller → screen-id mockup) và tên action (`Get*`→View, `Insert/Create*`→Add, `Delete*`→Delete, `Export*`, `Print*`, `Import*`, còn lại → Edit). Action nghiệp vụ không khớp quy tắc tên đó (vd. duyệt, khóa) **phải** gắn `[Permission("<screen-id>", PermissionAction.Approve|…)]` rõ ràng. Controller mới phải có dòng trong `ScreenMap`.
- **Giữ nguyên các cải tiến R1.0** (nằm trong khung cũ): bí mật ngoài code, `ICurrentActor`, AuditChange tự động trong DbContext, khóa sổ append-only, ReferenceGuard, CorrelationId, `/health`, Serilog, test.
- **Thời gian:** lưu **giờ Việt Nam** (Asia/Ho_Chi_Minh), khớp code cũ. 17 cột default `sysutcdatetime()` → app luôn tự gán giá trị. D-18 chờ anh chốt. Lấy giờ qua `IClock` (T45R chuyển `IClock` / `BusinessRulesOptions` từ `AppDelicare.Application` sang `AppDelicare.Data/Share` + `AppDelicare.Model/Option`). Ngưỡng nghiệp vụ đọc từ cấu hình `BusinessRules`.
- Log bằng Serilog. Không nuốt lỗi.

### 4.3 Phân quyền, vai trò, người dùng – GIỮ ĐÚNG CƠ CHẾ CŨ (D-31)
**Mô hình dữ liệu cũ là duy nhất:** `User` (`IsActive`, `IsAdmin`) → `UserClaim` (user thuộc nhóm nào) → `UserGroup` (vai trò) → `UserRole` (nhóm × `ObjectMenu`, cờ `AllowAccess / AllowAdd / AllowEdit / AllowDelete / AllowPrint / AllowExport / AllowImport`) ← `ObjectMenu` (menu 2 cấp, `MetaLink`). User thuộc nhiều nhóm thì quyền cộng dồn (OR). `IsAdmin = 1` có mọi quyền.

**Được làm:**
- Quản lý user / nhóm / ma trận quyền / menu **chỉ qua controller + service cũ**: `UserController`, `UserGroupController`, `UserRoleController` (`GetByUserGroupId`, `Update`), `ObjectMenuController`, `UserClaimService.SaveAfterUser`. Thiếu chức năng thì **thêm method / action vào đúng các file này**.
- Đăng nhập giữ `AuthenticationController.Login` → `LoginService.Authenticate`: kiểm `IsDeleted` / `IsActive`, trả token + `listMenu` theo `UserRoleService.GetByUserGroupId(…, false)`. Giữ nguyên các trường trong token / response cũ (chỉ được thêm, không đổi tên, không bỏ).
- Kiểm tra quyền phía server (`PermissionFilter` + `PermissionService`) **chỉ đọc đúng các bảng trên** – đây là cùng cơ chế cũ, áp ở server thay vì chỉ ẩn menu.

**Không được làm:**
- Không thêm bảng / cột / hệ quyền mới (ASP.NET Identity, role claim, policy theo tên nhóm, quyền trong `appsettings`…).
- Không viết API quản trị user / nhóm / quyền / menu thứ hai (v1 đang có → T45R gỡ).
- Không gán quyền theo **tên nhóm cứng trong code hoặc cấu hình**. Đổi tên nhóm trên màn hình quản trị không được làm hỏng quyền.

**Quyền DUYỆT (không có cột `AllowApprove`):** mỗi loại duyệt là **một mục `ObjectMenu` cấp 2** dưới nhóm menu "Phê duyệt" (vd. Duyệt đơn hàng / lệch suất, Duyệt đề nghị & đơn mua, Duyệt kế hoạch sản xuất, Duyệt kiểm kê, Duyệt nghỉ / đổi ca, Duyệt phiên bản công thức). MetaLink trỏ hộp duyệt lọc theo loại (screen-id lấy từ mockup, vd. `#/approval-inbox?type=PO` – kiểm `catalog.js`). Nhóm nào được duyệt = `UserRole.AllowAccess` (xem) + `AllowEdit` (duyệt / từ chối) trên mục đó, gán trên **chính màn hình ma trận quyền cũ**. `PermissionAction.Approve` kiểm theo mục menu này. `ApprovalStep.ApproverRoleCode` (nếu dùng) chỉ thu hẹp thêm theo nhóm, không thay cho UserRole. Đây là dữ liệu cấu hình, không thêm cột (PROPOSED – anh duyệt khi UAT-1).

**Cache quyền (5′):** mọi thao tác đổi quyền qua service cũ phải xóa cache ngay: `UserRoleService.Update`, `UserClaimService.SaveAfterUser`, `UserService.Save / Delete / SaveIsActive / SaveIsAdmin`, `UserGroupService.Save`, `ObjectMenuService.Save / SaveIsActive`. (T45R chuyển `IPermissionService` sang `AppDelicare.Services` để service cũ gọi được `Invalidate` / `InvalidateAll`.)

**Seed** (`DB/seed/ObjectMenu_seed.sql`, `UserGroup_seed.sql`, `UserRole_seed.sql`): chỉ **thêm phần chưa có** (theo `MetaLink` / `Name`), **không ghi đè** quyền admin đã chỉnh trên màn hình.

**Mật khẩu:** đang dùng `EncriptFunctionsHelper.GeneratePassword` (MD5). Đổi cách băm là D-29 (anh chốt); nếu làm thì sửa trong chính `EncriptFunctionsHelper` / `LoginService` / `UserService`, tương thích hash cũ, không đổi API.

### 4.4 Frontend Angular – KHUNG CHUẨN LÀ CODE ANGULAR CŨ (D-35)
**⚠ D-65 (2026-11-19, thay D-49): em làm toàn bộ FE, ở engine `src/assets/delicare/js` (không viết lại màn trong `src/app/main/*`); mục 4.4 dưới đây chỉ áp cho phần Angular còn dùng (đăng nhập, shell) và phần "Giao diện (D-36)" về màu / cỡ chữ. Kiểm tra mỗi task FE theo D-65 c.** ~~Em **không tự làm FE-x** (kể cả khi `/next`).~~ Mục này dùng để: (1) **soát code FE** khi ghép luồng; (2) sửa FE **chỉ khi anh yêu cầu rõ**. Mỗi API backend mới vẫn phải có handoff trong `docs/frontend-handoff/` để dev FE làm.

**Phạm vi (D-44):** mọi màn hình của luồng nghiệp vụ R1 theo `docs/mockup/` (trừ dashboard / KPI, báo cáo, bot, Hermes / agent), làm sau đợt BE-2, và chỉ khi **API backend tương ứng đã "Đã xong"** trong `docs/reference/DELICARE_API_Catalog.xlsx` / `docs/frontend-handoff/`. Không làm màn hình mà backend chưa có.

**Mẫu để chép** (giống D-30 cho backend – không đổi kiến trúc, không thêm thư viện UI):
- Service: `src/app/services/<entity>.service.ts` theo mẫu `unit-measure.service.ts` – `getAll / insert / update / delete / getById / getAllData` + method cho action nghiệp vụ (`URL_API + '<Controller>/<Action>'`). Insert / Update gửi **form** như backend (D-30); action nhận JSON thì gửi JSON.
- Màn hình danh sách: `src/app/main/<screen>/<screen>.component.ts|html` + `<screen>-routing.module.ts` + `<screen>.module.ts` theo mẫu `main/unit-measure`, `main/user`, `main/supplier`: `TableState` + `table-pager`, `searchForm`, `permission(...)` từ `listMenu` để ẩn / hiện nút Thêm / Sửa / Xóa / In / Xuất / Nhập; kiểm `responseCode === 200`, `responseCode === 400` → hiện `responseMessage` qua `NotifyService`.
- Form: `<screen>-modal/<screen>-modal.component.ts|html` mở bằng `ModalService` (mẫu `unit-measure-modal`, `user-modal`). Dropdown dùng `shared/form/select.component`, ngày dùng `shared/form/date-picker.component`, số dùng `number-input.directive`.
- Route: thêm vào `main/main-routing.module.ts` (lazy `loadChildren`). **Đường dẫn route = screen-id của mockup** (vd. `adm-user`, `crm-customer`) để khớp `ObjectMenu.MetaLink = '#/<screen-id>'`. Helper `permission()` / `checkPermission()` trong `helpers/common-helper.ts` hiện so `'/' + DOMAIN_ROUTER + '/' + metaLink` → sửa **một chỗ** ở helper để bỏ tiền tố `#/` khi so (không sửa dữ liệu MetaLink).
- Sidebar dựng từ `listMenu` (đăng nhập / `api/User/GetCurrent`), không hard-code menu.
- Bố cục, nhãn, cột, bộ lọc, nút theo `docs/mockup/` (màn hình cùng screen-id). Chữ hiển thị tiếng Việt.
- **Giao diện (D-36): giữ khung code Angular cũ, thay "lớp áo" theo mockup.** Làm bằng CSS + vài thành phần dùng chung, không đổi kiến trúc:
  - Màu: chính `#91AF42` (đậm `#6F9131`, nhạt `#EFF5E5`), phụ `#F07825`; nền `#F6F8F4`, chữ `#263238`, viền `#E8ECE5` – ghi đè biến `--primary`… của template trong `src/assets/css/custom.css` / `clinic.css` (không sửa `style.css` gốc). Font **Be Vietnam Pro** (tải về `assets`, không phụ thuộc mạng ngoài nếu triển khai nội bộ được).
  - **Cỡ chữ / kích thước (giữ cỡ của bản cũ, không dùng cỡ nhỏ của mockup):** chữ thường 14px; chữ trong bảng 13px; tiêu đề cột 12px, **không in hoa**; dòng bảng cao ≥ 44px; nút và ô nhập cao ≥ 36px; tiêu đề trang ~20px; mục menu sidebar 13–14px, cao ≥ 40px. Dùng được trên máy tính bảng.
  - Tên hiển thị: **DELI-CARE**; tiêu đề tab, footer, trang đăng nhập dùng tên pháp nhân (BUSINESS_DOCS.md). Bỏ mọi chữ "phòng khám", "W3CRM".
  - Sidebar nhóm theo menu cấp 1 của `ObjectMenu` (như mockup), mục đang mở được tô; ô chọn chi nhánh (`api/OrganizationUnit/GetBranches`) trên header. Bỏ nút sáng / tối.
  - Thành phần dùng chung `shared/page-header`: đường dẫn trang → tiêu đề → mô tả ngắn → nút hành động bên phải (Xuất / Tạo mới…). Mọi màn hình mới dùng; màn hình cũ đổi khi được sửa.
  - Bảng: nhãn trạng thái màu (`shared/status-badge`, màu theo `api/MetaData/GetStatuses`), cột khóa ngoại hiện tên; nút thao tác xem / sửa / xóa dạng viền mảnh như mockup; giữ `table-pager` cũ. Cột STT giữ (khung cũ).
  - Lọc: ô tìm + dropdown lọc đặt ngay trên bảng; nút lọc nhanh theo trạng thái nếu màn hình có cột trạng thái. Thẻ số liệu đầu trang chỉ làm ở màn hình mockup có và API có số liệu.
  - Thêm / sửa: popup (`ModalService`) như cũ. **Ngăn kéo chi tiết bên phải có tab** chỉ cho màn hình cần xem sâu: khách hàng 360°, NCC, công thức, đơn hàng, PO, mẻ sản xuất, chuyến giao.
  - **Không đưa vào app thật** các phần chỉ để trình diễn của mockup: nhãn "DỮ LIỆU DEMO", nút "Truy vết DB", tên cột DB in nhỏ dưới tiêu đề cột, khung cảnh báo GAP / PARTIAL, nút đổi vai trò, "Hướng dẫn trình diễn".
- Màn hình cũ đã có (`main/user`, `main/user-group`, `main/object-menu`, `main/person`, `main/supplier`, `main/unit-measure`, `main/change-password`, `main/profile`…): **rà soát và bổ sung**, không viết lại song song.
- Không xóa `pages/**` (demo template w3crm) – chỉ gỡ khỏi route / menu (đưa route demo ra khỏi `app.routes.ts` hoặc chặn bằng guard). Không đụng `main/patient*`, `main/medical-*` ngoài việc ẩn khỏi menu.
- Không gọi `api/AuditChange/DeleteData`, `api/LocationTracking/DeleteData` (D-26).
- Được chạy trong `app-delicare-client/`: `npm ci`, `npm run build`, `npm start` (dev). Không nâng phiên bản Angular / thư viện. `environment` trỏ API staging / local, không ghi bí mật.

**Kiểm tra trước khi báo xong:**
- `dotnet build AppDelicare/AppDelicare.sln` không lỗi; `dotnet test` xanh.
- Task chỉ backend (không đổi API giao diện dùng): `git status` không có thay đổi trong `app-delicare-client/`. Task frontend (FE-x): `npm run build` trong `app-delicare-client/` không lỗi; chỉ sửa file thuộc màn hình của task + helper / route / service dùng chung cần thiết.
- Phân quyền: không bảng / cột / cấu hình quyền mới; không API quản trị user / quyền ngoài controller cũ (mục 4.3).
- Không có file mới trong `Controllers/V1/`, `Api/`, `AppDelicare.Application/`; `grep -rn "api/v1" AppDelicare --include=*.cs` không tăng (sau T45R phải bằng 0).
- Controller / Service mới hoặc sửa đúng mẫu 4.2 (so với `WarehouseController` / `WarehouseService`).

---

### 4.5 CHUẨN GIAO DIỆN BẮT BUỘC CHO **MỌI** TÍNH NĂNG (anh chốt 2026-11-23 – áp toàn ứng dụng, cả màn cũ lẫn màn mới)
> Không chỉ áp cho màn được nêu trong task. **Sửa / làm bất kỳ màn nào cũng phải đưa màn đó về đúng chuẩn này**; màn chưa đúng chuẩn = lỗi. Rà toàn bộ trong ROADMAP UI-6.10.
1. **Ngôn ngữ (D-88 c):** mọi chữ người dùng thấy – giao diện, chip lọc, ô chọn, tiêu đề cột, form, nút, thông báo / lỗi (kể cả lỗi máy chủ), tooltip, file Excel / CSV / PDF, tên file, tin nhắn bot – **tiếng Việt, một cách gọi duy nhất** theo từ điển trạng thái chung (`UI.STATUS_VI` + phía máy chủ). Active / Inactive = **Đang hoạt động / Ngừng hoạt động**. Không in mã / tên cột kỹ thuật (Status, ForecastQuantity, true / false, null…). Chỉ giữ tiếng Anh cho mã chứng từ, đơn vị chuẩn, tên riêng. Mã trạng thái mới phải thêm vào từ điển trước khi lên giao diện.
2. **Thẻ chỉ số (D-88 a, b; D-64):** < 2 thẻ → **không hiển thị hàng chỉ số**; 2 → mỗi thẻ 1/2; 3 → 1/3; 4 → 1/4 (tối đa 4 một hàng); ≥ 5 → xuống hàng, thẻ thứ 5 trở đi rộng 1/4. Máy tính bảng / điện thoại 2 cột. Luôn qua hàm chung `UI.kpis`, không tự ghép khung `.kpis`. Trang chi tiết dùng bản gọn `.kpis.compact` (D-80 d).
3. **Màn bảng nhập số theo ngày / ma trận / kế hoạch (D-84 g):** dòng giải thích mục đích · dải bước đánh số có nút tại bước · chỉ số gọn · kỳ cố định (tuần / tháng) + ◀ ▶ · nhóm dòng mở / gập + dòng tổng · **một số mỗi ô**, màu theo trạng thái + chú thích màu · chi tiết phụ ở chữ nhỏ / rê chuột · sửa trực tiếp trong ô · phần phụ để trong tab. Mô phỏng mẫu: `docs/frontend-handoff/assets/forecast-week-mockup.html`.
4. **Chi tiết hồ sơ / chứng từ (D-80):** trang toàn màn theo khuôn hồ sơ / chứng từ, không dùng ngăn trượt cho hồ sơ / chứng từ chính; phần đầu gọn một khung bọc tên + chỉ số.
5. **Biểu đồ (D-86 a):** chỉ **Chart.js** (có sẵn, nạp qua `angular.json`), qua hàm chung `UI.chart`; không vẽ SVG thủ công, không CDN.
6. **Giảm thao tác:** không bắt người dùng làm từng bản ghi khi có thể làm hàng loạt (xác nhận / xuất / gửi nhiều dòng một lần – D-85, D-86 c, D-87); không có nút phụ thuộc bước trước mà nằm tách khỏi bước đó; mặc định điền sẵn số hợp lý (người dùng chỉ sửa ngoại lệ).
7. **Tên menu / màn (D-75 a):** ngắn, từ nghiệp vụ quen thuộc, không tiếng Anh / thuật ngữ kỹ thuật; bảng con không lên menu; màn chưa có API không lên menu.
8. **Tính năng chưa làm xong** mà vẫn để nút trên giao diện: bấm hiện "Tính năng chưa được nâng cấp. Vui lòng liên hệ quản trị viên." (D-87 c) – không để nút báo lỗi kỹ thuật hay màn GAP.
9. **Kiểm tra trước khi báo xong mọi task FE:** chạy script quét UI-6.10 (từ tiếng Anh / mã trạng thái, số thẻ chỉ số) trên các màn đã sửa – sạch mới báo xong.

### 4.6 DEFINITION OF DONE – chuẩn "Hoàn thành" bắt buộc (D-92 b, anh chốt 2026-11-24)
Task chỉ được ghi **DONE** trong ROADMAP / PROGRESS khi đủ **cả 7 điểm**; thiếu điểm nào → trạng thái **`PARTIAL`** + ghi rõ trong mục "Chưa làm / còn rủi ro".
1. **DB dev:** mọi migration / seed / script dữ liệu của task **đã chạy thật** (`DryRun=0`) trên DB dev, chạy lại lần 2 = 0 việc; số liệu ghi PROGRESS. Không ghi "chờ anh backup / chờ anh chạy" cho DB dev (D-92 a – backup chỉ bắt buộc khi có staging / production, anh sẽ báo).
2. **Giao diện thật:** API + FE chạy trên DB dev, kiểm bằng trình duyệt / Chrome headless, có ảnh (1440 + 390 với màn có giao diện).
3. **Đối chiếu từng dòng** AC / bảng anh chốt, mỗi dòng có bằng chứng (ảnh, test, truy vấn).
4. **Nguồn trùng đồng bộ:** seed, script, `catalog.js`, CodeGen, HDSD… cùng cập nhật; nguồn sinh từ CodeGen phải có test so khớp (vd `Tests/Menu/CatalogMenuTests`).
5. **Không chỉ admin:** kiểm bằng tài khoản **không phải admin** cho các vai trò liên quan (tài khoản thử `uat.<vai trò>` trên DB dev) + trường hợp biên / lỗi / không có quyền.
6. **Xanh toàn bộ:** `dotnet test`, `full-flow.http` (`tools/seed-demo`), build FE; mở mọi màn liên quan không lỗi JS / 500 / trang trắng.
7. **PROGRESS trung thực:** có mục **"Chưa làm / còn rủi ro"**; còn mục nào thì không ghi DONE.

## 5. Tra cứu nhanh

| Cần gì | Xem ở đâu |
|---|---|
| Bảng / cột có tồn tại không, ý nghĩa từng bảng / cột | `docs/reference/DB_SCHEMA_124.md` (có mô tả từ Dictionary + BRD); gốc: `.sql`, `20261113_Dictionary_DB_AppDelicare.xlsx`, `DeliCare_Database_Dictionary_BRD.xlsx` |
| Bảng nào tham chiếu bảng nào | `docs/reference/LOGICAL_RELATIONS.md` (224 quan hệ + 80 cột chưa rõ) |
| **Mẫu code chuẩn để chép** | `Controllers/WarehouseController.cs` + `Services/Implement/WarehouseService.cs` (CRUD); `UserController` / `UserService` (action nghiệp vụ: ResetPassword, SaveIsActive, ChangePassword); `UserRoleController` / `UserRoleService` (ma trận quyền); `ObjectMenuService` (menu); `LoginService` (đăng nhập) |
| Code cũ đã có gì, còn thiếu gì (theo màn hình) | `docs/claude/LEGACY_REVIEW.md` (T45R tạo, cập nhật mỗi task) |
| Chức năng / dữ liệu / logic 1 màn hình cần có | `docs/reference/DELICARE_API_Specification.md`, `DELICARE_API_Catalog.csv` – **chỉ là yêu cầu nghiệp vụ**, API viết theo khung cũ |
| Trạng thái hợp lệ | `docs/reference/STATUS_CATALOG.md` |
| Cặp cột trùng (OrderNo/OrderNumber, DeleteddBy/DeletedBy…) | `docs/reference/DEPRECATED_COLUMNS.md` |
| Biểu mẫu in thật, menu thật, quy tắc định lượng / hao hụt, quy trình báo giá → PO | `docs/claude/BUSINESS_DOCS.md` |
| Sơ đồ tổ chức thật, luồng vận hành 9 bước, nhóm quyền, ma trận quyền | `docs/claude/ORG_STRUCTURE.md` (ảnh gốc: `docs/reference/org/`) |
| Ngoài phạm vi (MISA lo kế toán / lương / công nợ; bán lẻ, bảo trì, CRM, CAPA để R2) | `docs/claude/DECISIONS.md` D-21…D-23 |
| Khung chuẩn = code cũ, bỏ API v1 | `docs/claude/DECISIONS.md` D-30 |
| Luồng nghiệp vụ end-to-end | `docs/reference/DELICARE_Business_Workflow.md` |
| Ngưỡng, mã chứng từ, ca, nhiệt độ, KPI | `docs/claude/BUSINESS_RULES.md` |
| Lỗi đã biết trong code hiện tại | `docs/claude/CODE_AUDIT.md` |
| 34 hàm gateway cũ | `docs/claude/LEGACY_GATEWAY.md` |
| Sheets → bảng dbo | `docs/claude/MIGRATION_SHEETS_TO_DBO.md` |
| Agent, công cụ, lịch chạy | `docs/claude/AGENTS.md` |
| Bot Telegram / kênh tin nhắn: kiến trúc source + tính năng, trải nghiệm, thông báo (D-62) | `docs/claude/BOT_SOURCE_ARCHITECTURE.md`, `docs/claude/BOT_TELEGRAM.md` |

---

## 6. Quy trình làm việc (mỗi phiên)

1. Đọc `docs/claude/PROGRESS.md` (phần "Trạng thái hiện tại") và `docs/claude/DECISIONS.md` (phần "Chờ anh chốt").
2. Chọn task: **theo bảng "★ ƯU TIÊN – RELEASE 1" ở đầu ROADMAP**. Lấy task chưa DONE đầu tiên theo thứ tự lát R1.0 → R1.7, hoặc task anh chỉ định (`/task T13`). **Không làm task `DEFERRED(R2)`.** Nếu task bị chặn bởi quyết định chưa chốt thì bỏ qua, lấy task kế tiếp không phụ thuộc. Hết một lát thì deploy staging, cập nhật UAT_CHECKLIST và dừng lại báo anh.
3. Tạo nhánh `feat/<TaskId>-<slug>` từ nhánh hiện tại.
4. Viết kế hoạch ngắn (3–8 bước) vào PROGRESS dưới tiêu đề task, rồi làm.
5. **Rà soát code cũ trước khi viết** (mục 4.1): tìm Controller / Service / SP / Request / ViewModel của entity và các service liên quan; ghi vào kế hoạch "đã có gì – bổ sung gì – ở file nào". Có rồi thì dùng lại / bổ sung vào đúng file đó. Đối chiếu bảng/cột với `DB_SCHEMA_124.md` **trước khi** viết entity, query hay DTO.
6. Build và test. Sửa tới khi xanh. Không tắt test để cho qua.
   - **Trước mỗi lần gộp `main` (D-61, T48.5):** test `SqlServer2022CompatibilityTests` xanh **và** kiểm trên SQL Server 2022 thật (Docker – `deploy/README.md` mục 11: DB trống → `db-init` 2 lần → `full-flow.http` đạt); ghi kết quả vào PROGRESS. Máy chưa có Docker → ghi rõ "chưa kiểm 2022 thật" khi báo anh.
7. Tự rà lại theo **tiêu chí nghiệm thu** của task. Đánh dấu từng tiêu chí đạt hay chưa.
8. Cập nhật:
   - ROADMAP: đổi trạng thái task (`TODO` → `DONE` / `PARTIAL` / `BLOCKED`) – `DONE` chỉ khi đủ DoD §4.6.
   - PROGRESS: ghi đã làm gì, file chính, cách kiểm tra, việc còn lại.
   - DECISIONS: GAP hoặc câu hỏi mới.
   - **`docs/reference/DELICARE_API_Catalog.xlsx`, sheet `API_Catalog`**: với mỗi dòng spec mà task làm, cập nhật cột **J Trạng thái** (`Đã xong` / `Một phần` / `Đang làm` / `Chưa bắt đầu` / `Không làm`), **L API thực tế (khung cũ)**, **M Task**, ghi chú ở **K** nếu cần. Không sửa cột A–I, không thêm / xóa dòng. Đây là **ngoại lệ duy nhất** của luật "không sửa `docs/reference/*`" (anh yêu cầu 2026-11-16). Dùng openpyxl, giữ định dạng; sheet `Tong_hop` tự tính.
9. Commit trên nhánh task. Báo anh **ngắn gọn**: xong gì, kiểm tra thế nào (theo DoD §4.6), cần anh làm gì (vd. nhập secret, chạy trên DB không phải dev). Script DB dev em tự chạy thật (D-92 a).
10. Nếu anh nói "làm tiếp", "tiếp" hoặc `/next` thì quay lại bước 1.

**Khi nào dừng để hỏi anh:**
> **Luật hỏi (D-49):** mọi câu hỏi / việc cần anh xác nhận **phải kèm phương án đề xuất tốt nhất của em**, theo mẫu:
> - **Vấn đề:** 1–2 câu, nêu chỗ (file / API / bảng).
> - **Đề xuất (em chọn):** làm gì cụ thể + lý do; ảnh hưởng (dữ liệu, API, FE, deploy).
> - **Phương án khác:** 1–2 phương án, mỗi cái 1 dòng ưu / nhược.
> - **Nếu anh chưa trả lời:** em làm phần không phụ thuộc trước; phần phụ thuộc thì ghi `PROPOSED` theo đề xuất, **không** làm việc không đảo ngược được (DB không phải dev, xóa dữ liệu, push / gộp `main`).
> Không hỏi câu trống kiểu "anh muốn thế nào?". Ghi câu hỏi + đề xuất vào DECISIONS (GAP / PROPOSED).
- Cần tạo bảng hoặc cột.
- Thấy cần một kiểu code / API **khác khung cũ** (route mới, base class mới, thư viện mới) → không tự làm, hỏi anh.
- Cần chạy gì lên DB không phải dev.
- Cần bí mật hoặc token thật.
- Hai tài liệu nguồn mâu thuẫn mà DECISIONS chưa phân xử.
- Việc frontend (mọi màn hình) → không tự làm (D-49); ghi việc cần dev FE làm vào handoff / DECISIONS mục "Việc cho frontend".

Những việc khác em tự quyết theo tài liệu và ghi lại lý do trong PROGRESS.
