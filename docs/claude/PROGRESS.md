# PROGRESS – Nhật ký tiến độ (Claude Code cập nhật sau mỗi task)

## Trạng thái hiện tại
- Release: **R1 – CHỈ BACKEND** (.NET + SQL, đủ API cho mockup; Angular ngoài phạm vi – D-20) · Lát hiện tại: **R1.0 Nền móng**
- Task đang làm: T00 DONE – task kế tiếp: **T01** (bí mật ra khỏi code)
- Nhánh: `chore/T00-claude-setup` (chưa merge, chưa push)
- Build backend: OK – 0 lỗi, 10 cảnh báo (2026-11-14) · Build frontend: ngoài phạm vi · Test: chưa có project test
- Việc anh cần làm: trả lời D-21 (endpoint `api/AuditChange/DeleteData`); merge nhánh T00 khi anh thấy ổn.

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

**Phát hiện thêm (ghi CODE_AUDIT):** A14 `GET api/AuditChange/DeleteData` xóa vật lý toàn DB (rất cao, D-21); A15 thiếu 74 SP; A16 `uspSystem_BackupDB`; A17 AutoMapper lỗ hổng.

File chính: `.claude/*`, `DB/sp/*.sql`, `DB/sp/_MISSING_FROM_DB.md`, `DB/_ssms_diagrams/*`, `DB/README.md`, `docs/claude/{PROGRESS,ROADMAP,DECISIONS,CODE_AUDIT}.md`.
Kiểm tra: `dotnet build` 0 lỗi; đăng nhập + gọi API bảo vệ OK; `git status` không có thay đổi trong `app-delicare-client/`.
AC: [x] nhánh + commit · [x] build baseline · [x] báo cáo lệch DB · [x] DB/sp + danh sách SP thiếu · [x] đăng nhập API · [x] danh sách vị trí bí mật.
Còn lại / GAP: không có GAP dữ liệu mới.
Cần anh: D-21; merge nhánh khi thấy ổn; chuẩn bị đổi khóa JWT / license / mật khẩu `sa` cho T01.
