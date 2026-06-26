FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/DownloaderBot/DownloaderBot.csproj src/DownloaderBot/
RUN dotnet restore src/DownloaderBot/DownloaderBot.csproj
COPY . .
RUN dotnet publish src/DownloaderBot/DownloaderBot.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* \
    && useradd -m -u 10001 botuser \
    && mkdir -p /app/data /app/backups /app/logs \
    && chown -R botuser:botuser /app
COPY --from=build /app/publish .
USER botuser
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "DownloaderBot.dll"]
