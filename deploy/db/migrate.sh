#!/bin/bash
# DELICARE – ĐỒNG BỘ DATABASE khi khởi động Docker (dùng cho docker-compose.yml ở gốc repo). Chạy lại bao nhiêu lần cũng được.
#
#   1. Xem trước migration cấu trúc bảng (DB/migrations – deploy/db/migrations.sh): DB đã có bảng mà có migration THAY ĐỔI cấu trúc đang chờ
#      → cần DB_AUTO_MIGRATE=1 (+ backup trước); không thì DỪNG kèm danh sách (thay vì để API lỗi 500).
#   2. Gọi deploy/docker/db-init.sh: chạy migration (DB trống → baseline 124 bảng; DB cũ 91 bảng → nâng cấp; migration mới → 1 lần),
#      kiểm cấu trúc so với baseline, rồi cập nhật stored procedure (CREATE OR ALTER) + seed còn thiếu.
#
# Script này KHÔNG tự nghĩ ra bảng / cột: cấu trúc chỉ đến từ DB/migrations/ (baseline = DDL chuẩn của PO + các migration chính thức).
# Backend thêm thay đổi cấu trúc mới = thêm 1 file DB/migrations/<yyyymmdd>_<nn>_<tên>.sql (chạy lại được) – xem DB/README.md mục Migration.
#
# Biến môi trường:
#   DB_AUTO_SYNC=1               bật đồng bộ (0 = bỏ qua toàn bộ, API chạy với DB hiện có)
#   DB_AUTO_MIGRATE=0            1 = cho phép chạy DB/migrations khi DB thiếu bảng / cột (thay đổi cấu trúc – cần người có trách nhiệm đồng ý)
#   DB_BACKUP_BEFORE_MIGRATE=1   backup DB (COPY_ONLY, thư mục backup mặc định của SQL Server) trước khi migrate; 0 = bỏ qua
#   DB_ALLOW_SCHEMA_DRIFT=0      1 = vẫn chạy tiếp dù còn thiếu bảng / cột (API sẽ lỗi ở các màn liên quan)
#   DB_SERVER, DB_USER, SA_PASSWORD, DB_NAME, ADMIN_PASSWORD…  như db-init.sh
set -euo pipefail

REPO="${REPO_ROOT:-/repo}"
DB_NAME="${DB_NAME:-AppDelicare}"
die() { printf 'LỖI: %s\n' "$*" >&2; exit 1; }
sql_str() { printf '%s' "${1//\'/\'\'}"; }

if [ "${DB_AUTO_SYNC:-1}" != "1" ]; then
  echo "== DB_AUTO_SYNC != 1 – bỏ qua đồng bộ database."
  exit 0
fi

SQLCMD_BIN="${SQLCMD_BIN:-/opt/mssql-tools18/bin/sqlcmd}"
if [ "${DB_TRUSTED_AUTH:-0}" = "1" ]; then
  AUTH=(-E)
else
  [ -n "${SA_PASSWORD:-}" ] || die "Chưa đặt mật khẩu SQL (DB_PASSWORD trong .env)"
  export SQLCMDPASSWORD="$SA_PASSWORD"
  AUTH=(-U "${DB_USER:-sa}")
fi
SQL=("$SQLCMD_BIN" -S "${DB_SERVER:-db,1433}" "${AUTH[@]}" -C -b -I -f 65001)
scalar() { "${SQL[@]}" -d "$1" -r1 -h -1 -W -Q "SET NOCOUNT ON; $2" | tr -d '[:space:]'; }

TMP_DIR=$(mktemp -d)
# Git Bash trên Windows: sqlcmd của Windows hiểu "/tmp/…" là tùy chọn "/t…" → đổi sang đường dẫn C:… (Linux / Docker không có cygpath – giữ nguyên).
command -v cygpath >/dev/null 2>&1 && TMP_DIR=$(cygpath -w "$TMP_DIR")
trap 'rm -rf "$TMP_DIR"' EXIT
chmod 700 "$TMP_DIR"

echo "== DELICARE đồng bộ database → ${DB_SERVER:-db,1433} / $DB_NAME"
"${SQL[@]}" -d master -Q "SELECT 1" >/dev/null || die "Không kết nối được SQL Server ${DB_SERVER:-db,1433} (kiểm tra DB_SERVER / DB_USER / DB_PASSWORD)"

DB_EXISTS=$(scalar master "SELECT CASE WHEN DB_ID(N'$(sql_str "$DB_NAME")') IS NULL THEN 0 ELSE 1 END")
TABLES=0
[ "$DB_EXISTS" = "1" ] && TABLES=$(scalar "$DB_NAME" "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0")

# ---- 2026-11-18: cấu trúc bảng = MIGRATION (DB/migrations) – bộ chạy chung deploy/db/migrations.sh (db-init.sh chạy thật).
# Ở đây chỉ XEM TRƯỚC kế hoạch để giữ chốt an toàn của server dev / test: có migration THAY ĐỔI cấu trúc (RUN) trên DB đã có bảng
# → cần DB_AUTO_MIGRATE=1 (người phụ trách DB đồng ý) + backup trước. Chỉ ghi nhận (ADOPT) / DB trống → không cần.
DB_NAME_SAVED="$DB_NAME"
# shellcheck source=migrations.sh
. "$REPO/deploy/db/migrations.sh"
if [ "$TABLES" != "0" ]; then
  PLAN=$(mig_plan)
  PENDING=$(printf '%s\n' "$PLAN" | awk '$1 == "RUN" { print $2 }')
  if [ -z "$PENDING" ]; then
    echo "  = Cấu trúc: không có migration thay đổi bảng đang chờ"
  elif [ "${DB_AUTO_MIGRATE:-0}" = "1" ]; then
    echo "  ! Migration sẽ chạy:"; printf '%s\n' "$PENDING" | sed 's/^/      /'
    if [ "${DB_BACKUP_BEFORE_MIGRATE:-1}" = "1" ]; then
      BAK="${DB_NAME}_premigrate_$(date +%Y%m%d_%H%M%S).bak"
      echo "  + Backup trước khi migrate → $BAK (thư mục backup mặc định của SQL Server)"
      "${SQL[@]}" -d master -Q "BACKUP DATABASE [${DB_NAME//]/]]}] TO DISK = N'$(sql_str "$BAK")' WITH COPY_ONLY, INIT, NAME = N'DELICARE pre-migrate';" \
        || die "Backup thất bại – KHÔNG migrate. Sửa quyền / dung lượng backup, hoặc tự backup rồi đặt DB_BACKUP_BEFORE_MIGRATE=0."
    else
      echo "  ! DB_BACKUP_BEFORE_MIGRATE=0 – migrate không có backup tự động"
    fi
  elif [ "${DB_ALLOW_SCHEMA_DRIFT:-0}" = "1" ]; then
    echo "  ! DB_ALLOW_SCHEMA_DRIFT=1 – bỏ qua migration chờ (API có thể lỗi ở các màn liên quan):"; printf '%s\n' "$PENDING" | sed 's/^/      /'
    export MIGRATIONS_SKIP=1
  else
    echo "  ! Migration thay đổi cấu trúc đang chờ:"; printf '%s\n' "$PENDING" | sed 's/^/      /'
    die "Chọn 1 cách: (a) DB_AUTO_MIGRATE=1 trong .env để tự chạy migration (có backup trước); (b) đổi DB_NAME sang DB mới (tạo từ baseline); (c) DB_ALLOW_SCHEMA_DRIFT=1 để chạy tạm."
  fi
else
  echo "  = Database trống / chưa có – db-init tạo cấu trúc từ migration baseline"
fi
DB_NAME="$DB_NAME_SAVED"

# ---- Stored procedure + seed (và tạo bảng nếu DB trống) ----
INIT="${DB_INIT_SCRIPT:-/db-init.sh}"
[ -f "$INIT" ] || INIT="$REPO/deploy/docker/db-init.sh"
[ -f "$INIT" ] || die "Không thấy db-init.sh (gắn deploy/docker/db-init.sh vào /db-init.sh)"
exec /bin/bash "$INIT"
