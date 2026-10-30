# =============================================================
# ExamNexus Multi-Tenant SaaS - Production Dockerfile (.NET 9.0)
# Optimized for Render, Azure App Service, and Railway
# =============================================================

# 1. Runtime Base
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# 2. Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first to leverage Docker layer caching on restores
COPY ["src/ExamNexus.Domain/ExamNexus.Domain.csproj", "src/ExamNexus.Domain/"]
COPY ["src/ExamNexus.Application/ExamNexus.Application.csproj", "src/ExamNexus.Application/"]
COPY ["src/ExamNexus.Infrastructure/ExamNexus.Infrastructure.csproj", "src/ExamNexus.Infrastructure/"]
COPY ["src/ExamNexus.Web/ExamNexus.Web.csproj", "src/ExamNexus.Web/"]

RUN dotnet restore "src/ExamNexus.Web/ExamNexus.Web.csproj"

# Copy the rest of the source code
COPY . .

# Publish release binaries
WORKDIR "/src/src/ExamNexus.Web"
RUN dotnet publish "ExamNexus.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 3. Final Production Container
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Directory for SQLite storage or persistent cloud disk mounts
RUN mkdir -p /app/data

ENTRYPOINT ["dotnet", "ExamNexus.Web.dll"]
