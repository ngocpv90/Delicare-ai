#!/bin/bash
# DELICARE – BỘ CHẠY MIGRATION CẤU TRÚC BẢNG (bash). Dùng chung: deploy/docker/db-init.sh, deploy/db/migrate.sh.
# Bản PowerShell cùng quy tắc: deploy/db/Migrations.ps1 (Deploy-Database.ps1). Sửa quy tắc thì sửa CẢ HAI.
#
# Nguồn cấu trúc DUY NHẤT: DB/migrations/*.sql, chạy theo THỨ TỰ TÊN FILE (YYYYMMDD_NN_<tên>.sql). Dòng đầu file có thể khai loại:
#   -- delicare-migration: baseline         124 bảng gốc – CHẠY khi DB chưa có bảng nào; DB đã có bảng → ghi nhận (ADOPT), không chạy.
#   -- delicare-migration: legacy-upgrade   nâng DB cũ (91 bảng) – CHẠY khi DB còn thiếu bảng của baseline; đủ rồi → ADOPT.
#   (không khai / normal)                   thay đổi cấu trúc về sau – CHẠY 1 lần (script phải chạy lại được – IF NOT EXISTS / COL_LENGTH).
# Đã chạy / đã ghi nhận lưu bằng EXTENDED PROPERTY của database (KHÔNG thêm bảng theo dõi – luật DB):
#   tên  DelicareMigration:<tên file>     giá trị  <RUN|ADOPT>|<giờ UTC>|sha256:<16 ký tự đầu, đã bỏ \r>
# File đã chạy mà nội dung đổi (khác mã băm) → CẢNH BÁO (không chạy lại).
#
# Cần sẵn trong script gọi: mảng SQL (sqlcmd + kết nối), hàm scalar <db> <câu lệnh>, hàm die, biến DB_NAME, REPO, TMP_DIR.

MIG_DIR="${MIGRATIONS_DIR:-$REPO/DB/migrations}"
MIG_PREFIX="DelicareMigration:"

mig_files() { ls "$MIG_DIR"/*.sql 2>/dev/null | LC_ALL=C sort; }

mig_mode() {
  local m
  m=$(head -n 5 "$1" | tr -d '\r' | sed -nE 's/^--[[:space:]]*delicare-migration:[[:space:]]*([a-z-]+).*/\1/p' | head -1)
  echo "${m:-normal}"
}

# File baseline (không có → trống). Dùng if (không dùng "[ ] && echo" – set -e của script gọi sẽ dừng khi vòng lặp kết thúc bằng điều kiện sai).
mig_baseline_file() {
  local f
  for f in $(mig_files); do
    if [ "$(mig_mode "$f")" = "baseline" ]; then echo "$f"; return 0; fi
  done
  return 0
}

mig_hash() { tr -d '\r' < "$1" | sha256sum | cut -c1-16; }

mig_sql_str() { printf '%s' "${1//\'/\'\'}"; }

# Giá trị đã ghi (trống = chưa chạy).
mig_recorded() {
  local name="${MIG_PREFIX}$(basename "$1")"
  "${SQL[@]}" -d "$DB_NAME" -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(value AS nvarchar(400)) FROM sys.extended_properties WHERE class = 0 AND name = N'$(mig_sql_str "$name")'" \
    | tr -d '\r' | sed '/^[[:space:]]*$/d' | head -1
}

mig_record() {
  local name="${MIG_PREFIX}$(basename "$1")" value="$2|$(date -u +%Y-%m-%dT%H:%M:%SZ)|sha256:$(mig_hash "$1")"
  "${SQL[@]}" -d "$DB_NAME" -Q "SET NOCOUNT ON;
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND name = N'$(mig_sql_str "$name")')
  EXEC sys.sp_updateextendedproperty @name = N'$(mig_sql_str "$name")', @value = N'$(mig_sql_str "$value")';
ELSE
  EXEC sys.sp_addextendedproperty @name = N'$(mig_sql_str "$name")', @value = N'$(mig_sql_str "$value")';" >/dev/null \
    || die "Không ghi được dấu migration $(basename "$1")"
}

mig_table_count() { scalar "$DB_NAME" "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0 AND name <> N'sysdiagrams'"; }

# Số bảng của baseline còn thiếu trong DB.
mig_missing_baseline() {
  local base
  base=$(mig_baseline_file)
  [ -n "$base" ] || { echo 0; return; }
  local values
  values=$(tr -d '\r' < "$base" | sed -nE 's/^CREATE TABLE \[dbo\]\.\[([^]]+)\].*/(N'"'"'\1'"'"')/p' | paste -sd, -)
  [ -n "$values" ] || { echo 0; return; }
  scalar "$DB_NAME" "SELECT COUNT(*) FROM (VALUES $values) v(t) WHERE OBJECT_ID(N'dbo.' + QUOTENAME(v.t), N'U') IS NULL"
}

# In kế hoạch: mỗi dòng "<RUN|ADOPT|WARN> <tên file>" cho file CHƯA ghi nhận (WARN = đã chạy nhưng nội dung đã đổi).
mig_plan() {
  local tables missing f mode rec
  tables=$(mig_table_count)
  missing=$(mig_missing_baseline)
  for f in $(mig_files); do
    rec=$(mig_recorded "$f")
    if [ -n "$rec" ]; then
      case "$rec" in *"sha256:"*) [ "${rec##*sha256:}" = "$(mig_hash "$f")" ] || echo "WARN $(basename "$f")" ;; esac
      continue
    fi
    mode=$(mig_mode "$f")
    case "$mode" in
      baseline)
        if [ "$tables" = "0" ]; then echo "RUN $(basename "$f")"; tables=124; missing=0; else echo "ADOPT $(basename "$f")"; fi ;;
      legacy-upgrade)
        if [ "$missing" != "0" ]; then echo "RUN $(basename "$f")"; missing=0; else echo "ADOPT $(basename "$f")"; fi ;;
      *) echo "RUN $(basename "$f")" ;;
    esac
  done
}

# Thực hiện kế hoạch. Trả 0 khi xong; die khi lỗi (dừng ngay, file lỗi KHÔNG được ghi nhận → lần sau chạy lại).
mig_apply() {
  local plan action name file ran=0 adopted=0
  plan=$(mig_plan)
  while read -r action name; do
    [ -n "$action" ] || continue
    file="$MIG_DIR/$name"
    case "$action" in
      WARN) echo "  ! CẢNH BÁO: migration $name đã chạy nhưng nội dung file đã ĐỔI sau đó – không chạy lại; thay đổi mới phải là file migration mới" ;;
      ADOPT)
        mig_record "$file" "ADOPT"
        adopted=$((adopted + 1))
        echo "  = Migration $name: DB đã có cấu trúc này – ghi nhận, không chạy" ;;
      RUN)
        sed -E 's/^USE \[[^]]+\];?[[:space:]]*$//' "$file" > "$TMP_DIR/migration.sql"
        "${SQL[@]}" -d "$DB_NAME" -i "$TMP_DIR/migration.sql" || die "Lỗi khi chạy migration $name – dừng (file chưa được ghi nhận, sửa lỗi rồi chạy lại)"
        mig_record "$file" "RUN"
        ran=$((ran + 1))
        echo "  + Migration $name: đã chạy" ;;
    esac
  done <<< "$plan"
  echo "  = Migration: chạy $ran, ghi nhận $adopted, tổng $(mig_files | wc -l | tr -d ' ') file"
}

# Kiểm cấu trúc so với baseline (thiếu bảng / cột) – in danh sách, trả số dòng thiếu.
mig_drift() {
  local base
  base=$(mig_baseline_file)
  [ -n "$base" ] || { echo 0; return; }
  {
    echo "SET NOCOUNT ON; DECLARE @e TABLE (t sysname NOT NULL, c sysname NOT NULL);"
    tr -d '\r' < "$base" | awk '
      /^CREATE TABLE \[dbo\]\.\[/ { t = $0; sub(/^CREATE TABLE \[dbo\]\.\[/, "", t); sub(/\]\(.*$/, "", t); next }
      t != "" && /^[ \t]+\[[^]]+\] \[/ { c = $0; sub(/^[ \t]+\[/, "", c); sub(/\].*$/, "", c); n++; rows[n] = "(N\x27" t "\x27,N\x27" c "\x27)"; next }
      t != "" && /^(\) ON|\)|[ \t]*CONSTRAINT|[ \t]*PRIMARY KEY|GO)/ { t = "" }
      END { for (i = 1; i <= n; i++) { if ((i - 1) % 500 == 0) printf "%sINSERT @e (t, c) VALUES ", (i > 1 ? ";\n" : ""); else printf ","; printf "%s", rows[i] } print ";" }'
    echo "SELECT x FROM (SELECT 1 AS o, N'TABLE ' + d.t AS x FROM (SELECT DISTINCT t FROM @e) d WHERE OBJECT_ID(N'dbo.' + QUOTENAME(d.t), N'U') IS NULL"
    echo "UNION ALL SELECT 2, N'COLUMN ' + e.t + N'.' + e.c FROM @e e WHERE OBJECT_ID(N'dbo.' + QUOTENAME(e.t), N'U') IS NOT NULL AND COL_LENGTH(N'dbo.' + QUOTENAME(e.t), e.c) IS NULL) q ORDER BY o, x;"
  } > "$TMP_DIR/drift.sql"
  "${SQL[@]}" -d "$DB_NAME" -h -1 -W -i "$TMP_DIR/drift.sql" | tr -d '\r' | grep -E '^(TABLE|COLUMN) ' > "$TMP_DIR/drift.txt" || true
  wc -l < "$TMP_DIR/drift.txt" | tr -d ' '
}

# ---- DEPLOY-SYNC-1 (D-134): script CHUYỂN ĐỔI DỮ LIỆU 1 LẦN (deploy/db/once.order) – cùng cơ chế ghi nhận với migration ----
# Ghi nhận: extended property DelicareOnce:<tên file> = <RUN|ADOPT>|<giờ UTC>|sha256:<16 ký tự>. Chạy theo thứ tự trong once.order, với -v DryRun=0.
# ADOPT_ONCE=1: CHỈ ghi nhận, không chạy (DB đã chạy tay các script này – vd DB dev của anh: chạy lại sẽ đổi dữ liệu nạp sau).
ONCE_PREFIX="DelicareOnce:"
once_files() { tr -d '\r' < "$REPO/deploy/db/once.order" | sed 's/#.*//; s/[[:space:]]*$//' | sed '/^$/d'; }
run_once_scripts() {
  local rel name v
  [ -f "$REPO/deploy/db/once.order" ] || return 0
  while IFS= read -r rel; do
    [ -f "$REPO/$rel" ] || die "once.order: không thấy file $rel"
    name="${ONCE_PREFIX}$(basename "$rel")"
    v=$("${SQL[@]}" -d "$DB_NAME" -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(value AS nvarchar(400)) FROM sys.extended_properties WHERE class = 0 AND name = N'$(mig_sql_str "$name")'" | tr -d '\r' | sed '/^[[:space:]]*$/d' | head -1)
    [ -z "$v" ] || continue
    if [ "${ADOPT_ONCE:-0}" = "1" ]; then
      echo "  = Ghi nhận (ADOPT) chuyển đổi $rel"
      MIG_PREFIX="$ONCE_PREFIX" mig_record "$REPO/$rel" ADOPT
    else
      "${SQL[@]}" -d "$DB_NAME" -v DryRun=0 -i "$REPO/$rel" >/dev/null || die "Lỗi khi chạy chuyển đổi $rel"
      MIG_PREFIX="$ONCE_PREFIX" mig_record "$REPO/$rel" RUN
      echo "  + Chuyển đổi 1 lần: $rel"
    fi
  done < <(once_files)
}
