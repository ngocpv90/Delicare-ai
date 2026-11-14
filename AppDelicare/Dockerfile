# ---------- Build .NET API ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (better layer caching). ClinicManagement.Generator is a
# .NET Framework 4.8 T4 code-gen project that cannot build on Linux and is
# not used at runtime, so it is excluded from the image.
COPY ClinicManagement.Model/AppDelicare.Model.csproj       ClinicManagement.Model/
COPY ClinicManagement.Data/AppDelicare.Data.csproj         ClinicManagement.Data/
COPY ClinicManagement.Services/AppDelicare.Services.csproj ClinicManagement.Services/
COPY ClinicManagement.WebApi/AppDelicare.WebApi.csproj     ClinicManagement.WebApi/
RUN sed -i '/AppDelicare.Generator.csproj/d' ClinicManagement.WebApi/AppDelicare.WebApi.csproj \
 && dotnet restore ClinicManagement.WebApi/AppDelicare.WebApi.csproj

COPY ClinicManagement.Model/    ClinicManagement.Model/
COPY ClinicManagement.Data/     ClinicManagement.Data/
COPY ClinicManagement.Services/ ClinicManagement.Services/
COPY ClinicManagement.WebApi/   ClinicManagement.WebApi/
RUN sed -i '/AppDelicare.Generator.csproj/d' ClinicManagement.WebApi/AppDelicare.WebApi.csproj \
 && dotnet publish ClinicManagement.WebApi/AppDelicare.WebApi.csproj \
      -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Fonts/fontconfig + gdiplus for Word/PDF rendering (Syncfusion, GemBox, EvoPdf)
RUN apt-get update \
 && apt-get install -y --no-install-recommends libfontconfig1 libgdiplus fonts-dejavu-core tzdata \
 && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    TZ=Asia/Ho_Chi_Minh \
    # appsettings.json uses a Windows path for the log file
    Serilog__WriteTo__1__Args__path=wwwroot/Logs/AppDelicare.log

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "AppDelicare.WebApi.dll"]
