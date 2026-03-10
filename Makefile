# ──────────────────────────────────────────────────────────────
#  PinkRooster 🐓  —  Development Makefile
# ──────────────────────────────────────────────────────────────

SOLUTION       := PinkRooster.slnx
API_PROJECT    := src/PinkRooster.Api
MCP_PROJECT    := src/PinkRooster.Mcp
DATA_PROJECT   := src/PinkRooster.Data
DASHBOARD_DIR  := src/dashboard
COMPOSE        := docker compose

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
	@$(COMPOSE) up -d postgres
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
#  Docker
# ──────────────────────────────────────────────────────────────

.PHONY: up
up: ## Start all containers (detached)
	@$(COMPOSE) up -d --build
	@printf "$(C_GREEN)✔ Services running$(C_RESET)\n"
	@printf "  API        → http://localhost:5100\n"
	@printf "  MCP        → http://localhost:5200\n"
	@printf "  Dashboard  → http://localhost:3000\n"

.PHONY: down
down: ## Stop all containers
	@$(COMPOSE) down

.PHONY: restart
restart: down up ## Rebuild and restart all containers

.PHONY: logs
logs: ## Tail container logs (all services)
	@$(COMPOSE) logs -f

.PHONY: logs-api
logs-api: ## Tail API container logs
	@$(COMPOSE) logs -f api

.PHONY: logs-mcp
logs-mcp: ## Tail MCP container logs
	@$(COMPOSE) logs -f mcp

.PHONY: logs-dashboard
logs-dashboard: ## Tail dashboard container logs
	@$(COMPOSE) logs -f dashboard

.PHONY: ps
ps: ## Show running containers
	@$(COMPOSE) ps

.PHONY: docker-build
docker-build: ## Build Docker images without starting
	@$(COMPOSE) build

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
setup: ## First-time setup (copy .env + install deps)
	@if [ ! -f .env ]; then \
		cp .env.example .env; \
		printf "$(C_GREEN)✔ Created .env from .env.example$(C_RESET)\n"; \
	else \
		printf "$(C_DIM)  .env already exists, skipping$(C_RESET)\n"; \
	fi
	@$(MAKE) install

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
	@printf "$(C_GREEN)✔ Nuked$(C_RESET)\n"
