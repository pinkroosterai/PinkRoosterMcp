# Contributing to PinkRooster

Thank you for your interest in contributing to PinkRooster! This guide will help you get started.

## Development Setup

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and Docker Compose
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/)
- [Make](https://www.gnu.org/software/make/)

### Getting Started

```bash
git clone https://github.com/pinkroosterai/PinkRoosterMcp.git
cd PinkRoosterMcp
make setup    # Copy .env template and install dashboard dependencies
make dev      # Start all services with hot reload
```

### Project Structure

```
src/
  PinkRooster.Shared/   # DTOs, enums, constants (no dependencies)
  PinkRooster.Data/     # EF Core entities, migrations, DbContext
  PinkRooster.Api/      # REST API — controllers, services, middleware
  PinkRooster.Mcp/      # MCP server — tools, responses, API client
  dashboard/            # React dashboard — Vite, TypeScript, shadcn/ui
tests/
  PinkRooster.Api.Tests/ # Integration tests (xUnit v3, Testcontainers)
```

## How to Contribute

### Reporting Bugs

Open an issue with:
- Steps to reproduce
- Expected vs. actual behavior
- Environment details (OS, .NET version, Docker version)

### Suggesting Features

Open a GitHub issue describing:
- The problem you're trying to solve
- Your proposed solution
- Any alternatives you've considered

### Submitting Changes

1. Fork the repository
2. Create a feature branch from `main`
3. Make your changes
4. Run tests to ensure nothing is broken:
   ```bash
   dotnet test                          # API + MCP integration tests
   cd src/dashboard && npm test         # Dashboard frontend tests
   ```
5. Open a pull request against `main`

## Code Style

- **C#**: 4-space indent, nullable enabled, implicit usings. See `.editorconfig`.
- **TypeScript/JS/CSS/JSON**: 2-space indent.
- **Line endings**: LF
- **Encoding**: UTF-8

Run formatting before committing:

```bash
make format   # Format .NET code
make lint     # Lint dashboard
```

## Architecture Guidelines

- **MCP server isolation**: The MCP project references `Shared` only — it communicates with the API via HTTP. Never add a reference to `Data` or `Api`.
- **MCP tools never throw**: All code paths must return an `OperationResult` with a clear message. Wrap API calls in try-catch.
- **Human-readable IDs**: Derived at read-time from auto-increment PKs. Never stored as a column. No GUIDs.
- **State cascades**: When changing entity states, use `StateCascadeService` to handle auto-block, auto-unblock, and upward propagation. Always include `stateChanges` in responses.
- **Audit logging**: Every field change must produce an audit entry. Use the existing per-entity audit log pattern.
- **Dashboard**: Use shadcn/ui components. Import state colors from `lib/state-colors.ts`. Follow the existing page patterns for list/detail/create pages.

## Testing

### API Integration Tests

Tests use Testcontainers (real PostgreSQL in Docker) with Respawn for database cleanup between tests. All async test methods must use `TestContext.Current.CancellationToken`.

```bash
dotnet test tests/PinkRooster.Api.Tests
```

### Dashboard Tests

Tests use Vitest + React Testing Library + MSW for API mocking.

```bash
cd src/dashboard
npm test              # Run tests
npm run test:coverage # With coverage report
```

## Questions?

Open a GitHub issue — we're happy to help.
