# DELICARE ToolGateway – image Linux (AI-1, D-133). Build context = thư mục AppDelicare/ :
#   docker build -f AppDelicare.ToolGateway/Dockerfile -t delicare-toolgateway:staging AppDelicare
# MCP /mcp + REST /tools/{name} cho agent Hermes. CHỈ mạng nội bộ Docker (expose, không publish cổng). Bí mật (chuỗi kết nối, khóa agent) qua biến môi trường.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AppDelicare.Model/AppDelicare.Model.csproj AppDelicare.Model/
COPY AppDelicare.Data/AppDelicare.Data.csproj AppDelicare.Data/
COPY AppDelicare.Services/AppDelicare.Services.csproj AppDelicare.Services/
COPY AppDelicare.ToolGateway/AppDelicare.ToolGateway.csproj AppDelicare.ToolGateway/
RUN dotnet restore AppDelicare.ToolGateway/AppDelicare.ToolGateway.csproj
COPY AppDelicare.Model/ AppDelicare.Model/
COPY AppDelicare.Data/ AppDelicare.Data/
COPY AppDelicare.Services/ AppDelicare.Services/
COPY AppDelicare.ToolGateway/ AppDelicare.ToolGateway/
COPY AppDelicare.WebApi/appsettings.json AppDelicare.WebApi/appsettings.Staging.json AppDelicare.WebApi/appsettings.Production.json AppDelicare.WebApi/
COPY AppDelicare.WebApi/Library/ AppDelicare.WebApi/Library/
RUN dotnet publish AppDelicare.ToolGateway/AppDelicare.ToolGateway.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
ENV TZ=Asia/Ho_Chi_Minh \
    ASPNETCORE_ENVIRONMENT=Staging \
    DOTNET_RUNNING_IN_CONTAINER=true \
    Logging__ConsoleOnly=true \
    ASPNETCORE_HTTP_PORTS=8080
RUN apt-get update && apt-get install -y --no-install-recommends tzdata libfontconfig1 curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /app/Logs /app/App_Data/DocumentFiles /app/App_Data/Knowledge && chown -R app:app /app/Logs /app/App_Data
USER app
EXPOSE 8080
HEALTHCHECK --interval=60s --timeout=10s --start-period=40s --retries=3 CMD ["curl", "-fsS", "http://localhost:8080/health"]
ENTRYPOINT ["dotnet", "AppDelicare.ToolGateway.dll"]
