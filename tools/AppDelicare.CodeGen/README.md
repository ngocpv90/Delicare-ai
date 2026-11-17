# AppDelicare.CodeGen – sinh mã từ DDL 124 bảng

Console .NET 8, thay bộ sinh T4 + OrmLite cũ (`AppDelicare/AppDelicare.Generator`, chỉ chạy trong Visual Studio và đọc DB live).

## Đầu vào

| Nguồn | Dùng để |
|---|---|
| `docs/reference/20261113_structure_db_appdelicare.sql` | Bảng, cột, kiểu, NULL, `UNIQUE`, `DEFAULT` |
| `docs/reference/DEPRECATED_COLUMNS.md` | Cặp cột cũ → mới: Request / ViewModel / SP chỉ dùng cột mới; Service ghi song song cột cũ |
| `docs/reference/LOGICAL_RELATIONS.md` | Cột `XxxId` → bảng đích: JOIN lấy nhãn `XxxName` trong SP `GetAll` |
| `Metadata.cs` | Tên class bỏ hậu tố legacy (`Dishe` → `Dish`…, CODE_AUDIT mục C); danh sách sổ chỉ ghi thêm (CLAUDE.md 3.3) |

## Chạy

```powershell
cd tools/AppDelicare.CodeGen
dotnet run                                    # mọi bảng
dotnet run -- --only Vehicle,TemperatureLog   # chỉ vài bảng (tên bảng SQL hoặc tên class)
dotnet run -- --dry-run -v                    # xem sẽ ghi gì, không ghi
dotnet run -- --only Vehicle --force          # ghi đè cả file đã có của Vehicle (cẩn thận: mất phần sửa tay)
dotnet run -- --merge --layers request,viewmodel  # T45R: bổ sung cột DDL còn thiếu vào DTO cũ, giữ phần sửa tay
dotnet run -- --layers entity,dbcontext       # chỉ một số lớp
```

| Cờ | Ý nghĩa |
|---|---|
| `--only <A,B>` | Chỉ sinh các bảng này (file tổng hợp vẫn sinh lại đủ mọi bảng) |
| `--skip-existing` | (mặc định) không ghi đè Request / ViewModel / Repository / Service / Controller / SP đã có |
| `--force` | Ghi đè cả các file trên |
| `--merge` | (T45R) Với Request / ViewModel do dev sở hữu: chỉ **thêm** thuộc tính còn thiếu theo DDL (không sửa / xóa dòng cũ, không thêm `[Required]` mới). File đủ cột thì không ghi lại. Vd. `dotnet run -- --merge --layers request,viewmodel` |
| `--layers <..>` | `entity, repository, request, search, viewmodel, service, controller, sp, dbcontext, unitofwork, automapper` |
| `--dry-run`, `-v` | Không ghi / in cả danh sách file bỏ qua |
| `--root <path>` | Thư mục gốc repo (mặc định: tìm `CLAUDE.md` từ thư mục hiện tại đi lên) |

Sau khi chạy: `dotnet build AppDelicare/AppDelicare.sln`, `dotnet test`, rồi chạy các SP mới trong `DB/sp/` lên **DB dev** (không bao giờ lên DB thật).

## Sinh ra những gì

| Lớp | File | Ghi đè? |
|---|---|---|
| Entity | `Model/Entity/<Class>.cs` – `[Table("TênThật")]`, kế thừa `BaseEntity`, `[MaxLength]`, `[Required]`, `[Column(TypeName)]` cho decimal / datetime / date, giá trị DEFAULT hằng số | **Luôn** (entity chỉ là dữ liệu – đồng bộ với DDL) |
| DbContext | `Data/Connection/DatabaseContext.Generated.cs` – DbSet mọi bảng; `Ignore` thuộc tính BaseEntity không có cột ở bảng đó (`DeletedBy`, `DeletedDate`…); kiểu cột audit | **Luôn** |
| UnitOfWork | `Data/Interface/IUnitOfWork.Generated.cs`, `Data/Repositories/UnitOfWork.Generated.cs` | **Luôn** |
| AutoMapper | `Services/Mapping/AutoMapperGeneratedProfile.cs` (T23: chuyển từ WebApi để AppDelicare.Job dùng chung) – map Request → Entity cho bảng chưa có trong `AutoMapperConfiguraion.cs` | **Luôn** |
| Repository | `I<Class>Repository`, `<Class>Repository` | Chỉ khi chưa có |
| Request / SearchRequest / ViewModel | Request: cột nghiệp vụ + `[Required]` (NOT NULL không DEFAULT) + `[MaxLength]`. SearchRequest: lọc theo `XxxId`, `…Status`, `…Type`. ViewModel: cột + `XxxName` (nhãn bảng đích: DisplayName → Name → FullName → số chứng từ → Code) | Chỉ khi chưa có |
| Service / Controller | Theo mẫu `Warehouse*` (route cũ `api/<Class>/GetAll|Insert|Update|Delete|GetById|GetAllData`). Kiểm tra trùng theo `UNIQUE` của DDL + `Code`. Gán DEFAULT khi client không gửi. Sổ chỉ ghi thêm: **không có Update / Delete**. Xóa: **không** dùng SQL động `uspSystem_ExecuteQuery` (kiểm tra tham chiếu làm ở T07 – ReferenceGuard) | Chỉ khi chưa có |
| SP | `DB/sp/usp<Class>_GetAll.sql` – `CREATE OR ALTER`, tham số có mặc định, `SearchText` trên cột mã / số / tên, lọc, JOIN nhãn, sort `ascend / descend` theo tên camelCase, phân trang `OFFSET / FETCH`. Khai báo thêm mọi tham số mà service cũ đang truyền | Chỉ khi chưa có |

Bỏ qua (chỉ sinh entity + repository): bảng `_Legacy_20261113` (chỉ đọc), bảng không theo khuôn BaseEntity (`CoreDataMergeConflict` – Id `bigint`, không có cột audit).

## Kiểm tra tự động

`AppDelicare.Tests/Schema/SchemaMappingTests.cs` đọc DDL và đối chiếu model EF: mọi DbSet → bảng có thật, mọi property → cột có thật, mọi bảng / cột của DDL đều được map, kiểu C# khớp kiểu SQL.

## Ghi chú T45R (D-30 – khung chuẩn là code cũ)
- Bỏ layer `crud` (metadata cho CRUD v1 generic); `CrudMetadataGen.cs` ở `_obsolete/v1/tools`.
- Request không nhận cột do lệnh nghiệp vụ / hệ thống ghi: `PassWord`, `ApprovedAt/By`, `LockedAt/By`, `SnapshotJson` (`Metadata.NotInRequest`).
- SP `usp<Entity>_GetAll` của bảng có `EffectiveFrom` + `EffectiveTo` nhận thêm `@EffectiveOn DATE = NULL` (lọc đang hiệu lực tại ngày).
- Service sinh không thêm kiểm trùng `Code` đơn lẻ khi DDL đã có UNIQUE chứa `Code` (vd. (Code, VersionNo)).
- Service / Controller đã sửa tay phải bỏ dòng `<auto-generated>` ở đầu file để CodeGen không ghi đè.
- (T45B.5) Layer `status`: sinh `AppDelicare.Model/Helper/StatusCatalog.Generated.cs` từ `docs/reference/STATUS_CATALOG.md` (luôn sinh lại).
