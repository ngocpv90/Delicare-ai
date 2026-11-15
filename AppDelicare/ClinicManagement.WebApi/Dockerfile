# DELICARE API – image Linux (T48.1). Build context = thư mục AppDelicare/ :
#   docker build -f ClinicManagement.WebApi/Dockerfile -t delicare-api:staging AppDelicare
# Bí mật KHÔNG có trong image: truyền bằng biến môi trường lúc chạy (xem deploy/README.md).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ClinicManagement.Model/AppDelicare.Model.csproj ClinicManagement.Model/
COPY ClinicManagement.Data/AppDelicare.Data.csproj ClinicManagement.Data/
COPY ClinicManagement.Services/AppDelicare.Services.csproj ClinicManagement.Services/
COPY ClinicManagement.WebApi/AppDelicare.WebApi.csproj ClinicManagement.WebApi/
RUN dotnet restore ClinicManagement.WebApi/AppDelicare.WebApi.csproj
COPY ClinicManagement.Model/ ClinicManagement.Model/
COPY ClinicManagement.Data/ ClinicManagement.Data/
COPY ClinicManagement.Services/ ClinicManagement.Services/
COPY ClinicManagement.WebApi/ ClinicManagement.WebApi/
RUN dotnet publish ClinicManagement.WebApi/AppDelicare.WebApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# Giờ Việt Nam (D-18) + thư viện cho xử lý tài liệu (GemBox / Syncfusion).
ENV TZ=Asia/Ho_Chi_Minh \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Staging \
    DOTNET_RUNNING_IN_CONTAINER=true
RUN apt-get update && apt-get install -y --no-install-recommends tzdata libfontconfig1 curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /app/wwwroot/Logs /app/wwwroot/Import && chown -R app:app /app/wwwroot
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD curl -fsS http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "AppDelicare.WebApi.dll"]
