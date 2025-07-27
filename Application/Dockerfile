# Unter https://aka.ms/customizecontainer erfahren Sie, wie Sie Ihren Debugcontainer anpassen und wie Visual Studio dieses Dockerfile verwendet, um Ihre Images für ein schnelleres Debuggen zu erstellen.

# Diese Stufe wird verwendet, wenn sie von VS im Schnellmodus ausgeführt wird (Standardeinstellung für Debugkonfiguration).
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base_build
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Diese Stufe wird zum Erstellen des Dienstprojekts verwendet.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS with-node
RUN apt-get update
RUN apt-get install curl
RUN curl -sL https://deb.nodesource.com/setup_22.x | bash
RUN apt-get -y install nodejs

FROM with-node AS build_backend
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet build "./Application/Frigorino.sln" -c $BUILD_CONFIGURATION -o /app/build

# Diese Stufe wird verwendet, um das Dienstprojekt zu veröffentlichen, das in die letzte Phase kopiert werden soll.
FROM build_backend AS publish_backend
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Application/Frigorino.sln" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM with-node as build_frontend
WORKDIR /src
COPY ./Application/Frigorino.Web/ClientApp ./Frigorino.Web/ClientApp
WORKDIR Frigorino.Web/ClientApp
RUN npm ci
RUN npm run build

# Diese Stufe wird in der Produktion oder bei Ausführung von VS im regulären Modus verwendet (Standard, wenn die Debugkonfiguration nicht verwendet wird).
FROM base_build AS final
WORKDIR /app
COPY --from=publish_backend /app/publish .
COPY --from=build_frontend ./src/Frigorino.Web/ClientApp/build ./wwwroot
COPY --from=build_frontend ./src/Frigorino.Web/ClientApp/build ./ClientApp/build
ENTRYPOINT ["dotnet", "Frigorino.Web.dll"]
