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
Podkop.Server/               # ASP.NET Core API host / composition root
Features/
  Findings/                  # Findings feature slice (Domain/Application/Infrastructure/Server/Tests projects)
Shared/
  Podkop.Shared.Infrastructure/  # Cross-slice infrastructure helpers (sample-data vocabulary for seed generators)
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

The backend is evolving from inline endpoints in `Program.cs` toward a **feature-first modular structure**: each feature (Findings, Votes, Comments, …) lives in its own folder containing its own set of Clean Architecture **layer projects**. The decision record and rationale live in `docs/adr/0003-vertical-slices-clean-architecture.md`; this section is the operational reference. (Domain vocabulary is defined in `CONTEXT.md` and is canonical in code identifiers as well as prose and UI copy — e.g. "Finding", never "Post".)

```
Features/
  Findings/
    Podkop.Findings.Domain/         # entities, value objects, domain events; no dependencies
    Podkop.Findings.Application/    # commands/queries + handlers + validators for this feature
    Podkop.Findings.Infrastructure/ # EF Core (PostgreSQL), persistence, external services
    Podkop.Findings.Server/         # minimal API endpoints (MapGroup), thin HTTP layer
    Podkop.Findings.Contracts/      # optional: cross-feature contract events (ADR 0003)
    Podkop.Findings.Tests/          # xUnit tests for this slice (domain unit + endpoint integration)
  Votes/
    Podkop.Votes.Domain/
    ...
```

`Podkop.Server` remains the composition root/host: it references each feature's Server project and wires everything together (DI registration, endpoint mapping, service defaults).

Conventions:

- **CQRS with MediatR**: `IRequest`/`IRequestHandler` per use case; endpoints dispatch through MediatR rather than calling services directly
- Dependency direction always points inward within a feature (Server → Application → Domain; Infrastructure implements Application/Domain abstractions); features don't reference each other's internals — cross-feature communication goes through contracts/events
- **Cross-feature events** go through an optional `Podkop.<Feature>.Contracts` project — ADR 0003 is the canonical statement of the pattern's rules
- Keep the service-defaults pattern (`Extensions.cs`: OpenTelemetry, health checks, resilience) intact when restructuring

When adding a new feature, scaffold the full slice — its four layer projects plus a `Podkop.<Feature>.Tests` project with command/query, handler, endpoint, and tests — rather than expanding `Program.cs`.

### Frontend — Feature folders, signals-first

- Standalone components only (no NgModules); feature-per-folder structure like `src/sink/`
- **Signals + NgRx SignalStore** for state as features grow; `toSignal()` interop for server data (current pattern in `SinkComponent`)
- HTTP calls live in per-feature services (e.g. `SinkService`), returning observables converted to signals at the component boundary
- Angular Material for UI components; SCSS per component plus global `src/styles.scss`
- Colocate `.spec.ts` files with the code they test

## Feature Development Workflow

When developing a new feature, follow this division of labor:

1. **Discovery first — ask exhaustively.** Before writing anything, interview the user in depth about the domain and intended behavior: entities and their invariants, use cases, edge cases, validation rules, error behavior, API shape. Don't fill gaps with assumptions — keep asking until the behavior is unambiguous.
2. **Claude implements structure and tests only — on both ends of the stack.** Scaffold the backend slice (per the Target Architecture section above) and the frontend feature folder, and write the unit and integration tests that specify the agreed behavior. Skeletons must compile but leave all behavioral decisions unimplemented (throwing), and scaffolded `.html`/`.scss` files hold only a guidance comment describing the _what_, never the _how_, so the new xUnit tests **and** Vitest specs fail until the logic is written. The operational rules — the implemented-vs-throwing line, throw idioms, empty-template and guidance-comment rules, recipes, and spec idioms — live **only** in `docs/agents/scaffolding.md`; read it **before** scaffolding.
3. **The user writes the logic — backend and frontend alike.** "Logic" is not just C# domain code: Angular store methods, component behavior, service bodies, templates, and styles count too. Do not implement any of it unless explicitly asked to, and do not propose a split where Claude builds the frontend fully. The failing tests on both ends define what the user's implementation must satisfy.

## Testing Expectations

Write or update tests with every change, on both ends:

- **Backend:** xUnit in the feature's own `Podkop.<Feature>.Tests` project inside its slice (e.g. `Features/Findings/Podkop.Findings.Tests`) — tests are part of the slice, not a shared root project. Prefer integration-style tests via `WebApplicationFactory` for endpoints, unit tests for handlers/domain logic.
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
- Installing new frontend packages may fail on an optional-peer conflict from `@analogjs/vite-plugin-angular` (it optionally peers on `@angular-devkit/build-angular`, whose latest release peers Angular 21). Retry with `npm install --legacy-peer-deps`; remove this note once Analog resolves the peer range.

## Agent skills

### Issue tracker

Issues are tracked as GitHub Issues in this repo's `origin` remote, via the `gh` CLI (which infers the repo automatically); external PRs are not a triage surface. See `docs/agents/issue-tracker.md`.

### Triage labels

Canonical defaults: needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `docs/adr/` at the repo root (created lazily by `/domain-modeling`; they may not exist yet). See `docs/agents/domain.md`.

### Scaffolding conventions

The Feature Development Workflow's operational calibration — what's implemented vs. what throws in a red-only scaffold, backend/frontend recipes, spec idioms, and the process checklist. Read it **before** scaffolding instead of re-deriving the rules from past scaffold commits. See `docs/agents/scaffolding.md`.

### Skill overrides

- **tdd — red only.** Regardless of what `.agents/skills/tdd/SKILL.md` says, Claude writes compiling skeletons and failing tests only; the green (implementation) step belongs to the user, per the Feature Development Workflow above. This section is authoritative even if a skills update reverts the override text inside the vendored file — after running `npx skills add/update`, check `git diff .agents/skills/tdd/SKILL.md` and restore the "Project override (Podkop)" section if it was overwritten.
