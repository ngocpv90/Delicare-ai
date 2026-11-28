#!/usr/bin/env bash
# AI-24 (D-142 b): sao lưu hằng đêm volume tệp đính kèm (delicare-docs) + kho tri thức (knowledge) – chạy cùng giờ backup DB.
#   bash deploy/docker/backup-volumes.sh <tên project compose: delicare-prod | delicare-staging> [thư mục đích] [số ngày giữ]
#   cron (02:30 hằng đêm, giữ 14 ngày):  30 2 * * *  cd /opt/delicare && bash deploy/docker/backup-volumes.sh delicare-prod /backup/delicare 14 >> /var/log/delicare-backup.log 2>&1
# Khôi phục 1 volume (dừng api / job / toolgateway trước):
#   docker run --rm -v <project>_knowledge:/data -v <thư mục đích>:/backup alpine sh -c "cd /data && tar xzf /backup/<tệp>.tar.gz"
# Không chứa bí mật; không đụng DB (DB backup bằng SQL Server – deploy/README.md mục 10).
set -euo pipefail
PROJECT="${1:?Thiếu tên project compose (vd delicare-prod)}"
DEST="${2:-/backup/delicare}"
KEEP="${3:-14}"
STAMP="$(date +%Y%m%d-%H%M)"
mkdir -p "$DEST"
for VOL in delicare-docs knowledge; do
  NAME="${PROJECT}_${VOL}"
  if ! docker volume inspect "$NAME" >/dev/null 2>&1; then
    echo "[backup] bỏ qua $NAME (chưa có volume)"
    continue
  fi
  OUT="${VOL}-${STAMP}.tar.gz"
  docker run --rm -v "${NAME}:/data:ro" -v "${DEST}:/backup" alpine sh -c "cd /data && tar czf /backup/${OUT} ."
  echo "[backup] ${NAME} → ${DEST}/${OUT} ($(du -h "${DEST}/${OUT}" | cut -f1))"
done
find "$DEST" -maxdepth 1 -name '*.tar.gz' -mtime "+${KEEP}" -print -delete | sed 's/^/[backup] xóa bản cũ: /'
