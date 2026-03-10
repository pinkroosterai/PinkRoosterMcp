FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/PinkRooster.Shared/PinkRooster.Shared.csproj src/PinkRooster.Shared/
COPY src/PinkRooster.Data/PinkRooster.Data.csproj src/PinkRooster.Data/
COPY src/PinkRooster.Api/PinkRooster.Api.csproj src/PinkRooster.Api/
RUN dotnet restore src/PinkRooster.Api/PinkRooster.Api.csproj

COPY src/PinkRooster.Shared/ src/PinkRooster.Shared/
COPY src/PinkRooster.Data/ src/PinkRooster.Data/
COPY src/PinkRooster.Api/ src/PinkRooster.Api/
RUN dotnet publish src/PinkRooster.Api/PinkRooster.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=5 --start-period=15s \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "PinkRooster.Api.dll"]
