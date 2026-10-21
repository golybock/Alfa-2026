FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends poppler-utils && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/alfanews/research && chown "$APP_UID:$APP_UID" /var/lib/alfanews/research
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["backend/AlfaNews.Worker/AlfaNews.Worker.csproj", "backend/AlfaNews.Worker/"]
COPY ["backend/AlfaNews.Core/AlfaNews.Core.csproj", "backend/AlfaNews.Core/"]
RUN dotnet restore "backend/AlfaNews.Worker/AlfaNews.Worker.csproj"
COPY backend/ backend/
COPY .dockerignore .dockerignore
WORKDIR "/src/backend/AlfaNews.Worker"
RUN dotnet build "./AlfaNews.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AlfaNews.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AlfaNews.Worker.dll"]
