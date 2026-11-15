# DB – Script SQL của DELICARE

| Thư mục | Nội dung | Ai chạy |
|---|---|---|
| `migrations/` | `20261113_restore_structure_db_appdelicare.sql`: script **chính thức của PO**, nâng DB cũ (91 bảng) lên lược đồ chuẩn 124 bảng `dbo`. Nguồn: `D:\Duan\Delicare\SQLScript`. | Anh duyệt; chỉ chạy trên DB dev / UAT **sau khi backup** |
| `sp/` | Stored procedure (`CREATE OR ALTER`), một file mỗi SP: `usp<Entity>_GetAll.sql`… Gồm SP lấy ra từ DB dev (T00) và SP mới do CodeGen sinh. | Claude Code chạy trên DB dev |
| `seed/` | Dữ liệu cấu hình (Shift, ObjectMenu, UserGroup, KpiMetricDefinition…). Dữ liệu demo gắn `[DEMO]`. | Claude Code chạy trên DB dev |

Không có script tạo bảng / cột ngoài `migrations/` đã được PO duyệt.
