# DELICARE API – image Linux (T48.1). Build context = thư mục AppDelicare/ :
#   docker build -f AppDelicare.WebApi/Dockerfile -t delicare-api:staging AppDelicare
# Bí mật KHÔNG có trong image: truyền bằng biến môi trường lúc chạy (xem deploy/README.md).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AppDelicare.Model/AppDelicare.Model.csproj AppDelicare.Model/
COPY AppDelicare.Data/AppDelicare.Data.csproj AppDelicare.Data/
COPY AppDelicare.Services/AppDelicare.Services.csproj AppDelicare.Services/
COPY AppDelicare.WebApi/AppDelicare.WebApi.csproj AppDelicare.WebApi/
RUN dotnet restore AppDelicare.WebApi/AppDelicare.WebApi.csproj
COPY AppDelicare.Model/ AppDelicare.Model/
COPY AppDelicare.Data/ AppDelicare.Data/
COPY AppDelicare.Services/ AppDelicare.Services/
COPY AppDelicare.WebApi/ AppDelicare.WebApi/
RUN dotnet publish AppDelicare.WebApi/AppDelicare.WebApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# Giờ Việt Nam (D-18) + thư viện cho xử lý tài liệu (GemBox / Syncfusion).
ENV TZ=Asia/Ho_Chi_Minh \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Staging \
    DOTNET_RUNNING_IN_CONTAINER=true
RUN apt-get update && apt-get install -y --no-install-recommends tzdata libfontconfig1 curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /app/Logs /app/wwwroot/Import && chown -R app:app /app/Logs /app/wwwroot
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD curl -fsS http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "AppDelicare.WebApi.dll"]
