# ──────────────────────────────────────────────────────────────
#  PinkRooster 🐓  —  Development Makefile
# ──────────────────────────────────────────────────────────────

SOLUTION       := PinkRooster.slnx
API_PROJECT    := src/PinkRooster.Api
MCP_PROJECT    := src/PinkRooster.Mcp
DATA_PROJECT   := src/PinkRooster.Data
DASHBOARD_DIR  := src/dashboard
COMPOSE        := docker compose
COMPOSE_DEV    := docker compose -f docker-compose.dev.yml -f docker-compose.dev.override.yml

# Colors (ANSI)
C_RESET  := \033[0m
C_BOLD   := \033[1m
C_CYAN   := \033[36m
C_GREEN  := \033[32m
C_YELLOW := \033[33m
C_DIM    := \033[2m

# ──────────────────────────────────────────────────────────────
#  Help
# ──────────────────────────────────────────────────────────────

.DEFAULT_GOAL := help

.PHONY: help
help: ## Show this help
	@printf "\n$(C_BOLD)$(C_CYAN) 🐓 PinkRooster$(C_RESET)$(C_DIM) — available targets$(C_RESET)\n\n"
	@grep -E '^[a-zA-Z_-]+:.*?##' $(MAKEFILE_LIST) | \
		awk 'BEGIN {FS = ":.*?## "}; { \
			split($$1, a, ":"); \
			printf "  $(C_GREEN)%-18s$(C_RESET) %s\n", a[1], $$2 \
		}'
	@printf "\n"

# ──────────────────────────────────────────────────────────────
#  Build
# ──────────────────────────────────────────────────────────────

.PHONY: build
build: ## Build everything (.NET + dashboard)
	@printf "$(C_CYAN)▸ Building .NET solution…$(C_RESET)\n"
	@dotnet build $(SOLUTION) -q
	@printf "$(C_CYAN)▸ Building dashboard…$(C_RESET)\n"
	@cd $(DASHBOARD_DIR) && npm run build
	@printf "$(C_GREEN)✔ All builds succeeded$(C_RESET)\n"

.PHONY: build-api
build-api: ## Build .NET solution only
	@dotnet build $(SOLUTION)

.PHONY: build-dashboard
build-dashboard: ## Build dashboard only
	@cd $(DASHBOARD_DIR) && npm run build

# ──────────────────────────────────────────────────────────────
#  Development  (local processes + Docker PostgreSQL)
# ──────────────────────────────────────────────────────────────

.PHONY: dev
dev: ## Start all services locally (DB + API + MCP + dashboard)
	@printf "$(C_BOLD)$(C_CYAN)▸ Starting PostgreSQL container…$(C_RESET)\n"
	@docker compose -f docker-compose.dev.yml up -d postgres
	@printf "$(C_BOLD)$(C_CYAN)▸ Starting all services — Ctrl+C to stop$(C_RESET)\n"
	@$(MAKE) -j3 dev-api dev-mcp dev-dashboard

.PHONY: dev-api
dev-api: ## Start API server (hot reload)
	@printf "$(C_YELLOW)▸ API$(C_RESET)        → http://localhost:5100\n"
	@cd $(API_PROJECT) && dotnet watch run --environment Development

.PHONY: dev-mcp
dev-mcp: ## Start MCP server (hot reload)
	@printf "$(C_YELLOW)▸ MCP$(C_RESET)        → http://localhost:5200\n"
	@cd $(MCP_PROJECT) && dotnet watch run --environment Development

.PHONY: dev-dashboard
dev-dashboard: ## Start dashboard dev server
	@printf "$(C_YELLOW)▸ Dashboard$(C_RESET)  → http://localhost:5173\n"
	@cd $(DASHBOARD_DIR) && npm run dev

# ──────────────────────────────────────────────────────────────
#  Docker (unified image — default for users)
# ──────────────────────────────────────────────────────────────

.PHONY: up
up: ## Start all containers (unified image)
	@$(COMPOSE) up -d --build
	@printf "$(C_GREEN)✔ Services running$(C_RESET)\n"
	@printf "  Dashboard  → http://localhost:3000\n"
	@printf "  API        → http://localhost:5100\n"
	@printf "  MCP        → http://localhost:5200\n"
	@printf "  Swagger    → http://localhost:5100/swagger/index.html\n"

.PHONY: down
down: ## Stop all containers
	@$(COMPOSE) down

.PHONY: restart
restart: down up ## Rebuild and restart all containers

.PHONY: logs
logs: ## Tail container logs
	@$(COMPOSE) logs -f

.PHONY: ps
ps: ## Show running containers
	@$(COMPOSE) ps

# ──────────────────────────────────────────────────────────────
#  Docker (multi-image — for developers)
# ──────────────────────────────────────────────────────────────

.PHONY: dev-up
dev-up: ## Start multi-image containers with hot reload
	@$(COMPOSE_DEV) up -d --build
	@printf "$(C_GREEN)✔ Dev services running$(C_RESET)\n"
	@printf "  API        → http://localhost:5100\n"
	@printf "  MCP        → http://localhost:5200\n"
	@printf "  Dashboard  → http://localhost:3000\n"

.PHONY: dev-down
dev-down: ## Stop multi-image dev containers
	@$(COMPOSE_DEV) down

.PHONY: dev-logs
dev-logs: ## Tail multi-image dev container logs
	@$(COMPOSE_DEV) logs -f

.PHONY: dev-ps
dev-ps: ## Show running dev containers
	@$(COMPOSE_DEV) ps

# ──────────────────────────────────────────────────────────────
#  Database  (EF Core migrations)
# ──────────────────────────────────────────────────────────────

.PHONY: db-migrate
db-migrate: ## Apply pending migrations
	@printf "$(C_CYAN)▸ Applying migrations…$(C_RESET)\n"
	@dotnet ef database update --project $(DATA_PROJECT) --startup-project $(API_PROJECT)
	@printf "$(C_GREEN)✔ Database up to date$(C_RESET)\n"

.PHONY: db-migration
db-migration: ## Create a migration  (usage: make db-migration name=AddSomeTable)
	@if [ -z "$(name)" ]; then \
		printf "$(C_YELLOW)Usage: make db-migration name=MigrationName$(C_RESET)\n"; \
		exit 1; \
	fi
	@printf "$(C_CYAN)▸ Creating migration: $(name)$(C_RESET)\n"
	@dotnet ef migrations add $(name) --project $(DATA_PROJECT) --startup-project $(API_PROJECT)

.PHONY: db-status
db-status: ## Show migration status
	@dotnet ef migrations list --project $(DATA_PROJECT) --startup-project $(API_PROJECT)

.PHONY: db-rollback
db-rollback: ## Rollback last migration
	@printf "$(C_YELLOW)▸ Rolling back last migration…$(C_RESET)\n"
	@dotnet ef database update 0 --project $(DATA_PROJECT) --startup-project $(API_PROJECT)

.PHONY: db-reset
db-reset: ## Drop and recreate database (destructive!)
	@printf "$(C_YELLOW)⚠ This will destroy all data. Press Ctrl+C to cancel…$(C_RESET)\n"
	@sleep 3
	@dotnet ef database drop --force --project $(DATA_PROJECT) --startup-project $(API_PROJECT)
	@dotnet ef database update --project $(DATA_PROJECT) --startup-project $(API_PROJECT)
	@printf "$(C_GREEN)✔ Database recreated$(C_RESET)\n"

# ──────────────────────────────────────────────────────────────
#  Utilities
# ──────────────────────────────────────────────────────────────

.PHONY: install
install: ## Install all dependencies (.NET restore + npm install)
	@printf "$(C_CYAN)▸ Restoring .NET packages…$(C_RESET)\n"
	@dotnet restore $(SOLUTION) -q
	@printf "$(C_CYAN)▸ Installing npm packages…$(C_RESET)\n"
	@cd $(DASHBOARD_DIR) && npm install
	@printf "$(C_GREEN)✔ All dependencies installed$(C_RESET)\n"

.PHONY: lint
lint: ## Lint dashboard code
	@cd $(DASHBOARD_DIR) && npm run lint

.PHONY: format
format: ## Format .NET code
	@dotnet format $(SOLUTION)

.PHONY: setup
setup: ## Quick start: configure, register MCP, install skills, start containers
	@printf "\n$(C_BOLD)$(C_CYAN) 🐓 PinkRooster Setup$(C_RESET)\n\n"
	@# 1. Copy .env if it doesn't exist
	@if [ ! -f .env ]; then \
		cp .env.example .env; \
		printf "$(C_GREEN)  ✔ Created .env from .env.example$(C_RESET)\n"; \
	else \
		printf "$(C_DIM)  .env already exists, skipping$(C_RESET)\n"; \
	fi
	@# 2. Register MCP server in Claude Code (global scope)
	@if command -v claude >/dev/null 2>&1; then \
		claude mcp remove --scope user pinkrooster >/dev/null 2>&1 || true; \
		if claude mcp add --transport http --scope user pinkrooster http://localhost:5200 >/dev/null 2>&1; then \
			printf "$(C_GREEN)  ✔ MCP server registered in Claude Code (global)$(C_RESET)\n"; \
		else \
			printf "$(C_YELLOW)  ⚠ Failed to register MCP server in Claude Code$(C_RESET)\n"; \
		fi; \
	else \
		printf "$(C_DIM)  Claude Code CLI not found, skipping MCP registration$(C_RESET)\n"; \
	fi
	@# 3. Install PM skills globally
	@mkdir -p ~/.claude/skills
	@if [ -d .claude/skills ]; then \
		cp -r .claude/skills/* ~/.claude/skills/ 2>/dev/null || true; \
		printf "$(C_GREEN)  ✔ PM skills installed to ~/.claude/skills/$(C_RESET)\n"; \
	fi
	@# 4. Start containers
	@printf "\n$(C_CYAN)  ▸ Starting containers…$(C_RESET)\n\n"
	@$(MAKE) up

.PHONY: setup-dev
setup-dev: ## Developer setup: configure, install deps, register MCP, start dev containers
	@printf "\n$(C_BOLD)$(C_CYAN) 🐓 PinkRooster Developer Setup$(C_RESET)\n\n"
	@# 1. Copy .env if it doesn't exist
	@if [ ! -f .env ]; then \
		cp .env.example .env; \
		printf "$(C_GREEN)  ✔ Created .env from .env.example$(C_RESET)\n"; \
	else \
		printf "$(C_DIM)  .env already exists, skipping$(C_RESET)\n"; \
	fi
	@# 2. Install dependencies
	@printf "$(C_CYAN)  ▸ Restoring .NET packages…$(C_RESET)\n"
	@dotnet restore $(SOLUTION) -q
	@printf "$(C_CYAN)  ▸ Installing npm packages…$(C_RESET)\n"
	@cd $(DASHBOARD_DIR) && npm install --silent
	@printf "$(C_GREEN)  ✔ Dependencies installed$(C_RESET)\n"
	@# 3. Register MCP server in Claude Code (global scope)
	@if command -v claude >/dev/null 2>&1; then \
		claude mcp remove --scope user pinkrooster >/dev/null 2>&1 || true; \
		if claude mcp add --transport http --scope user pinkrooster http://localhost:5200 >/dev/null 2>&1; then \
			printf "$(C_GREEN)  ✔ MCP server registered in Claude Code (global)$(C_RESET)\n"; \
		else \
			printf "$(C_YELLOW)  ⚠ Failed to register MCP server in Claude Code$(C_RESET)\n"; \
		fi; \
	else \
		printf "$(C_DIM)  Claude Code CLI not found, skipping MCP registration$(C_RESET)\n"; \
	fi
	@# 4. Install PM skills globally
	@mkdir -p ~/.claude/skills
	@if [ -d .claude/skills ]; then \
		cp -r .claude/skills/* ~/.claude/skills/ 2>/dev/null || true; \
		printf "$(C_GREEN)  ✔ PM skills installed to ~/.claude/skills/$(C_RESET)\n"; \
	fi
	@# 5. Start dev containers
	@printf "\n$(C_CYAN)  ▸ Starting dev containers…$(C_RESET)\n\n"
	@$(MAKE) dev-up

.PHONY: clean
clean: ## Remove build artifacts
	@printf "$(C_CYAN)▸ Cleaning…$(C_RESET)\n"
	@dotnet clean $(SOLUTION) -q
	@rm -rf $(DASHBOARD_DIR)/dist
	@printf "$(C_GREEN)✔ Clean$(C_RESET)\n"

.PHONY: nuke
nuke: clean ## Deep clean (artifacts + node_modules + Docker volumes)
	@rm -rf $(DASHBOARD_DIR)/node_modules
	@$(COMPOSE) down -v 2>/dev/null || true
	@$(COMPOSE_DEV) down -v 2>/dev/null || true
	@printf "$(C_GREEN)✔ Nuked$(C_RESET)\n"
