# DELICARE Job – image Linux (T23, D-48). Build context = thư mục AppDelicare/ :
#   docker build -f AppDelicare.Job/Dockerfile -t delicare-job:staging AppDelicare
# Chạy ĐÚNG 1 bản (khóa sp_getapplock chống trùng nếu lỡ chạy 2). Bí mật (chuỗi kết nối) truyền bằng biến môi trường lúc chạy.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AppDelicare.Model/AppDelicare.Model.csproj AppDelicare.Model/
COPY AppDelicare.Data/AppDelicare.Data.csproj AppDelicare.Data/
COPY AppDelicare.Services/AppDelicare.Services.csproj AppDelicare.Services/
COPY AppDelicare.Job/AppDelicare.Job.csproj AppDelicare.Job/
RUN dotnet restore AppDelicare.Job/AppDelicare.Job.csproj
COPY AppDelicare.Model/ AppDelicare.Model/
COPY AppDelicare.Data/ AppDelicare.Data/
COPY AppDelicare.Services/ AppDelicare.Services/
COPY AppDelicare.Job/ AppDelicare.Job/
# appsettings.shared*.json = appsettings*.json của WebApi (cấu hình nghiệp vụ dùng chung) + GemBox.dll mà Services tham chiếu.
COPY AppDelicare.WebApi/appsettings.json AppDelicare.WebApi/appsettings.Staging.json AppDelicare.WebApi/appsettings.Production.json AppDelicare.WebApi/
COPY AppDelicare.WebApi/Library/ AppDelicare.WebApi/Library/
RUN dotnet publish AppDelicare.Job/AppDelicare.Job.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# Giờ Việt Nam (D-18): lịch job tính theo BusinessRules:TimeZone, TZ để log cùng giờ.
ENV TZ=Asia/Ho_Chi_Minh \
    DOTNET_ENVIRONMENT=Staging \
    DOTNET_RUNNING_IN_CONTAINER=true
RUN apt-get update && apt-get install -y --no-install-recommends tzdata libfontconfig1 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /app/Logs /app/App_Data/DocumentFiles && chown -R app:app /app/Logs /app/App_Data
USER app
ENTRYPOINT ["dotnet", "AppDelicare.Job.dll"]
