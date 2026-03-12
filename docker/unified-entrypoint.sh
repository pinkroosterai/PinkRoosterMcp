#!/bin/bash
set -e

# -------------------------------------------------------------------------
# PinkRooster unified entrypoint
# Maps user-friendly env vars to the config each service expects.
# -------------------------------------------------------------------------

# --- Validate required env vars ---
if [ -z "$DATABASE_URL" ]; then
  echo "ERROR: DATABASE_URL is required."
  echo "  Example: Host=mydb;Database=pinkrooster;Username=user;Password=secret"
  exit 1
fi

if [ -z "$API_KEY" ]; then
  echo "ERROR: API_KEY is required."
  exit 1
fi

# --- API environment ---
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
export AUTO_MIGRATE="${AUTO_MIGRATE:-true}"
export ENABLE_SWAGGER="${ENABLE_SWAGGER:-true}"
export ConnectionStrings__DefaultConnection="$DATABASE_URL"
export Auth__ApiKeys__0="$API_KEY"

# --- MCP environment ---
export ApiServer__BaseUrl="http://localhost:8080"
export ApiServer__ApiKey="$API_KEY"
# MCP_API_KEY is optional — empty means open access
if [ -n "$MCP_API_KEY" ]; then
  export Auth__ApiKeys__0="$API_KEY"
  # MCP reads its own auth keys; we need a separate env scope.
  # Supervisor passes environment per-program, so we write an env file.
  echo "MCP_AUTH_KEY=$MCP_API_KEY" > /tmp/mcp-env
else
  echo "MCP_AUTH_KEY=" > /tmp/mcp-env
fi

# --- Nginx: inject API_KEY into config ---
# nginx-unified.conf uses $api_key variable. We set it via nginx map.
cat > /etc/nginx/conf.d/api-key.conf <<NGINX_EOF
map \$uri \$api_key {
    default "$API_KEY";
}
NGINX_EOF

# --- Update supervisor MCP environment with correct auth key ---
# The API and MCP share the same process env, but MCP needs its own Auth key.
# We override via supervisor environment directive.
MCP_ENV="ASPNETCORE_URLS=\"http://+:8081\",ASPNETCORE_ENVIRONMENT=\"${ASPNETCORE_ENVIRONMENT}\",ApiServer__BaseUrl=\"http://localhost:8080\",ApiServer__ApiKey=\"${API_KEY}\""
if [ -n "$MCP_API_KEY" ]; then
  MCP_ENV="${MCP_ENV},Auth__ApiKeys__0=\"${MCP_API_KEY}\""
else
  MCP_ENV="${MCP_ENV},Auth__ApiKeys__0=\"\""
fi
sed -i "s|^environment=ASPNETCORE_URLS=\"http://+:8081\"|environment=${MCP_ENV}|" /etc/supervisor/conf.d/pinkrooster.conf

# --- Dashboard auth env (passed to Node.js auth-server via process env) ---
export DASHBOARD_USER="${DASHBOARD_USER:-}"
export DASHBOARD_PASSWORD="${DASHBOARD_PASSWORD:-}"

echo "=== PinkRooster Unified Container ==="
echo "  API:       http://localhost:8080 (internal)"
echo "  MCP:       http://localhost:8081 (exposed)"
echo "  Dashboard: http://localhost:80   (exposed)"
echo "  Database:  ${DATABASE_URL%%Password=*}..."
echo "======================================="

exec /usr/bin/supervisord -c /etc/supervisor/conf.d/pinkrooster.conf
