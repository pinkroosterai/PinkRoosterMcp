FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/PinkRooster.Shared/PinkRooster.Shared.csproj src/PinkRooster.Shared/
COPY src/PinkRooster.Mcp/PinkRooster.Mcp.csproj src/PinkRooster.Mcp/
RUN dotnet restore src/PinkRooster.Mcp/PinkRooster.Mcp.csproj

COPY src/PinkRooster.Shared/ src/PinkRooster.Shared/
COPY src/PinkRooster.Mcp/ src/PinkRooster.Mcp/
RUN dotnet publish src/PinkRooster.Mcp/PinkRooster.Mcp.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=5 --start-period=15s \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "PinkRooster.Mcp.dll"]
