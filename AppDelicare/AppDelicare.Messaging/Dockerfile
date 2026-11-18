# DELICARE messaging – image Linux (BOT-1.3, D-62). Build context = thư mục AppDelicare/ :
#   docker build -f AppDelicare.Messaging/Dockerfile -t delicare-messaging AppDelicare
# KHÔNG chạm DB (không chuỗi kết nối) – chỉ gọi api qua HTTP. Chạy 1 bản. Bí mật (Messaging__ClientKey, Telegram__Bots__<BOT>__Token / __WebhookSecret)
# truyền bằng biến môi trường lúc chạy.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AppDelicare.Messaging/AppDelicare.Messaging.csproj AppDelicare.Messaging/
RUN dotnet restore AppDelicare.Messaging/AppDelicare.Messaging.csproj
COPY AppDelicare.Messaging/ AppDelicare.Messaging/
RUN dotnet publish AppDelicare.Messaging/AppDelicare.Messaging.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
ENV TZ=Asia/Ho_Chi_Minh \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    Messaging__Mode=Webhook
RUN apt-get update && apt-get install -y --no-install-recommends tzdata && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER app
EXPOSE 8080
# Khỏe = GET /health của chính tiến trình (dotnet AppDelicare.Messaging.dll --healthcheck).
HEALTHCHECK --interval=60s --timeout=10s --start-period=30s --retries=3 CMD ["dotnet", "/app/AppDelicare.Messaging.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "AppDelicare.Messaging.dll"]
