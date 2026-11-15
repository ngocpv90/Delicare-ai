#!/bin/bash
# Khởi tạo DB staging trong container (T48.1) – cùng thứ tự với deploy/db/Deploy-Database.ps1, idempotent.
set -euo pipefail
SQL="/opt/mssql-tools18/bin/sqlcmd -S db,1433 -U sa -P $SA_PASSWORD -C -b -I -f 65001"
echo "== DELICARE db-init → $DB_NAME"
$SQL -d master -Q "IF DB_ID(N'$DB_NAME') IS NULL CREATE DATABASE [$DB_NAME];"
TABLES=$($SQL -d "$DB_NAME" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0" | tr -d '[:space:]')
if [ "$TABLES" = "0" ]; then
  sed -E 's/^USE \[[^]]+\][[:space:]]*$//' /repo/docs/reference/20261113_structure_db_appdelicare.sql > /tmp/ddl.sql
  $SQL -d "$DB_NAME" -i /tmp/ddl.sql
  echo "  + Đã tạo schema 124 bảng"
else
  echo "  = Schema đã có ($TABLES bảng) – không đụng tới bảng"
fi
for f in /repo/DB/sp/*.sql; do $SQL -d "$DB_NAME" -i "$f"; done
echo "  + Stored procedure"
HASH=$(printf '%s' "$ADMIN_PASSWORD" | md5sum | cut -d' ' -f1 | tr 'a-f' 'A-F' | sed -E 's/(..)/\1-/g; s/-$//')
$SQL -d "$DB_NAME" -v AdminUserName="admin" AdminPasswordHash="$HASH" -i /repo/DB/seed/00_Admin_seed.sql
for f in OrganizationUnit_seed.sql ObjectMenu_seed.sql UserRole_seed.sql Shift_seed.sql; do $SQL -d "$DB_NAME" -i "/repo/DB/seed/$f"; echo "  + Seed $f"; done
echo "== Xong."
