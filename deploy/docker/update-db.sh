#!/bin/bash
# DELICARE – T48.3: dev FE tự cập nhật DB Docker sau khi git pull.
#   git pull --ff-only → nếu có thay đổi đụng DB/ , deploy/db/ hoặc script db-init kể từ lần cập nhật DB thành công gần nhất
#   → docker compose ... run --rm db-init (idempotent: SP CREATE OR ALTER, seed chỉ thêm, không đụng bảng / dữ liệu nghiệp vụ).
# Chạy tay:        bash deploy/docker/update-db.sh            (thêm --force để chạy db-init dù không có thay đổi)
# Cron 5 phút:     */5 * * * * /đường/dẫn/repo/deploy/docker/update-db.sh >> /đường/dẫn/update-db.log 2>&1
# Biến tùy chọn:   ENV_FILE (mặc định deploy/docker/staging.env), STATE_FILE (commit đã áp dụng, mặc định ~/.delicare-update-db.last),
#                  DOCKER (mặc định docker), NO_PULL=1 (không git pull – chỉ so commit hiện tại với lần trước).
# Không chứa bí mật: mật khẩu nằm trong staging.env (gitignored) trên máy dev FE.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$SCRIPT_DIR/../.." && pwd)"
COMPOSE_FILE="deploy/docker/docker-compose.staging.yml"
ENV_FILE="${ENV_FILE:-deploy/docker/staging.env}"
STATE_FILE="${STATE_FILE:-$HOME/.delicare-update-db.last}"
DOCKER="${DOCKER:-docker}"
WATCH=(DB/ deploy/db/ deploy/docker/db-init.sh deploy/docker/docker-compose.staging.yml)
FORCE=0
for a in "$@"; do
  case "$a" in
    --force) FORCE=1 ;;
    -h|--help) sed -n '2,9p' "$0"; exit 0 ;;
    *) echo "Tham số không hợp lệ: $a" >&2; exit 2 ;;
  esac
done

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }
cd "$REPO"

# Không cho 2 lần chạy chồng nhau (cron 5 phút + chạy tay).
exec 9>"${TMPDIR:-/tmp}/delicare-update-db.lock"
if command -v flock >/dev/null 2>&1 && ! flock -n 9; then log "Đang có lần cập nhật khác – bỏ qua."; exit 0; fi

[ -f "$ENV_FILE" ] || { log "LỖI: không thấy $ENV_FILE (chép từ deploy/docker/staging.env.example rồi điền)."; exit 1; }

if [ "${NO_PULL:-0}" != "1" ]; then
  if ! out=$(git pull --ff-only 2>&1); then
    log "LỖI: git pull thất bại – không cập nhật DB."; printf '%s\n' "$out"; exit 1
  fi
fi
HEAD_SHA="$(git rev-parse HEAD)"
LAST_SHA="$(cat "$STATE_FILE" 2>/dev/null || true)"

if [ "$FORCE" != "1" ]; then
  if [ -n "$LAST_SHA" ] && git cat-file -e "$LAST_SHA^{commit}" 2>/dev/null; then
    if [ "$LAST_SHA" = "$HEAD_SHA" ]; then log "Không có commit mới (${HEAD_SHA:0:7}) – bỏ qua."; exit 0; fi
    CHANGED="$(git diff --name-only "$LAST_SHA" "$HEAD_SHA" -- "${WATCH[@]}")"
    if [ -z "$CHANGED" ]; then
      log "Commit mới ${LAST_SHA:0:7}..${HEAD_SHA:0:7} không đụng DB – bỏ qua."
      echo "$HEAD_SHA" > "$STATE_FILE"
      exit 0
    fi
    log "Có thay đổi DB ${LAST_SHA:0:7}..${HEAD_SHA:0:7}:"; printf '    %s\n' $CHANGED
  else
    log "Chưa có lần cập nhật DB nào được ghi nhận (hoặc commit cũ không còn) – chạy db-init."
  fi
fi

log "docker compose run --rm db-init (commit ${HEAD_SHA:0:7})"
if "$DOCKER" compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" run --rm db-init; then
  echo "$HEAD_SHA" > "$STATE_FILE"
  log "Xong – DB đã cập nhật tới ${HEAD_SHA:0:7}."
else
  rc=$?
  log "LỖI: db-init thất bại (exit $rc) – lần chạy sau sẽ thử lại."
  exit "$rc"
fi
