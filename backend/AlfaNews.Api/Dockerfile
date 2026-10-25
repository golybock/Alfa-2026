FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN sed -i -e 's|http://|https://|g' -e 's/^Components:.*/Components: main/' /etc/apt/sources.list.d/ubuntu.sources \
    && apt-get -o Acquire::Retries=3 update && apt-get -o Acquire::Retries=3 install -y --no-install-recommends curl poppler-utils && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/alfanews/research && chown "$APP_UID:$APP_UID" /var/lib/alfanews/research
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY Directory.Build.props ./
COPY ["backend/AlfaNews.Api/AlfaNews.Api.csproj", "backend/AlfaNews.Api/"]
COPY ["backend/AlfaNews.Api/packages.lock.json", "backend/AlfaNews.Api/"]
COPY ["backend/AlfaNews.Core/AlfaNews.Core.csproj", "backend/AlfaNews.Core/"]
COPY ["backend/AlfaNews.Core/packages.lock.json", "backend/AlfaNews.Core/"]
COPY ["backend/AlfaNews.Contracts/AlfaNews.Contracts.csproj", "backend/AlfaNews.Contracts/"]
COPY ["backend/AlfaNews.Contracts/packages.lock.json", "backend/AlfaNews.Contracts/"]
RUN dotnet restore "backend/AlfaNews.Api/AlfaNews.Api.csproj" --locked-mode
COPY backend/ backend/
COPY contracts/schemas/ contracts/schemas/
COPY .dockerignore .dockerignore
WORKDIR "/src/backend/AlfaNews.Api"
RUN dotnet build "./AlfaNews.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AlfaNews.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AlfaNews.Api.dll"]