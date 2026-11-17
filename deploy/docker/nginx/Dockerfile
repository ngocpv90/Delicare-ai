# DELICARE nginx (T48.4, D-60): build Angular (app-delicare-client) rồi phục vụ file tĩnh + HTTPS + chuyển /api, /Import → api:8080.
# Build context = gốc repo:  docker build -f deploy/docker/nginx/Dockerfile -t delicare-nginx .
FROM node:20-bookworm-slim AS build
WORKDIR /src
COPY app-delicare-client/package.json app-delicare-client/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY app-delicare-client/ ./
RUN npx ng build --configuration production

FROM nginx:1.27-alpine
ENV TZ=Asia/Ho_Chi_Minh
# Angular 18 builder "application": dist/w3crm/browser (bản cũ "browser"-less: dist/w3crm) – lấy thư mục có index.html.
COPY --from=build /src/dist/w3crm /tmp/dist
RUN if [ -f /tmp/dist/browser/index.html ]; then cp -r /tmp/dist/browser/. /usr/share/nginx/html/; else cp -r /tmp/dist/. /usr/share/nginx/html/; fi && rm -rf /tmp/dist
# Template: nginx thay ${SERVER_NAME}, ${MAX_UPLOAD_MB} lúc khởi động (envsubst của image chính thức).
COPY deploy/docker/nginx/default.conf.template /etc/nginx/templates/default.conf.template
EXPOSE 80 443
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD wget -qO- http://127.0.0.1/nginx-health || exit 1
