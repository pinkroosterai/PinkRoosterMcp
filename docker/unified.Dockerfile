# =============================================================================
# PinkRooster — Single unified image (API + MCP + Dashboard)
# Requires an external PostgreSQL database.
#
# Ports exposed:
#   80   — Dashboard (nginx) + API proxy
#   8081 — MCP server (for Claude Code / MCP clients)
#
# Required env vars:
#   DATABASE_URL — PostgreSQL connection string
#   API_KEY      — Shared API authentication key
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Build .NET projects (API + MCP)
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/PinkRooster.Shared/PinkRooster.Shared.csproj src/PinkRooster.Shared/
COPY src/PinkRooster.Data/PinkRooster.Data.csproj src/PinkRooster.Data/
COPY src/PinkRooster.Api/PinkRooster.Api.csproj src/PinkRooster.Api/
COPY src/PinkRooster.Mcp/PinkRooster.Mcp.csproj src/PinkRooster.Mcp/

RUN dotnet restore src/PinkRooster.Api/PinkRooster.Api.csproj \
 && dotnet restore src/PinkRooster.Mcp/PinkRooster.Mcp.csproj

COPY src/PinkRooster.Shared/ src/PinkRooster.Shared/
COPY src/PinkRooster.Data/ src/PinkRooster.Data/
COPY src/PinkRooster.Api/ src/PinkRooster.Api/
COPY src/PinkRooster.Mcp/ src/PinkRooster.Mcp/

RUN dotnet publish src/PinkRooster.Api/PinkRooster.Api.csproj -c Release -o /app/api \
 && dotnet publish src/PinkRooster.Mcp/PinkRooster.Mcp.csproj -c Release -o /app/mcp

# ---------------------------------------------------------------------------
# Stage 2: Build dashboard (Vite/React)
# ---------------------------------------------------------------------------
FROM node:22-alpine AS dashboard-build
WORKDIR /app

COPY src/dashboard/package.json src/dashboard/package-lock.json* ./
RUN npm ci

COPY src/dashboard/ .
RUN npm run build

# ---------------------------------------------------------------------------
# Stage 3: Unified runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Install nginx, Node.js (for auth server), supervisor, and curl (healthchecks)
RUN apt-get update && apt-get install -y --no-install-recommends \
    nginx \
    nodejs \
    supervisor \
    curl \
 && rm -rf /var/lib/apt/lists/*

# --- .NET apps ---
COPY --from=dotnet-build /app/api /app/api
COPY --from=dotnet-build /app/mcp /app/mcp

# --- Dashboard ---
COPY --from=dashboard-build /app/dist /usr/share/nginx/html

# --- Config files ---
COPY docker/nginx-unified.conf /etc/nginx/sites-available/default
COPY docker/auth-server.mjs /opt/auth-server.mjs
COPY docker/supervisord.conf /etc/supervisor/conf.d/pinkrooster.conf
COPY docker/unified-entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Remove default nginx config that conflicts
RUN rm -f /etc/nginx/sites-enabled/default \
 && ln -s /etc/nginx/sites-available/default /etc/nginx/sites-enabled/default

EXPOSE 80 8081

HEALTHCHECK --interval=10s --timeout=3s --retries=5 --start-period=20s \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["/entrypoint.sh"]
