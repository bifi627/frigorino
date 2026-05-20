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
COPY Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj ./Application/Frigorino.Infrastructure/
COPY Application/Frigorino.Web/Frigorino.Web.csproj ./Application/Frigorino.Web/
COPY Application/Frigorino.Test/Frigorino.Test.csproj ./Application/Frigorino.Test/
COPY Application/Frigorino.Features/Frigorino.Features.csproj ./Application/Frigorino.Features/
COPY Application/Frigorino.IntegrationTests/Frigorino.IntegrationTests.csproj ./Application/Frigorino.IntegrationTests/
COPY Application/Directory.Build.props ./Application/
COPY Application/Frigorino.Domain/packages.lock.json ./Application/Frigorino.Domain/
COPY Application/Frigorino.Features/packages.lock.json ./Application/Frigorino.Features/
COPY Application/Frigorino.Infrastructure/packages.lock.json ./Application/Frigorino.Infrastructure/
COPY Application/Frigorino.Web/packages.lock.json ./Application/Frigorino.Web/
COPY Application/Frigorino.Test/packages.lock.json ./Application/Frigorino.Test/
COPY Application/Frigorino.IntegrationTests/packages.lock.json ./Application/Frigorino.IntegrationTests/
RUN dotnet restore ./Application/Frigorino.sln --locked-mode
COPY . .
# Web-only publish, RID-pinned so PublishReadyToRun pre-JITs IL for linux-x64.
# --self-contained false keeps the runtime in the base image, not in /app/publish.
RUN dotnet publish "./Application/Frigorino.Web/Frigorino.Web.csproj" \
    -c $BUILD_CONFIGURATION --no-restore -o /app/publish \
    /p:UseAppHost=false -r linux-x64 --self-contained false

# Frontend build: slim Node LTS only — no .NET SDK overhead.
FROM node:22-bookworm-slim AS build_frontend
WORKDIR /src/Frigorino.Web/ClientApp
# npm cache layer: only invalidated when package.json/package-lock.json change
COPY Application/Frigorino.Web/ClientApp/package.json Application/Frigorino.Web/ClientApp/package-lock.json ./
# --ignore-scripts blocks postinstall hooks on transitives — primary npm supply-chain
# attack surface. None of our direct deps need install scripts to run.
RUN npm ci --ignore-scripts
COPY Application/Frigorino.Web/ClientApp/ ./
# VITE_* values are inlined into the JS bundle at build time by `npm run build`.
# Railway only injects service variables into the build context if they're declared
# as ARG here (per Railway Dockerfile docs); promote to ENV so Vite picks them up
# from process.env. Empty defaults keep the build green when a var is unset — the
# Faro SDK gates off on empty VITE_FARO_URL.
ARG VITE_FARO_URL=
ARG VITE_FARO_APP_NAME=
ARG VITE_FARO_ENV=
ARG VITE_APP_VERSION=
ENV VITE_FARO_URL=$VITE_FARO_URL \
    VITE_FARO_APP_NAME=$VITE_FARO_APP_NAME \
    VITE_FARO_ENV=$VITE_FARO_ENV \
    VITE_APP_VERSION=$VITE_APP_VERSION
RUN npm run build

FROM runtime AS final
WORKDIR /app
COPY --chown=$APP_UID:$APP_UID --from=publish_backend /app/publish .
COPY --chown=$APP_UID:$APP_UID --from=build_frontend ./src/Frigorino.Web/ClientApp/build ./wwwroot
COPY --chown=$APP_UID:$APP_UID --from=build_frontend ./src/Frigorino.Web/ClientApp/build ./ClientApp/build
ENTRYPOINT ["dotnet", "Frigorino.Web.dll"]
