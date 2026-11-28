#!/bin/bash
# DELICARE – khởi tạo / cập nhật DB trong container (T48.1, T48.3). Idempotent: chạy lại bao nhiêu lần cũng được.
#   1. Tạo database nếu chưa có; DB chưa có bảng nào → tạo 124 bảng từ DDL chuẩn của PO. DB đã có bảng → KHÔNG đụng tới bảng.
#   2. Chạy đúng thứ tự trong deploy/db/scripts.order (nguồn duy nhất, dùng chung với deploy/db/Deploy-Database.ps1):
#      SP (CREATE OR ALTER) → tài khoản admin (chỉ khi chưa có, hash PBKDF2 như T48.0) → seed (chỉ thêm phần chưa có).
#   sqlcmd luôn có -f 65001 (UTF-8) – thiếu cờ này tên tiếng Việt bị lỗi font.
#
# Biến môi trường (docker-compose.staging.yml truyền từ staging.env – không ghi giá trị vào repo):
#   SA_PASSWORD     mật khẩu SQL (bắt buộc, trừ khi DB_TRUSTED_AUTH=1)
#   DB_NAME         tên DB (mặc định AppDelicare)
#   ADMIN_PASSWORD  mật khẩu admin app – CHỈ cần khi DB chưa có tài khoản admin
#   ADMIN_USERNAME  mặc định admin
#   APP_DB_USER / APP_DB_PASSWORD  (T48.4, D-60) tài khoản SQL riêng cho api / job – đọc + ghi + EXECUTE, không DDL (deploy/db/app-login.sql);
#                   APP_DB_RESET_PASSWORD=1 để đặt lại mật khẩu login đã có. Không đặt = bỏ qua.
#   Tùy chọn (chạy ngoài container / thử): DB_SERVER (db,1433), DB_USER (sa), DB_TRUSTED_AUTH=1 (-E), SQLCMD_BIN, REPO_ROOT (/repo)
# Xem thứ tự sẽ chạy mà không kết nối DB (từ gốc repo):  REPO_ROOT=. bash deploy/docker/db-init.sh --dry-run
set -euo pipefail

REPO="${REPO_ROOT:-/repo}"
ORDER_FILE="$REPO/deploy/db/scripts.order"
DB_NAME="${DB_NAME:-AppDelicare}"
ADMIN_USERNAME="${ADMIN_USERNAME:-admin}"
DRY_RUN="${DRY_RUN:-0}"
[ "${1:-}" = "--dry-run" ] && DRY_RUN=1

die() { printf 'LỖI: %s\n' "$*" >&2; exit 1; }
sql_str() { printf '%s' "${1//\'/\'\'}"; }   # giá trị đặt trong N'...'

# ---- Đọc danh sách thứ tự ----
[ -f "$ORDER_FILE" ] || die "Không thấy $ORDER_FILE"
ORDER=()
declare -A LISTED=()
while IFS= read -r line || [ -n "$line" ]; do
  line="${line%$'\r'}"
  line="${line%%#*}"
  line="${line#"${line%%[![:space:]]*}"}"
  line="${line%"${line##*[![:space:]]}"}"
  [ -z "$line" ] && continue
  [ -f "$REPO/$line" ] || die "scripts.order: không thấy file $line"
  [ -z "${LISTED[$line]:-}" ] || die "scripts.order: $line ghi 2 lần"
  LISTED[$line]=1
  ORDER+=("$line")
done < "$ORDER_FILE"

# SP có trong DB/sp nhưng quên ghi vào scripts.order: vẫn chạy (sau cùng, thứ tự byte) + cảnh báo. Seed quên ghi: KHÔNG chạy.
EXTRA=()
while IFS= read -r name; do
  [ -n "$name" ] || continue
  [ -n "${LISTED[DB/sp/$name]:-}" ] || EXTRA+=("DB/sp/$name")
done < <(cd "$REPO/DB/sp" && LC_ALL=C ls -1 -- *.sql 2>/dev/null | LC_ALL=C sort)
for f in "$REPO"/DB/seed/*.sql; do
  [ -e "$f" ] || continue
  [ -n "${LISTED[DB/seed/${f##*/}]:-}" ] || echo "  ! CẢNH BÁO: DB/seed/${f##*/} không có trong scripts.order – KHÔNG chạy"
done
for rel in "${EXTRA[@]}"; do echo "  ! CẢNH BÁO: $rel không có trong scripts.order – vẫn chạy ở cuối (hãy thêm vào scripts.order)"; done

if [ "$DRY_RUN" = "1" ]; then
  echo "== DRY RUN – thứ tự sẽ chạy (${#ORDER[@]} trong scripts.order + ${#EXTRA[@]} SP ngoài danh sách):"
  for rel in "${ORDER[@]}" "${EXTRA[@]}"; do echo "$rel"; done
  exit 0
fi

# ---- Kết nối ----
SQLCMD_BIN="${SQLCMD_BIN:-/opt/mssql-tools18/bin/sqlcmd}"
if [ "${DB_TRUSTED_AUTH:-0}" = "1" ]; then
  AUTH=(-E)
else
  [ -n "${SA_PASSWORD:-}" ] || die "Chưa đặt SA_PASSWORD"
  export SQLCMDPASSWORD="$SA_PASSWORD"   # sqlcmd đọc mật khẩu từ biến này – không lộ trên dòng lệnh (ps)
  AUTH=(-U "${DB_USER:-sa}")
fi
SQL=("$SQLCMD_BIN" -S "${DB_SERVER:-db,1433}" "${AUTH[@]}" -C -b -I -f 65001)
scalar() { "${SQL[@]}" -d "$1" -r1 -h -1 -W -Q "SET NOCOUNT ON; $2" | tr -d '[:space:]'; }
run_file() { "${SQL[@]}" -d "$DB_NAME" -v DryRun=0 -i "$REPO/$1" || die "Lỗi khi chạy $1"; }  # D-134: script cấu hình hội tụ dùng $(DryRun)

TMP_DIR=$(mktemp -d)
# Git Bash trên Windows: sqlcmd của Windows hiểu "/tmp/…" là tùy chọn "/t…" → đổi sang đường dẫn C:… (Linux / Docker không có cygpath – giữ nguyên).
command -v cygpath >/dev/null 2>&1 && TMP_DIR=$(cygpath -w "$TMP_DIR")
trap 'rm -rf "$TMP_DIR"' EXIT
chmod 700 "$TMP_DIR"

echo "== DELICARE db-init → $DB_NAME"
"${SQL[@]}" -d master -Q "IF DB_ID(N'$(sql_str "$DB_NAME")') IS NULL CREATE DATABASE [${DB_NAME//]/]]}];" || die "Không tạo được database $DB_NAME"
# T48.5 (D-61): mức tương thích DB = 160 (SQL Server 2022): > 160 → đặt về 160; < 160 → chỉ cảnh báo, không tự nâng.
"${SQL[@]}" -d "$DB_NAME" -i "$REPO/deploy/db/compat-level.sql" || die "Lỗi khi kiểm mức tương thích"

# 2026-11-18: cấu trúc bảng = MIGRATION (DB/migrations, deploy/db/migrations.sh – cùng quy tắc Deploy-Database.ps1): DB trống → baseline 124 bảng;
# DB cũ 91 bảng → script nâng cấp của PO; migration mới → chạy 1 lần; đã chạy ghi ở extended property DelicareMigration:<file>.
# shellcheck source=../db/migrations.sh
. "$REPO/deploy/db/migrations.sh"
if [ "${MIGRATIONS_SKIP:-0}" = "1" ]; then
  echo "  ! MIGRATIONS_SKIP=1 – không chạy migration (đã chọn DB_ALLOW_SCHEMA_DRIFT ở migrate.sh)"
else
  mig_apply
fi
DRIFT=$(mig_drift)
if [ "$DRIFT" != "0" ]; then
  echo "  ! Cấu trúc còn thiếu $DRIFT bảng / cột so với baseline:"; head -n 40 "$TMP_DIR/drift.txt" | sed 's/^/      /'
  [ "${DB_ALLOW_SCHEMA_DRIFT:-0}" = "1" ] || die "Cấu trúc DB chưa khớp migration – cần thêm file DB/migrations (backend). Tạm bỏ qua: DB_ALLOW_SCHEMA_DRIFT=1"
fi
echo "  = Cấu trúc: $(mig_table_count) bảng"

SP_COUNT=0
for rel in "${ORDER[@]}" "${EXTRA[@]}"; do
  case "$rel" in
    */00_Admin_seed.sql)
      EXISTS=$(scalar "$DB_NAME" "SELECT COUNT(*) FROM [dbo].[User] WHERE UserName = LOWER(N'$(sql_str "$ADMIN_USERNAME")') AND IsDeleted = 0")
      if [ "$EXISTS" != "0" ]; then
        echo "  = Tài khoản $ADMIN_USERNAME đã có – giữ nguyên"
        continue
      fi
      [ -n "${ADMIN_PASSWORD:-}" ] || die "DB chưa có tài khoản $ADMIN_USERNAME – đặt ADMIN_PASSWORD (staging.env) cho lần chạy đầu"
      # Mật khẩu → hex UTF-8 → file tạm (quyền 600) → pbkdf2-hash.sql tính PBKDF2 ngay trong SQL Server (cùng định dạng T48.0).
      # Không in, không đưa mật khẩu lên dòng lệnh.
      ( umask 077
        printf ':setvar PasswordHex %s\n:r "%s"\n' "$(printf '%s' "$ADMIN_PASSWORD" | od -An -v -tx1 | tr -d ' \n')" \
          "$REPO/deploy/db/pbkdf2-hash.sql" > "$TMP_DIR/hash.sql" )
      HASH=$("${SQL[@]}" -d master -h -1 -W -i "$TMP_DIR/hash.sql" | tr -d '\r' | grep -m1 '^PBKDF2\$' || true)
      rm -f "$TMP_DIR/hash.sql"
      [ -n "$HASH" ] || die "Không tính được hash mật khẩu admin"
      # Không dùng "-v AdminPasswordHash=...": sqlcmd tách sai giá trị có "=" (đuôi base64). Dùng :setvar + :r trong file tạm.
      case "$ADMIN_USERNAME" in *'"'*) die "ADMIN_USERNAME không hợp lệ" ;; esac
      printf ':setvar AdminUserName "%s"\n:setvar AdminPasswordHash "%s"\n:r "%s"\n' "$ADMIN_USERNAME" "$HASH" "$REPO/$rel" > "$TMP_DIR/admin.sql"
      "${SQL[@]}" -d "$DB_NAME" -i "$TMP_DIR/admin.sql" || die "Lỗi khi chạy $rel"
      echo "  + Admin: $rel"
      ;;
    DB/sp/*)
      run_file "$rel"
      SP_COUNT=$((SP_COUNT + 1))
      ;;
    *)
      run_file "$rel"
      echo "  + Seed $rel"
      ;;
  esac
done
echo "  + Stored procedure: $SP_COUNT file"

# DEPLOY-SYNC-1 (D-134): chuyển đổi dữ liệu 1 lần (deploy/db/once.order; ADOPT_ONCE=1 = chỉ ghi nhận) → tự kiểm menu / quyền (CHECK lệch → dừng; SKIP_CHECK=1 bỏ qua).
run_once_scripts
if [ "${SKIP_CHECK:-0}" != "1" ]; then
  "${SQL[@]}" -d "$DB_NAME" -W -s "|" -i "$REPO/DB/scripts/CHECK_menu_permission.sql" || die "CHECK menu / quyền lệch (D-134) – xem danh sách phía trên"
  echo "  = CHECK menu / quyền: 0 lệch"
fi

# T48.4 (D-60): tài khoản SQL riêng cho api / job (đọc + ghi + EXECUTE, không DDL) – chỉ khi đặt APP_DB_USER + APP_DB_PASSWORD.
# Mật khẩu → hex ASCII trong file tạm (quyền 600), không đưa lên dòng lệnh. APP_DB_RESET_PASSWORD=1 để đặt lại mật khẩu login đã có.
if [ -n "${APP_DB_USER:-}" ]; then
  [ -n "${APP_DB_PASSWORD:-}" ] || die "Đặt APP_DB_PASSWORD cùng APP_DB_USER"
  case "$APP_DB_USER" in *[!A-Za-z0-9_]*|sa) die "APP_DB_USER chỉ gồm chữ, số, _ và không phải sa" ;; esac
  case "$APP_DB_PASSWORD" in *[![:print:]]*) die "APP_DB_PASSWORD chỉ dùng ký tự ASCII in được" ;; esac
  ( umask 077
    printf ':setvar AppLogin "%s"\n:setvar AppPasswordHex %s\n:setvar ResetPassword %s\n:r "%s"\n' \
      "$APP_DB_USER" "$(printf '%s' "$APP_DB_PASSWORD" | od -An -v -tx1 | tr -d ' \n')" "${APP_DB_RESET_PASSWORD:-0}" \
      "$REPO/deploy/db/app-login.sql" > "$TMP_DIR/app-login.sql" )
  "${SQL[@]}" -d "$DB_NAME" -i "$TMP_DIR/app-login.sql" || die "Lỗi khi tạo tài khoản ứng dụng $APP_DB_USER"
  rm -f "$TMP_DIR/app-login.sql"
  echo "  + Tài khoản ứng dụng: $APP_DB_USER (đọc / ghi / EXECUTE)"
fi
echo "== Xong."
