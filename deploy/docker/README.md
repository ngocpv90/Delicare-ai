# DELICARE – Docker staging (T48.1) và tự cập nhật DB cho dev FE (T48.3)

`docker-compose.staging.yml` gồm 3 service: `db` (SQL Server 2022, cổng máy **14333**), `db-init` (tạo / cập nhật DB rồi dừng), `api` (cổng **8080**). Không có Angular (xem `deploy/README.md` mục 8).

Mọi lệnh chạy từ **thư mục gốc repo**.

## 1. Lần đầu

```bash
cp deploy/docker/staging.env.example deploy/docker/staging.env   # điền SA_PASSWORD, ADMIN_PASSWORD, JWT_SIGNING_KEY – file này KHÔNG commit (gitignored)
docker compose -f deploy/docker/docker-compose.staging.yml --env-file deploy/docker/staging.env up -d --build
docker compose -f deploy/docker/docker-compose.staging.yml logs -f db-init    # xem khởi tạo DB
```

Lần đầu `db-init` tạo DB, 124 bảng (chỉ khi DB chưa có bảng nào), mọi SP, tài khoản `admin` (hash **PBKDF2** như app – T48.0, tính từ `ADMIN_PASSWORD`), rồi seed cấu hình. Sau khi đăng nhập được và đổi mật khẩu admin, có thể xóa giá trị `ADMIN_PASSWORD` khỏi `staging.env` (chỉ cần khi DB chưa có admin).

## 2. Cập nhật DB sau mỗi lần `git pull` (dev FE)

```bash
git pull && docker compose -f deploy/docker/docker-compose.staging.yml --env-file deploy/docker/staging.env run --rm db-init
```

- `db-init` **chạy lại bao nhiêu lần cũng được**: migration cấu trúc bảng (`DB/migrations`, 2026-11-18) chạy **mỗi file 1 lần** – DB trống → baseline 124 bảng, DB đã có bảng → chỉ ghi nhận, migration mới → chạy 1 lần (xem `DB/README.md` mục Migration); sau đó kiểm thiếu bảng / cột so với baseline (thiếu → dừng, tạm bỏ qua bằng `DB_ALLOW_SCHEMA_DRIFT=1`); SP `CREATE OR ALTER`; seed **chỉ thêm** phần chưa có (không ghi đè chỉnh sửa trên màn hình); tài khoản admin đã có thì giữ nguyên; không đụng dữ liệu nghiệp vụ.
- `--env-file` bắt buộc: compose đọc mật khẩu SA từ `staging.env` (không có thì báo lỗi biến chưa đặt).
- Thứ tự script lấy từ `deploy/db/scripts.order` – cùng file mà `deploy/db/Deploy-Database.ps1` dùng.
- Nếu commit mới có cả code backend (`AppDelicare/`), build lại API: `docker compose -f deploy/docker/docker-compose.staging.yml --env-file deploy/docker/staging.env up -d --build api`.

### Gói sẵn: `update-db.sh` (+ cron tùy chọn)

```bash
bash deploy/docker/update-db.sh            # git pull --ff-only; chỉ chạy db-init khi có thay đổi DB
bash deploy/docker/update-db.sh --force    # chạy db-init dù không có thay đổi
```

- Chỉ chạy `db-init` khi từ **lần cập nhật DB thành công gần nhất** có commit đụng `DB/`, `deploy/db/`, `deploy/docker/db-init.sh` hoặc `docker-compose.staging.yml`. Commit đã áp dụng ghi ở `~/.delicare-update-db.last` (đổi bằng biến `STATE_FILE`). Lần chạy đầu (chưa có file này) luôn chạy `db-init`.
- `db-init` lỗi → không ghi nhận commit → lần chạy sau tự thử lại. `git pull` lỗi (vd. còn sửa đổi chưa commit) → ghi lỗi, không chạy `db-init`.
- Mỗi dòng log có giờ; không in mật khẩu.

Cron mỗi 5 phút (Linux; `crontab -e`). Cron có `PATH` ngắn – thêm dòng `PATH` nếu `docker` / `git` nằm ở chỗ khác; `git pull` phải chạy được không hỏi mật khẩu (SSH key / credential helper):

```cron
PATH=/usr/local/bin:/usr/bin:/bin
*/5 * * * * /đường/dẫn/repo/deploy/docker/update-db.sh >> /đường/dẫn/update-db.log 2>&1
```

Windows (Docker Desktop): chạy `update-db.sh` bằng Git Bash; muốn tự động thì tạo Task Scheduler gọi `"C:\Program Files\Git\bin\bash.exe" -lc "/c/đường/dẫn/repo/deploy/docker/update-db.sh >> /c/đường/dẫn/update-db.log 2>&1"`.

### Xem trước thứ tự script (không cần DB)

```bash
REPO_ROOT=. bash deploy/docker/db-init.sh --dry-run
```

## 3. Dự phòng: server không ra được Internet (không `git pull` được)

Chạy `Deploy-Database.ps1` từ **máy dev FE** (có repo mới + `sqlcmd`) trỏ vào SQL Server của server Docker (cổng 14333). Mật khẩu chỉ đặt trong phiên PowerShell, **không** ghi vào file trong repo:

```powershell
$env:DELICARE_SQL_USER = "sa"
$env:DELICARE_SQL_PASSWORD = Read-Host "Mật khẩu SA"          # hoặc dùng -SqlPassword (Read-Host -AsSecureString)
./deploy/db/Deploy-Database.ps1 -Server "<ip-server>,14333" -Database AppDelicare -TrustServerCertificate
Remove-Item Env:DELICARE_SQL_PASSWORD
```

- `-TrustServerCertificate` vì SQL Server trong Docker dùng chứng chỉ tự ký.
- DB chưa có admin: thêm `-AdminPassword (Read-Host -AsSecureString "Mật khẩu admin")` (hoặc biến `DELICARE_ADMIN_PASSWORD`).
- Xem trước thứ tự: `./deploy/db/Deploy-Database.ps1 -ListOnly`.

## 4. Khi thêm SP / seed mới (dev backend)

Thêm 1 dòng vào **cuối nhóm tương ứng** trong `deploy/db/scripts.order` và vào bảng trong `DB/README.md`. SP quên ghi vẫn được chạy (sau cùng) kèm **cảnh báo**; seed quên ghi **không chạy**.

## 5. Production / tài khoản SQL ứng dụng (T48.4, D-60)
- Production: `docker-compose.production.yml` (nginx + api + job, SQL Server riêng) – cách làm ở `deploy/README.md` mục 10.
- Staging / server dev FE: thêm vào `staging.env` `APP_DB_USER=delicare_app` + `APP_DB_PASSWORD=…` rồi `run --rm db-init` → api / job dùng tài khoản này (không còn dùng sa). Bỏ trống = giữ như cũ (sa).
