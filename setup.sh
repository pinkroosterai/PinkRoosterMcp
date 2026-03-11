#!/usr/bin/env bash
set -euo pipefail

# ─── PinkRooster Setup Script ────────────────────────────────────────────────
# Idempotent script for configuring, building, deploying, and registering
# PinkRooster. Safe to re-run for config changes or restarts.
#
# Usage: ./setup.sh [--config-only]
#   --config-only   Update .env without rebuilding Docker or re-registering MCP
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

ok()     { printf "${GREEN}  %s${RESET}\n" "$1"; }
fail()   { printf "${RED}  %s${RESET}\n" "$1"; }
info()   { printf "${CYAN}  %s${RESET}\n" "$1"; }
warn()   { printf "${YELLOW}  %s${RESET}\n" "$1"; }
step()   { printf "\n${BOLD}${CYAN}── [%s/4] %s ──${RESET}\n\n" "$1" "$2"; }

# ─── Flags ───────────────────────────────────────────────────────────────────
CONFIG_ONLY=false
for arg in "$@"; do
  case "$arg" in
    --config-only) CONFIG_ONLY=true ;;
    -h|--help)
      printf "Usage: ./setup.sh [--config-only]\n"
      printf "  --config-only   Update .env without rebuilding Docker or re-registering MCP\n"
      exit 0
      ;;
    *)
      printf "Unknown option: %s\n" "$arg"
      printf "Usage: ./setup.sh [--config-only]\n"
      exit 1
      ;;
  esac
done

# ─── Secret generation ───────────────────────────────────────────────────────
generate_secret() {
  openssl rand -base64 48 2>/dev/null | tr -d '/+=' | head -c 32
}

# ─── Step 1: Environment file ────────────────────────────────────────────────
configure_env() {
  step 1 "Environment Configuration"

  # Load existing values if .env exists
  local existing_pg_password="" existing_api_key="" existing_mcp_key=""
  local existing_dash_user="" existing_dash_password=""

  if [[ -f .env ]]; then
    info "Found existing .env — loading current values as defaults"
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

  # Backup existing .env before overwriting
  if [[ -f .env ]]; then
    cp .env .env.bak
  fi

  # Write .env (quoted values to handle special characters)
  cat > .env <<'ENVEOF'
POSTGRES_PASSWORD=
API_KEY=

# MCP server authentication (optional — leave empty for open access)
MCP_API_KEY=

# Dashboard authentication (optional — leave empty for open access)
DASHBOARD_USER=
DASHBOARD_PASSWORD=
ENVEOF

  # Write actual values safely (handles special chars in passwords)
  sed -i "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=${POSTGRES_PASSWORD//|/\\|}|" .env
  sed -i "s|^API_KEY=.*|API_KEY=${API_KEY//|/\\|}|" .env
  sed -i "s|^MCP_API_KEY=.*|MCP_API_KEY=${MCP_API_KEY//|/\\|}|" .env
  sed -i "s|^DASHBOARD_USER=.*|DASHBOARD_USER=${DASHBOARD_USER//|/\\|}|" .env
  sed -i "s|^DASHBOARD_PASSWORD=.*|DASHBOARD_PASSWORD=${DASHBOARD_PASSWORD//|/\\|}|" .env
  chmod 600 .env

  ok "Environment file written to .env (mode 600)"
  if [[ -f .env.bak ]]; then
    info "Previous config backed up to .env.bak"
  fi

  # Show what changed
  printf "\n"
  printf "  ${BOLD}Configuration summary:${RESET}\n"
  if [[ -n "$existing_pg_password" && "$existing_pg_password" == "$POSTGRES_PASSWORD" ]]; then
    printf "  %-18s ${DIM}unchanged${RESET}\n" "POSTGRES_PASSWORD"
  elif [[ -n "$existing_pg_password" ]]; then
    printf "  %-18s ${YELLOW}changed${RESET}\n" "POSTGRES_PASSWORD"
  else
    printf "  %-18s ${GREEN}generated${RESET}\n" "POSTGRES_PASSWORD"
  fi

  if [[ -n "$existing_api_key" && "$existing_api_key" == "$API_KEY" ]]; then
    printf "  %-18s ${DIM}unchanged${RESET}\n" "API_KEY"
  elif [[ -n "$existing_api_key" ]]; then
    printf "  %-18s ${YELLOW}changed${RESET}\n" "API_KEY"
  else
    printf "  %-18s ${GREEN}generated${RESET}\n" "API_KEY"
  fi

  if [[ -n "$DASHBOARD_USER" ]]; then
    if [[ -n "$existing_dash_user" ]]; then
      printf "  %-18s ${GREEN}enabled${RESET} (user: %s)\n" "Dashboard auth" "$DASHBOARD_USER"
    else
      printf "  %-18s ${GREEN}newly enabled${RESET} (user: %s)\n" "Dashboard auth" "$DASHBOARD_USER"
    fi
  elif [[ -n "$existing_dash_user" ]]; then
    printf "  %-18s ${YELLOW}disabled${RESET} (was: %s)\n" "Dashboard auth" "$existing_dash_user"
  else
    printf "  %-18s ${DIM}off${RESET}\n" "Dashboard auth"
  fi

  if [[ -n "$MCP_API_KEY" ]]; then
    if [[ -n "$existing_mcp_key" ]]; then
      printf "  %-18s ${GREEN}enabled${RESET}\n" "MCP auth"
    else
      printf "  %-18s ${GREEN}newly enabled${RESET}\n" "MCP auth"
    fi
  elif [[ -n "$existing_mcp_key" ]]; then
    printf "  %-18s ${YELLOW}disabled${RESET}\n" "MCP auth"
  else
    printf "  %-18s ${DIM}off${RESET}\n" "MCP auth"
  fi
}

# ─── Step 2: Preflight checks ────────────────────────────────────────────────
preflight_checks() {
  step 2 "Preflight Checks"

  local failed=false

  # Docker CLI
  if ! command -v docker &>/dev/null; then
    fail "Docker is not installed or not in PATH"
    failed=true
  else
    ok "Docker CLI found"
  fi

  # Docker Compose
  if ! docker compose version &>/dev/null; then
    fail "Docker Compose is not available (need docker compose v2+)"
    failed=true
  else
    ok "Docker Compose $(docker compose version --short 2>/dev/null || echo "v2+") found"
  fi

  # Docker daemon
  if ! docker info &>/dev/null 2>&1; then
    fail "Docker daemon is not running"
    info "Start Docker Desktop or run 'sudo systemctl start docker'"
    failed=true
  else
    ok "Docker daemon is running"
  fi

  if [[ "$failed" == true ]]; then
    printf "\n"
    fail "Preflight checks failed — fix the above issues and re-run ./setup.sh"
    exit 1
  fi

  # Port checks (non-fatal warnings)
  local ports=("5432:PostgreSQL" "5100:API" "5200:MCP" "3000:Dashboard")
  for entry in "${ports[@]}"; do
    local port="${entry%%:*}"
    local name="${entry##*:}"
    # Check if something non-Docker is using the port
    if ss -tlnp 2>/dev/null | grep -q ":${port} " && \
       ! docker compose ps --format '{{.Ports}}' 2>/dev/null | grep -q "${port}"; then
      warn "Port $port ($name) is in use by another process — Docker may fail to bind"
    fi
  done
}

# ─── Step 3: Docker build & deploy ───────────────────────────────────────────
deploy_docker() {
  step 3 "Docker Build & Deploy"

  info "Building and starting services..."
  printf "\n"

  if docker compose up -d --build; then
    printf "\n"
    ok "Docker stack started"
  else
    printf "\n"
    fail "Docker compose failed — run 'docker compose logs' to investigate"
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
        printf "\033[2K"
        ok "PostgreSQL is ready"
      fi
    fi

    # API
    if [[ "$api_ok" == false && "$pg_ok" == true ]]; then
      if curl -sf http://localhost:5100/health >/dev/null 2>&1; then
        api_ok=true
        printf "\033[2K"
        ok "API is ready (http://localhost:5100)"
      fi
    fi

    # MCP
    if [[ "$mcp_ok" == false && "$api_ok" == true ]]; then
      if curl -sf http://localhost:5200/health >/dev/null 2>&1; then
        mcp_ok=true
        printf "\033[2K"
        ok "MCP server is ready (http://localhost:5200)"
      fi
    fi

    # Dashboard
    if [[ "$dash_ok" == false && "$api_ok" == true ]]; then
      if curl -sf http://localhost:3000 >/dev/null 2>&1; then
        dash_ok=true
        printf "\033[2K"
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
    printf "\033[2K  ${DIM}... waiting (%ds/%ds)${RESET}\r" "$elapsed" "$timeout"
  done

  # Timeout — report what failed, showing dependency chain
  printf "\033[2K\n"
  warn "Health check timeout after ${timeout}s. Status:"
  if [[ "$pg_ok" == true ]]; then ok "PostgreSQL: healthy"; else fail "PostgreSQL: not ready (blocker — all services depend on this)"; fi
  if [[ "$api_ok" == true ]]; then
    ok "API: healthy"
  elif [[ "$pg_ok" == false ]]; then
    fail "API: not checked (waiting on PostgreSQL)"
  else
    fail "API: not ready (blocker — MCP + Dashboard depend on this)"
  fi
  if [[ "$mcp_ok" == true ]]; then
    ok "MCP: healthy"
  elif [[ "$api_ok" == false ]]; then
    fail "MCP: not checked (waiting on API)"
  else
    fail "MCP: not ready"
  fi
  if [[ "$dash_ok" == true ]]; then
    ok "Dashboard: healthy"
  elif [[ "$api_ok" == false ]]; then
    fail "Dashboard: not checked (waiting on API)"
  else
    fail "Dashboard: not ready"
  fi
  printf "\n"
  warn "Run 'docker compose logs' to investigate"
  HEALTH_FAILED=true
}

# ─── Step 4a: Install skills ──────────────────────────────────────────────────
install_skills() {
  local skills_src="$SCRIPT_DIR/.claude/skills"
  local skills_dst="$HOME/.claude/skills"
  SKILLS_COPIED=0

  if [[ -d "$skills_src" ]]; then
    mkdir -p "$skills_dst"
    local skill_names=()
    for skill_dir in "$skills_src"/pm-*/; do
      if [[ -d "$skill_dir" ]]; then
        cp -r "$skill_dir" "$skills_dst/"
        skill_names+=("$(basename "$skill_dir")")
        SKILLS_COPIED=$((SKILLS_COPIED + 1))
      fi
    done
    ok "Copied $SKILLS_COPIED PM skills to $skills_dst"
    for name in "${skill_names[@]}"; do
      printf "  ${DIM}  /%s${RESET}\n" "$name"
    done
  else
    warn "Skills directory not found at $skills_src"
  fi
}

# ─── Step 4b: MCP registration ───────────────────────────────────────────────
register_mcp() {
  # Check claude CLI is available
  if ! command -v claude &>/dev/null; then
    warn "Claude Code CLI not found — skipping MCP registration"
    info "Install Claude Code, then re-run ./setup.sh"
    MCP_REGISTERED=false
    return 0
  fi

  # Remove existing registration first (idempotent re-registration)
  if claude mcp get pinkrooster &>/dev/null 2>&1; then
    claude mcp remove --scope user pinkrooster &>/dev/null 2>&1 || true
    info "Removed existing MCP registration"
  fi

  # Register MCP server (user-scoped, available to all projects)
  if [[ -n "$MCP_API_KEY" ]]; then
    if claude mcp add --transport http --scope user \
      --header "X-Api-Key: $MCP_API_KEY" \
      pinkrooster http://localhost:5200 2>/dev/null; then
      ok "MCP server registered (user-scoped, with API key)"
      MCP_REGISTERED=true
    else
      fail "Failed to register MCP server"
      MCP_REGISTERED=false
    fi
  else
    if claude mcp add --transport http --scope user \
      pinkrooster http://localhost:5200 2>/dev/null; then
      ok "MCP server registered (user-scoped, open access)"
      MCP_REGISTERED=true
    else
      fail "Failed to register MCP server"
      MCP_REGISTERED=false
    fi
  fi
}

# ─── Summary ─────────────────────────────────────────────────────────────────
print_summary() {
  printf "\n${BOLD}${CYAN}── Setup Complete ──${RESET}\n\n"

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
  if [[ "${MCP_REGISTERED:-false}" == true ]]; then
    ok "MCP server registered (user-scoped)"
  else
    fail "MCP server not registered"
  fi
  if (( ${SKILLS_COPIED:-0} > 0 )); then
    ok "$SKILLS_COPIED PM skills installed"
  else
    warn "No PM skills installed"
  fi
  printf "\n"

  # Warnings
  if [[ "${HEALTH_FAILED:-false}" == true ]]; then
    warn "Some services failed health checks. Run 'docker compose logs' to debug."
  fi

  printf "  ${DIM}Re-run ./setup.sh anytime to change settings or restart services.${RESET}\n"
  printf "  ${DIM}Use ./setup.sh --config-only to update .env without rebuilding.${RESET}\n\n"
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
  MCP_REGISTERED=false
  SKILLS_COPIED=0

  configure_env

  if [[ "$CONFIG_ONLY" == true ]]; then
    printf "\n"
    ok "Config-only mode — skipping Docker build and MCP registration."
    info "Run ./setup.sh (without --config-only) for a full deploy."
    printf "\n"
    return 0
  fi

  preflight_checks
  deploy_docker

  step 4 "Claude Code Integration"
  install_skills
  printf "\n"
  register_mcp

  print_summary
}

main "$@"
