# Runtime: distroless chiseled image — minimal CVE surface, runs as non-root ($APP_UID=1654).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
EXPOSE 8080

# Backend build: dotnet SDK only — no Node needed for `dotnet publish`.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS publish_backend
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Restore layer: only invalidated when sln/csproj change
COPY Application/Frigorino.sln ./Application/Frigorino.sln
COPY Application/Frigorino.Domain/Frigorino.Domain.csproj ./Application/Frigorino.Domain/
COPY Application/Frigorino.Application/Frigorino.Application.csproj ./Application/Frigorino.Application/
COPY Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj ./Application/Frigorino.Infrastructure/
COPY Application/Frigorino.Web/Frigorino.Web.csproj ./Application/Frigorino.Web/
COPY Application/Frigorino.Test/Frigorino.Test.csproj ./Application/Frigorino.Test/
RUN dotnet restore ./Application/Frigorino.sln
COPY . .
RUN dotnet publish "./Application/Frigorino.sln" -c $BUILD_CONFIGURATION --no-restore -o /app/publish /p:UseAppHost=false

# Frontend build: slim Node LTS only — no .NET SDK overhead.
FROM node:22-bookworm-slim AS build_frontend
WORKDIR /src/Frigorino.Web/ClientApp
# npm cache layer: only invalidated when package.json/package-lock.json change
COPY Application/Frigorino.Web/ClientApp/package.json Application/Frigorino.Web/ClientApp/package-lock.json ./
RUN npm ci
COPY Application/Frigorino.Web/ClientApp/ ./
RUN npm run build

FROM runtime AS final
WORKDIR /app
COPY --chown=$APP_UID:$APP_UID --from=publish_backend /app/publish .
COPY --chown=$APP_UID:$APP_UID --from=build_frontend ./src/Frigorino.Web/ClientApp/build ./wwwroot
COPY --chown=$APP_UID:$APP_UID --from=build_frontend ./src/Frigorino.Web/ClientApp/build ./ClientApp/build
ENTRYPOINT ["dotnet", "Frigorino.Web.dll"]
