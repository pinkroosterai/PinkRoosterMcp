#!/usr/bin/env bash
set -euo pipefail

# ─── PinkRooster Setup Script ────────────────────────────────────────────────
# Idempotent script for configuring, building, deploying, and registering
# PinkRooster. Safe to re-run for config changes or restarts.
# ─────────────────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# ─── Colors ──────────────────────────────────────────────────────────────────
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[0;33m'
CYAN='\033[0;36m'
BOLD='\033[1m'
DIM='\033[2m'
RESET='\033[0m'

ok()   { printf "${GREEN}  %s${RESET}\n" "$1"; }
fail() { printf "${RED}  %s${RESET}\n" "$1"; }
info() { printf "${CYAN}  %s${RESET}\n" "$1"; }
warn() { printf "${YELLOW}  %s${RESET}\n" "$1"; }
header() { printf "\n${BOLD}${CYAN}── %s ──${RESET}\n\n" "$1"; }

# ─── Secret generation ───────────────────────────────────────────────────────
generate_secret() {
  openssl rand -base64 32 2>/dev/null | tr -d '/+=' | head -c 32
}

# ─── Step 1: Environment file ────────────────────────────────────────────────
configure_env() {
  header "Environment Configuration"

  # Load existing values if .env exists
  local existing_pg_password="" existing_api_key="" existing_mcp_key=""
  local existing_dash_user="" existing_dash_password=""

  if [[ -f .env ]]; then
    info "Found existing .env — loading current values as defaults"
    # Source safely: only read known variables
    existing_pg_password=$(grep -E '^POSTGRES_PASSWORD=' .env 2>/dev/null | cut -d= -f2- || true)
    existing_api_key=$(grep -E '^API_KEY=' .env 2>/dev/null | cut -d= -f2- || true)
    existing_mcp_key=$(grep -E '^MCP_API_KEY=' .env 2>/dev/null | cut -d= -f2- || true)
    existing_dash_user=$(grep -E '^DASHBOARD_USER=' .env 2>/dev/null | cut -d= -f2- || true)
    existing_dash_password=$(grep -E '^DASHBOARD_PASSWORD=' .env 2>/dev/null | cut -d= -f2- || true)
  fi

  # Generate defaults for required secrets
  local default_pg_password="${existing_pg_password:-$(generate_secret)}"
  local default_api_key="${existing_api_key:-$(generate_secret)}"

  # Core secrets (auto-generated, shown for transparency)
  printf "  ${DIM}Secrets are auto-generated. Press Enter to keep defaults.${RESET}\n\n"

  read -rp "  POSTGRES_PASSWORD [${default_pg_password:0:8}...]: " pg_password
  POSTGRES_PASSWORD="${pg_password:-$default_pg_password}"

  read -rp "  API_KEY [${default_api_key:0:8}...]: " api_key
  API_KEY="${api_key:-$default_api_key}"

  # Dashboard authentication
  printf "\n"
  local dash_default="n"
  if [[ -n "$existing_dash_user" && -n "$existing_dash_password" ]]; then
    dash_default="y"
  fi

  read -rp "  Protect dashboard with authentication? (y/n) [$dash_default]: " enable_dash_auth
  enable_dash_auth="${enable_dash_auth:-$dash_default}"

  DASHBOARD_USER=""
  DASHBOARD_PASSWORD=""
  if [[ "$enable_dash_auth" =~ ^[Yy] ]]; then
    local default_user="${existing_dash_user:-admin}"
    read -rp "  Dashboard username [$default_user]: " dash_user
    DASHBOARD_USER="${dash_user:-$default_user}"

    if [[ -n "$existing_dash_password" ]]; then
      read -rp "  Dashboard password [keep existing]: " dash_pass
      DASHBOARD_PASSWORD="${dash_pass:-$existing_dash_password}"
    else
      local gen_dash_pass
      gen_dash_pass="$(generate_secret | head -c 16)"
      read -rp "  Dashboard password [$gen_dash_pass]: " dash_pass
      DASHBOARD_PASSWORD="${dash_pass:-$gen_dash_pass}"
    fi
  fi

  # MCP authentication
  printf "\n"
  local mcp_default="n"
  if [[ -n "$existing_mcp_key" ]]; then
    mcp_default="y"
  fi

  read -rp "  Protect MCP server with API key? (y/n) [$mcp_default]: " enable_mcp_auth
  enable_mcp_auth="${enable_mcp_auth:-$mcp_default}"

  MCP_API_KEY=""
  if [[ "$enable_mcp_auth" =~ ^[Yy] ]]; then
    local default_mcp_key="${existing_mcp_key:-$(generate_secret)}"
    read -rp "  MCP API key [${default_mcp_key:0:8}...]: " mcp_key
    MCP_API_KEY="${mcp_key:-$default_mcp_key}"
  fi

  # Write .env
  cat > .env <<EOF
POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
API_KEY=${API_KEY}

# MCP server authentication (optional — leave empty for open access)
MCP_API_KEY=${MCP_API_KEY}

# Dashboard authentication (optional — leave empty for open access)
DASHBOARD_USER=${DASHBOARD_USER}
DASHBOARD_PASSWORD=${DASHBOARD_PASSWORD}
EOF

  ok "Environment file written to .env"
}

# ─── Step 2: Docker build & deploy ───────────────────────────────────────────
deploy_docker() {
  header "Docker Build & Deploy"

  # Check Docker is available
  if ! command -v docker &>/dev/null; then
    fail "Docker is not installed or not in PATH"
    exit 1
  fi

  if ! docker compose version &>/dev/null; then
    fail "Docker Compose is not available (need docker compose v2+)"
    exit 1
  fi

  info "Building and starting services..."
  if docker compose up -d --build 2>&1 | tail -5; then
    ok "Docker stack started"
  else
    fail "Docker compose failed"
    exit 1
  fi

  printf "\n"
  wait_for_health
}

wait_for_health() {
  local timeout=90
  local interval=3
  local elapsed=0

  info "Waiting for services to become healthy (timeout: ${timeout}s)..."
  printf "\n"

  # Track health status
  local pg_ok=false api_ok=false mcp_ok=false dash_ok=false

  while (( elapsed < timeout )); do
    # Postgres
    if [[ "$pg_ok" == false ]]; then
      if docker compose exec -T postgres pg_isready -U pinkrooster -q 2>/dev/null; then
        pg_ok=true
        ok "PostgreSQL is ready"
      fi
    fi

    # API
    if [[ "$api_ok" == false && "$pg_ok" == true ]]; then
      if curl -sf http://localhost:5100/health >/dev/null 2>&1; then
        api_ok=true
        ok "API is ready (http://localhost:5100)"
      fi
    fi

    # MCP
    if [[ "$mcp_ok" == false && "$api_ok" == true ]]; then
      if curl -sf http://localhost:5200/health >/dev/null 2>&1; then
        mcp_ok=true
        ok "MCP server is ready (http://localhost:5200)"
      fi
    fi

    # Dashboard
    if [[ "$dash_ok" == false && "$api_ok" == true ]]; then
      if curl -sf http://localhost:3000 >/dev/null 2>&1; then
        dash_ok=true
        ok "Dashboard is ready (http://localhost:3000)"
      fi
    fi

    # All healthy?
    if [[ "$pg_ok" == true && "$api_ok" == true && "$mcp_ok" == true && "$dash_ok" == true ]]; then
      printf "\n"
      ok "All services are healthy"
      return 0
    fi

    sleep "$interval"
    elapsed=$((elapsed + interval))
    printf "  ${DIM}... waiting (%ds/%ds)${RESET}\r" "$elapsed" "$timeout"
  done

  # Timeout — report what failed
  printf "\n\n"
  warn "Health check timeout after ${timeout}s. Status:"
  if [[ "$pg_ok" == true ]]; then ok "PostgreSQL: healthy"; else fail "PostgreSQL: not ready"; fi
  if [[ "$api_ok" == true ]]; then ok "API: healthy"; else fail "API: not ready"; fi
  if [[ "$mcp_ok" == true ]]; then ok "MCP: healthy"; else fail "MCP: not ready"; fi
  if [[ "$dash_ok" == true ]]; then ok "Dashboard: healthy"; else fail "Dashboard: not ready"; fi
  printf "\n"
  warn "Run 'docker compose logs' to investigate"
  HEALTH_FAILED=true
}

# ─── Step 3: MCP registration & skills ───────────────────────────────────────
register_mcp() {
  header "Claude Code Integration"

  local claude_dir="$HOME/.claude"

  # Check claude CLI is available
  if ! command -v claude &>/dev/null; then
    fail "Claude Code CLI not found — cannot register MCP server"
    warn "Install Claude Code, then re-run ./setup.sh"
    return 1
  fi

  # Register MCP server (user-scoped, available to all projects)
  if [[ -n "$MCP_API_KEY" ]]; then
    claude mcp add --transport http --scope user \
      --header "X-Api-Key: $MCP_API_KEY" \
      pinkrooster http://localhost:5200
  else
    claude mcp add --transport http --scope user \
      pinkrooster http://localhost:5200
  fi
  ok "MCP server registered (user-scoped)"

  # Copy skills
  printf "\n"
  local skills_src="$SCRIPT_DIR/.claude/skills"
  local skills_dst="$claude_dir/skills"

  if [[ -d "$skills_src" ]]; then
    mkdir -p "$skills_dst"
    local copied=0
    for skill_dir in "$skills_src"/pm-*/; do
      if [[ -d "$skill_dir" ]]; then
        cp -r "$skill_dir" "$skills_dst/"
        copied=$((copied + 1))
      fi
    done
    ok "Copied $copied PM skills to $skills_dst"
  else
    warn "Skills directory not found at $skills_src"
  fi
}

# ─── Step 4: Status summary ──────────────────────────────────────────────────
print_summary() {
  header "Setup Complete"

  printf "  ${BOLD}Service URLs${RESET}\n"
  printf "  %-14s %s\n" "API:" "http://localhost:5100"
  printf "  %-14s %s\n" "Swagger:" "http://localhost:5100/swagger/index.html"
  printf "  %-14s %s\n" "MCP Server:" "http://localhost:5200"
  printf "  %-14s %s\n" "Dashboard:" "http://localhost:3000"
  printf "\n"

  printf "  ${BOLD}Authentication${RESET}\n"
  if [[ -n "$DASHBOARD_USER" ]]; then
    printf "  %-14s ${GREEN}enabled${RESET} (user: %s)\n" "Dashboard:" "$DASHBOARD_USER"
  else
    printf "  %-14s ${DIM}open access${RESET}\n" "Dashboard:"
  fi
  if [[ -n "$MCP_API_KEY" ]]; then
    printf "  %-14s ${GREEN}enabled${RESET} (key: %s...)\n" "MCP Server:" "${MCP_API_KEY:0:8}"
  else
    printf "  %-14s ${DIM}open access${RESET}\n" "MCP Server:"
  fi
  printf "\n"

  printf "  ${BOLD}Claude Code${RESET}\n"
  if command -v claude &>/dev/null && claude mcp get pinkrooster &>/dev/null; then
    ok "MCP server registered (user-scoped)"
  else
    fail "MCP server not registered"
  fi

  local skill_count=0
  for d in "$HOME/.claude/skills"/pm-*/; do
    [[ -d "$d" ]] && skill_count=$((skill_count + 1))
  done
  if (( skill_count > 0 )); then
    ok "$skill_count PM skills installed"
  else
    warn "No PM skills found in ~/.claude/skills/"
  fi
  printf "\n"

  if [[ "${HEALTH_FAILED:-false}" == true ]]; then
    warn "Some services failed health checks. Run 'docker compose logs' to debug."
  fi

  printf "  ${DIM}Re-run ./setup.sh anytime to change settings or restart services.${RESET}\n\n"
}

# ─── Main ─────────────────────────────────────────────────────────────────────
main() {
  printf "\n${BOLD}${CYAN}"
  printf "  ____  _       _    ____                 _            \n"
  printf " |  _ \\(_)_ __ | | _|  _ \\ ___   ___  ___| |_ ___ _ __ \n"
  printf " | |_) | | '_ \\| |/ / |_) / _ \\ / _ \\/ __| __/ _ \\ '__|\n"
  printf " |  __/| | | | |   <|  _ < (_) | (_) \\__ \\ ||  __/ |   \n"
  printf " |_|   |_|_| |_|_|\\_\\_| \\_\\___/ \\___/|___/\\__\\___|_|   \n"
  printf "${RESET}\n"
  printf "  ${DIM}Project Management for AI-Assisted Development${RESET}\n"

  HEALTH_FAILED=false

  configure_env
  deploy_docker
  register_mcp
  print_summary
}

main "$@"
