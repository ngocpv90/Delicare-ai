# AppDelicare.Tests

xUnit + FluentAssertions 7 (bản 8 là license trả phí) + EF Core InMemory + `Microsoft.AspNetCore.Mvc.Testing`.

## Chạy

```powershell
cd AppDelicare
dotnet test                                   # toàn bộ
dotnet test --filter "FullyQualifiedName~Security"   # một nhóm
```

## Ba loại test

| Loại | Ở đâu | Cần DB? | Dữ liệu |
|---|---|---|---|
| **Unit** | `Actor/`, `Schema/`, một phần `Ledger/` | Không | EF InMemory, mỗi test một DB riêng |
| **API trong bộ nhớ** | `Integration/V1ShiftsTests`, `Security/PermissionTests`, `Ledger/AppendOnlyAndLedgerTests` | Không | `InMemoryApiFactory`: WebApi thật + EF InMemory + JWT tự ký bằng khóa test; user mặc định là admin |
| **Tích hợp DB dev** | `Integration/HttpPipelineTests`, `Security/SeedTests`, `Ledger/LedgerSqlServerTests` | Có | `ApiFactory`: WebApi thật + SQL Server. Chỉ **đọc**, hoặc ghi trong **transaction rồi ROLLBACK** – không để lại dữ liệu |

Không kết nối / đăng nhập được DB thì test tích hợp DB được **Skip kèm lý do** (`[SkippableFact]`), không báo lỗi.

## Cấu hình cho test tích hợp DB

`ApiFactory` chạy môi trường Development nên đọc **user-secrets của WebApi** (xem `docs/claude/SECRETS_TODO.md`):

| Khóa | Ý nghĩa |
|---|---|
| `ConnectionStrings:MainConnectionString` | DB dev (bắt buộc để WebApi khởi động) |
| `ConnectionStrings:TestDb` | (tùy chọn) DB riêng cho test – có thì thay cho MainConnectionString |
| `Jwt:SigningKey` | Khóa JWT dev |

Tài khoản đăng nhập test: biến môi trường `DELICARE_TEST_USER` / `DELICARE_TEST_PASSWORD`; mặc định tài khoản dev (`docs/claude/LOCAL_DEV.local.md`).

```powershell
cd AppDelicare/AppDelicare.WebApi
dotnet user-secrets set "ConnectionStrings:TestDb" "Server=(localdb)\MSSQLLocalDB;Database=AppDelicare;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true;"
```

## Quy ước khi viết test mới

- Test ghi dữ liệu: dùng `InMemoryApiFactory` / EF InMemory; nếu buộc phải ghi SQL Server thì mở transaction và ROLLBACK (xem `LedgerSqlServerTests`).
- Sổ chỉ ghi thêm (`InventoryTransaction`, `IntegrationLog`…) không xóa được → **không bao giờ** commit dữ liệu test vào sổ trên DB dev.
- Dữ liệu demo gắn `[DEMO]`.
- Test model ↔ DDL (`Schema/SchemaMappingTests`) đọc `docs/reference/20261113_structure_db_appdelicare.sql`: sửa entity thì test này phải xanh.
