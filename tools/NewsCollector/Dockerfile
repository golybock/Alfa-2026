FROM mcr.microsoft.com/dotnet/runtime:10.0.13 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY Directory.Build.props ./
COPY ["tools/NewsCollector/NewsCollector.csproj", "tools/NewsCollector/"]
COPY ["tools/NewsCollector/packages.lock.json", "tools/NewsCollector/"]
RUN dotnet restore "tools/NewsCollector/NewsCollector.csproj"
COPY . .
WORKDIR "/src/tools/NewsCollector"
RUN dotnet build "./NewsCollector.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NewsCollector.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NewsCollector.dll"]
