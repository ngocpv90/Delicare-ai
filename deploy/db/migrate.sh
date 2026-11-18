#!/bin/bash
# DELICARE – ĐỒNG BỘ DATABASE khi khởi động Docker (dùng cho docker-compose.yml ở gốc repo). Chạy lại bao nhiêu lần cũng được.
#
#   1. So cấu trúc DB với DDL chuẩn (docs/reference/20261113_structure_db_appdelicare.sql): thiếu bảng / cột nào thì liệt kê.
#   2. Có thiếu + DB_AUTO_MIGRATE=1 → backup rồi chạy lần lượt các script trong DB/migrations/*.sql (theo tên file),
#      mỗi script CHỈ CHẠY 1 LẦN (đánh dấu bằng extended property của database – không thêm bảng theo dõi).
#      Có thiếu + không bật DB_AUTO_MIGRATE → DỪNG kèm danh sách (thay vì để API lỗi 500).
#   3. Gọi deploy/docker/db-init.sh: DB trống thì tạo 124 bảng; cập nhật stored procedure (CREATE OR ALTER) + seed còn thiếu.
#
# Script này KHÔNG tự nghĩ ra bảng / cột: cấu trúc chỉ đến từ DDL chuẩn và các script chính thức trong DB/migrations/.
# Backend thêm thay đổi cấu trúc mới = thêm 1 file DB/migrations/<yyyymmdd>_<tên>.sql (chạy lại được) + cập nhật DDL chuẩn.
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
trap 'rm -rf "$TMP_DIR"' EXIT
chmod 700 "$TMP_DIR"

echo "== DELICARE đồng bộ database → ${DB_SERVER:-db,1433} / $DB_NAME"
"${SQL[@]}" -d master -Q "SELECT 1" >/dev/null || die "Không kết nối được SQL Server ${DB_SERVER:-db,1433} (kiểm tra DB_SERVER / DB_USER / DB_PASSWORD)"

DB_EXISTS=$(scalar master "SELECT CASE WHEN DB_ID(N'$(sql_str "$DB_NAME")') IS NULL THEN 0 ELSE 1 END")
TABLES=0
[ "$DB_EXISTS" = "1" ] && TABLES=$(scalar "$DB_NAME" "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0")

# ---- Danh sách bảng / cột chuẩn lấy từ DDL (CREATE TABLE [dbo].[T]( … dòng "<tab>[Cột] [kiểu]") ----
DDL="$REPO/docs/reference/20261113_structure_db_appdelicare.sql"
[ -f "$DDL" ] || die "Không thấy $DDL"
build_check() {
  # In ra 1 batch T-SQL trả về các dòng "TABLE <bảng>" / "COLUMN <bảng>.<cột>" còn thiếu trong DB.
  echo "SET NOCOUNT ON; DECLARE @e TABLE (t sysname NOT NULL, c sysname NOT NULL);"
  tr -d '\r' < "$DDL" | awk '
    /^CREATE TABLE \[dbo\]\.\[/ { t = $0; sub(/^CREATE TABLE \[dbo\]\.\[/, "", t); sub(/\]\(.*$/, "", t); next }
    t != "" && /^[ \t]+\[[^]]+\] \[/ { c = $0; sub(/^[ \t]+\[/, "", c); sub(/\].*$/, "", c); n++; rows[n] = "(N\x27" t "\x27,N\x27" c "\x27)"; next }
    t != "" && /^(\) ON|\)|[ \t]*CONSTRAINT|[ \t]*PRIMARY KEY|GO)/ { t = "" }
    END { for (i = 1; i <= n; i++) { if ((i - 1) % 500 == 0) printf "%sINSERT @e (t, c) VALUES ", (i > 1 ? ";\n" : ""); else printf ","; printf "%s", rows[i] } print ";" }'
  cat <<'SQL'
SELECT x FROM (
  SELECT 1 AS o, N'TABLE ' + d.t AS x FROM (SELECT DISTINCT t FROM @e) d WHERE OBJECT_ID(N'dbo.' + QUOTENAME(d.t), N'U') IS NULL
  UNION ALL
  SELECT 2, N'COLUMN ' + e.t + N'.' + e.c FROM @e e WHERE OBJECT_ID(N'dbo.' + QUOTENAME(e.t), N'U') IS NOT NULL AND COL_LENGTH(N'dbo.' + QUOTENAME(e.t), e.c) IS NULL
) q ORDER BY o, x;
SQL
}
MISSING_FILE="$TMP_DIR/missing.txt"
check_drift() {
  build_check > "$TMP_DIR/check.sql"
  "${SQL[@]}" -d "$DB_NAME" -h -1 -W -i "$TMP_DIR/check.sql" | tr -d '\r' | grep -E '^(TABLE|COLUMN) ' > "$MISSING_FILE" || true
  MISSING_TABLES=$(grep -c '^TABLE ' "$MISSING_FILE" || true)
  MISSING_COLUMNS=$(grep -c '^COLUMN ' "$MISSING_FILE" || true)
}
print_missing() {
  echo "  ! Thiếu $MISSING_TABLES bảng, $MISSING_COLUMNS cột so với DDL chuẩn:"
  head -n 40 "$MISSING_FILE" | sed 's/^/      /'
  TOTAL=$(wc -l < "$MISSING_FILE")
  [ "$TOTAL" -le 40 ] || echo "      … và $((TOTAL - 40)) dòng khác"
}

if [ "$TABLES" != "0" ]; then
  EXPECTED=$(tr -d '\r' < "$DDL" | grep -c '^CREATE TABLE \[dbo\]\.\[' || true)
  check_drift
  if [ "$MISSING_TABLES" = "0" ] && [ "$MISSING_COLUMNS" = "0" ]; then
    echo "  = Cấu trúc khớp DDL chuẩn ($EXPECTED bảng) – không cần migrate"
  else
    print_missing
    if [ "${DB_AUTO_MIGRATE:-0}" = "1" ]; then
      shopt -s nullglob
      PENDING=()
      for f in "$REPO"/DB/migrations/*.sql; do
        name="${f##*/}"
        prop="DelicareMigration:${name:0:100}"
        done_flag=$(scalar "$DB_NAME" "SELECT COUNT(*) FROM sys.extended_properties WHERE class = 0 AND name = N'$(sql_str "$prop")'")
        [ "$done_flag" = "0" ] && PENDING+=("$f")
      done
      if [ "${#PENDING[@]}" = "0" ]; then
        echo "  ! Mọi script trong DB/migrations đã chạy trên DB này nhưng cấu trúc vẫn thiếu."
      else
        if [ "${DB_BACKUP_BEFORE_MIGRATE:-1}" = "1" ]; then
          BAK="${DB_NAME}_premigrate_$(date +%Y%m%d_%H%M%S).bak"
          echo "  + Backup trước khi migrate → $BAK (thư mục backup mặc định của SQL Server)"
          "${SQL[@]}" -d master -Q "BACKUP DATABASE [${DB_NAME//]/]]}] TO DISK = N'$(sql_str "$BAK")' WITH COPY_ONLY, INIT, NAME = N'DELICARE pre-migrate';" \
            || die "Backup thất bại – KHÔNG migrate. Sửa quyền / dung lượng backup, hoặc tự backup rồi đặt DB_BACKUP_BEFORE_MIGRATE=0."
        else
          echo "  ! DB_BACKUP_BEFORE_MIGRATE=0 – migrate không có backup tự động"
        fi
        for f in "${PENDING[@]}"; do
          name="${f##*/}"
          prop="DelicareMigration:${name:0:100}"
          echo "  + Migrate: DB/migrations/$name"
          # Bỏ "USE [AppDelicare]" để chạy được với tên DB khác (như db-init.sh làm với DDL).
          tr -d '\r' < "$f" | sed -E 's/^USE \[[^]]+\];?[[:space:]]*$//' > "$TMP_DIR/migration.sql"
          "${SQL[@]}" -d "$DB_NAME" -i "$TMP_DIR/migration.sql" || die "Lỗi khi chạy DB/migrations/$name – DB giữ nguyên trạng thái sau lỗi, xem thông báo phía trên (có backup nếu đã bật)."
          "${SQL[@]}" -d "$DB_NAME" -Q "EXEC sys.sp_addextendedproperty @name = N'$(sql_str "$prop")', @value = N'$(date +%Y-%m-%dT%H:%M:%S)';" \
            || die "Không ghi được dấu đã chạy cho $name"
        done
      fi
      check_drift
      if [ "$MISSING_TABLES" != "0" ] || [ "$MISSING_COLUMNS" != "0" ]; then
        echo "  ! Sau migrate vẫn còn thiếu:"
        print_missing
        [ "${DB_ALLOW_SCHEMA_DRIFT:-0}" = "1" ] || die "Cấu trúc DB chưa khớp DDL chuẩn. Cần thêm script trong DB/migrations (bên backend), hoặc dùng DB mới (đổi DB_NAME). Tạm bỏ qua: DB_ALLOW_SCHEMA_DRIFT=1."
      else
        echo "  = Sau migrate: cấu trúc khớp DDL chuẩn"
      fi
    elif [ "${DB_ALLOW_SCHEMA_DRIFT:-0}" = "1" ]; then
      echo "  ! DB_ALLOW_SCHEMA_DRIFT=1 – chạy tiếp dù thiếu cấu trúc (API sẽ lỗi ở các màn liên quan)"
    else
      die "DB $DB_NAME thiếu bảng / cột so với bản code này. Chọn 1 cách: (a) đặt DB_AUTO_MIGRATE=1 trong .env để tự nâng cấp bằng DB/migrations (có backup trước); (b) đổi DB_NAME sang một DB mới để tạo đủ 124 bảng; (c) DB_ALLOW_SCHEMA_DRIFT=1 để chạy tạm."
    fi
  fi
else
  echo "  = Database trống / chưa có – sẽ tạo đủ bảng từ DDL chuẩn"
fi

# ---- Stored procedure + seed (và tạo bảng nếu DB trống) ----
INIT="${DB_INIT_SCRIPT:-/db-init.sh}"
[ -f "$INIT" ] || INIT="$REPO/deploy/docker/db-init.sh"
[ -f "$INIT" ] || die "Không thấy db-init.sh (gắn deploy/docker/db-init.sh vào /db-init.sh)"
exec /bin/bash "$INIT"
