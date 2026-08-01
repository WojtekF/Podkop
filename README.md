# Podkop

A Wykop-style social link-aggregation platform: users submit **findings**, vote on them, and the best ones get **promoted** to the main page.

Built as a learning project on .NET Aspire + Angular 22, with a deliberately opinionated architecture — vertical feature slices, Clean Architecture layering inside each slice, CQRS via MediatR on the backend, and signals-first standalone components on the frontend.

> **Project status: early-stage prototype.** The API serves in-memory sample data — there is no database and no authentication yet (the current user is a stub). Several feature slices are scaffolded with failing tests that specify agreed behavior but are not implemented. See [TODO.md](TODO.md) and the [issue tracker](https://github.com/WojtekF/AngularLearning/issues).

## Domain language

Podkop mirrors Wykop's observable behavior one-to-one (see [ADR 0002](docs/adr/0002-wykop-1-to-1-functional-copy.md)), and its vocabulary is canonical in code, UI copy, and docs alike:

| Term | Meaning |
| --- | --- |
| **Finding** | A link submitted by a user for the community to vote on — the central content unit. Never "post". |
| **Feed** | An ordered, pageable listing of findings. There are two: Main Page and Upcoming. |
| **Main Page** | The feed of promoted findings — the site's front page. |
| **Upcoming** | The feed of fresh, not-yet-promoted findings (Wykop's *Wykopalisko*). |
| **Promotion** | The one-way transition of a finding from Upcoming to the Main Page. |
| **Dig** / **Bury** | An upvote / downvote on a finding. Every bury carries a **bury reason** from a closed list. |
| **Upvote** / **Downvote** | A vote on a *comment* (comments never use dig/bury). |
| **Net Score** | Votes for minus votes against. |

The full glossary lives in [CONTEXT.md](CONTEXT.md).

## Tech stack

- **Backend** — ASP.NET Core (.NET 10) minimal APIs, orchestrated by **.NET Aspire**, with OpenAPI + Scalar UI and OpenTelemetry
- **Frontend** — **Angular 22** (standalone components, signals, NgRx SignalStore), Angular Material, SCSS
- **Persistence** — planned: PostgreSQL via the Aspire hosting integration + EF Core
- **Testing** — xUnit per feature slice (backend), Vitest + jsdom (frontend)

## Getting started

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download), [Node.js](https://nodejs.org) with npm 11+, and the [Aspire CLI / workload](https://learn.microsoft.com/dotnet/aspire/).

Run the full stack — API and Angular dev server together, with the Aspire dashboard:

```bash
dotnet run --project Podkop.AppHost --launch-profile https
```

That starts the API on `https://localhost:7460` (HTTP `5381`), the frontend on `http://localhost:4200` with HMR, and the Aspire dashboard on `https://localhost:17271`.

### Backend only

```bash
dotnet run --project Podkop.Server --launch-profile https
```

In Development the API exposes its OpenAPI document at `/openapi/v1.json` and the Scalar reference UI at `/scalar/v1`.

### Frontend only

Run from `frontend/`:

```bash
npm install
```

```bash
npm start
```

## Testing

Backend (xUnit, per-slice test projects):

```bash
dotnet test
```

Frontend (Vitest), from `frontend/`:

```bash
npm test
```

A single frontend spec:

```bash
npx vitest run src/main-page/main-page-feed.service.spec.ts
```

Some tests currently fail by design: features are scaffolded test-first, and the failing specs define the behavior still to be implemented.

## API

All endpoints are unauthenticated for now and read from in-memory sample data.

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/findings?feed=main&page=&limit=` | Main Page feed, page-number paginated ([ADR 0004](docs/adr/0004-page-number-pagination-for-feeds.md)) |
| `GET` | `/api/findings/{id}` | A single finding's detail |
| `PUT` | `/api/findings/{id}/my-vote` | Cast a dig or a bury (a bury requires a reason) |
| `DELETE` | `/api/findings/{id}/my-vote` | Withdraw the current user's vote |
| `GET` | `/api/findings/{findingId}/comments` | Comment threads for a finding (one level of replies deep) |
| `PUT` | `/api/comments/{commentId}/my-vote` | Upvote or downvote a comment |
| `DELETE` | `/api/comments/{commentId}/my-vote` | Withdraw the current user's comment vote |

## Repository layout

```
Podkop.slnx              # Solution manifest
Podkop.AppHost/          # Aspire orchestration — wires the server and the frontend together
Podkop.Server/           # ASP.NET Core host / composition root (DI wiring, endpoint mapping, sample seed)
Features/                # One folder per feature slice
  Findings/              #   Domain / Application / Infrastructure / Server / Tests projects
  FindingComments/       #   ditto
frontend/                # Angular 22 app
  src/app/               #   Root component, routing, app config
  src/main-page/         #   Main Page feed + finding cards
  src/finding-detail/    #   Finding detail page: comments, threads, voting
docs/adr/                # Architecture decision records
docs/agents/             # Conventions for AI-assisted work in this repo
CONTEXT.md               # Domain glossary (canonical vocabulary)
CLAUDE.md                # Guidance for Claude Code
TODO.md                  # Backlog
```

## Architecture

The backend follows **vertical slices × Clean Architecture** ([ADR 0003](docs/adr/0003-vertical-slices-clean-architecture.md)): every feature owns a `Domain`, `Application`, `Infrastructure`, `Server`, and `Tests` project. Dependencies point inward inside a slice (Server → Application → Domain), features never reach into each other's internals, and cross-feature communication goes through contract events. `Podkop.Server` is the composition root that references each slice's Server project and maps its endpoints.

The frontend is organised feature-per-folder with standalone components only, state in signals and NgRx SignalStore, HTTP confined to per-feature services, and `.spec.ts` files colocated with the code they test.

Decision records:

- [0001 — Promotion is a one-way recorded event, not a computed state](docs/adr/0001-one-way-promotion-event.md)
- [0002 — Podkop is a 1:1 functional copy of Wykop](docs/adr/0002-wykop-1-to-1-functional-copy.md)
- [0003 — Backend uses vertical slices with Clean Architecture layers per feature](docs/adr/0003-vertical-slices-clean-architecture.md)
- [0004 — Feeds paginate by page number, not cursor](docs/adr/0004-page-number-pagination-for-feeds.md)
- [0005 — Comments live in their own FindingComments slice, not inside Findings](docs/adr/0005-finding-comments-separate-slice.md)

## Contributing

- **Conventional Commits** — `feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`
- Work happens on branches named after the commit type and topic (`feat/finding-voting`, `fix/vote-count`, `docs/readme`); nothing is committed straight to `master`, and changes land only via reviewed pull requests
- Keep branches small and focused — one slice, fix, or concern each
- Write or update tests with every change, on both ends of the stack
- All code, comments, commit messages, and UI text are in English

By contributing you agree that your contributions are licensed under the same terms as the project.

## License

Licensed under the **[PolyForm Noncommercial License 1.0.0](LICENSE.md)**.

In short: you may use, modify, share, and build on Podkop freely **for any noncommercial purpose** — personal projects, study, research, teaching, hobby work, and use by charities, schools, and government bodies. **Commercial use is not granted by this license.** The software is provided as is, without warranty.

This is a source-available license, not an OSI-approved open-source one — the noncommercial restriction is what puts it outside that definition. For commercial licensing, contact the copyright holder ([@WojtekF](https://github.com/WojtekF)).

Required Notice: Copyright 2026 WojtekF
