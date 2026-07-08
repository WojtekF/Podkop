# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Podkop** is a Wykop/Reddit-style social link-aggregation platform (posts, up/down votes, comments, tags) intended to grow into a full product. It is currently an early-stage prototype: the API serves mock data and there is no database or auth yet — but changes should build toward the target architecture described below, not just the current state.

All code, comments, commit messages, and UI text are in **English**.

## Tech Stack

- **Backend:** ASP.NET Core (.NET 10) minimal APIs, orchestrated by **.NET Aspire** (`Podkop.AppHost`), OpenAPI + Scalar UI, OpenTelemetry
- **Frontend:** **Angular 22** (standalone components, signals), Angular Material, SCSS, built with Angular CLI/Vite
- **Persistence (planned):** PostgreSQL via Aspire hosting integration + EF Core
- **Testing:** Vitest + jsdom (frontend), xUnit (backend — test project to be created)

## Repository Layout

```
Podkop.slnx                  # Solution manifest
Podkop.AppHost/              # Aspire orchestration — wires server + frontend
Podkop.Server/               # ASP.NET Core API (minimal APIs in Program.cs for now)
frontend/                    # Angular 22 app
  src/app/                   # Root component, routing, app config
  src/sink/                  # Feature: main post feed (component, service, post-card/)
TODO.md                      # Backlog
```

## Commands

**Full stack (preferred for development)** — starts server (HTTP 5381 / HTTPS 7460) and frontend dev server (4200) with HMR:

```bash
dotnet run --project Podkop.AppHost --launch-profile https
```

**Backend only:**

```bash
dotnet run --project Podkop.Server --launch-profile https   # https://localhost:7460
dotnet build                                                # build the solution
```

**Frontend** (run from `frontend/`):

```bash
npm start          # ng serve with HMR
npm run build      # production build
npm test           # Vitest (single run: npx vitest run)
```

Run a single frontend test file: `npx vitest run src/sink/sink.service.spec.ts`

## Target Architecture

### Backend — Vertical Slices × Clean Architecture (Milan Jovanović style)

The backend is evolving from inline endpoints in `Program.cs` toward a **feature-first modular structure**: each feature (Posts, Votes, Comments, …) lives in its own folder containing its own set of Clean Architecture **layer projects**:

```
Features/
  Posts/
    Podkop.Posts.Domain/            # entities, value objects, domain events; no dependencies
    Podkop.Posts.Application/       # commands/queries + handlers + validators for this feature
    Podkop.Posts.Infrastructure/    # EF Core (PostgreSQL), persistence, external services
    Podkop.Posts.Server/            # minimal API endpoints (MapGroup), thin HTTP layer
  Votes/
    Podkop.Votes.Domain/
    ...
```

`Podkop.Server` remains the composition root/host: it references each feature's Server project and wires everything together (DI registration, endpoint mapping, service defaults).

Conventions:

- **CQRS with MediatR**: `IRequest`/`IRequestHandler` per use case; endpoints dispatch through MediatR rather than calling services directly
- Dependency direction always points inward within a feature (Server → Application → Domain; Infrastructure implements Application/Domain abstractions); features don't reference each other's internals — cross-feature communication goes through contracts/events
- Keep the service-defaults pattern (`Extensions.cs`: OpenTelemetry, health checks, resilience) intact when restructuring

When adding a new feature, scaffold the full slice — its four layer projects plus command/query, handler, endpoint, and tests — rather than expanding `Program.cs`.

### Frontend — Feature folders, signals-first

- Standalone components only (no NgModules); feature-per-folder structure like `src/sink/`
- **Signals + NgRx SignalStore** for state as features grow; `toSignal()` interop for server data (current pattern in `SinkComponent`)
- HTTP calls live in per-feature services (e.g. `SinkService`), returning observables converted to signals at the component boundary
- Angular Material for UI components; SCSS per component plus global `src/styles.scss`
- Colocate `.spec.ts` files with the code they test

## Feature Development Workflow

When developing a new feature, follow this division of labor:

1. **Discovery first — ask exhaustively.** Before writing anything, interview the user in depth about the domain and intended behavior: entities and their invariants, use cases, edge cases, validation rules, error behavior, API shape. Don't fill gaps with assumptions — keep asking until the behavior is unambiguous.
2. **Claude implements structure and tests only.** Scaffold the feature slice (layer projects, endpoint/handler/entity skeletons, DI wiring) and write the unit and integration tests that specify the agreed behavior. Skeletons should compile but leave domain logic unimplemented (e.g. `throw new NotImplementedException()`), so the new tests fail until the logic is written.
3. **The user writes the domain logic.** Do not implement business/domain logic unless explicitly asked to. The failing tests define what the user's implementation must satisfy.

## Testing Expectations

Write or update tests with every change, on both ends:

- **Backend:** xUnit. Prefer integration-style tests via `WebApplicationFactory` for endpoints, unit tests for handlers/domain logic. (The test project doesn't exist yet — create `Podkop.Tests` alongside the other projects when first needed.)
- **Frontend:** Vitest specs colocated with components/services

## Verification Workflow

- Verify changes by building (`dotnet build`, `npm run build`) and running the test suites
- API changes may additionally be verified by running `Podkop.Server` headlessly and hitting endpoints with `curl`
- **Never launch a browser preview or UI panel** — the user verifies all UI changes manually themselves

## Git Conventions

- **Conventional Commits**: `feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:` etc.
- **Never commit or push directly to `master`.** All work happens on branches named after the commit type and topic (`feat/post-voting`, `fix/vote-count`, `docs/readme`). Changes reach `master` only via merge after review, and the **user** performs the merge — Claude never merges into `master` itself. If asked to commit while on `master`, create an appropriately named branch first.
- **Keep branches small and focused** — one feature slice, fix, or concern per branch — so per-diff reviews (`/code-review`, the `arch-review` agent) stay small and meaningful. (The `security-scan` agent is different: it audits the whole codebase, not the branch diff.)

## Known Issues

- `Microsoft.OpenApi` 2.0.0 (transitive) has a high-severity advisory (GHSA-v5pm-xwqc-g5wc) — see TODO.md; pin to a patched version or wait for the ASP.NET Core fix

## Agent skills

### Issue tracker

Issues are tracked as GitHub Issues in this repo's `origin` remote, via the `gh` CLI (which infers the repo automatically); external PRs are not a triage surface. See `docs/agents/issue-tracker.md`.

### Triage labels

Canonical defaults: needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `docs/adr/` at the repo root (created lazily by `/domain-modeling`; they may not exist yet). See `docs/agents/domain.md`.

### Skill overrides

- **tdd — red only.** Regardless of what `.agents/skills/tdd/SKILL.md` says, Claude writes compiling skeletons and failing tests only; the green (implementation) step belongs to the user, per the Feature Development Workflow above. This section is authoritative even if a skills update reverts the override text inside the vendored file — after running `npx skills add/update`, check `git diff .agents/skills/tdd/SKILL.md` and restore the "Project override (Podkop)" section if it was overwritten.
