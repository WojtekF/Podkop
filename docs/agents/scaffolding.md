# Scaffolding a feature (red-only)

Operational reference for the **Feature Development Workflow** in CLAUDE.md: Claude scaffolds
structure and failing tests on both ends of the stack; the user implements all logic,
templates, and styles. This file pins down the calibration that otherwise has to be
re-derived from git history on every scaffolding session.

Canonical examples: PR #20 (finding detail page, issue #14) and PR #21 (FindingComments
read slice, issue #16). Canonical templates to mirror: `Features/Findings` and
`frontend/src/finding-detail`.

## What is implemented vs. what throws

The line is **structure vs. decision**: code with exactly one obvious spelling is
implemented; code embodying a behavioral decision throws and is the user's to write.

**Implemented in the scaffold (structural):**

- Thin minimal-API endpoint lambdas: parameter validation, dispatch through MediatR, map
  `null` → 404. The behavior behind the dispatch still throws in the handler.
- In-memory repository one-liners (a LINQ filter or lookup).
- DI wiring (`Add<Feature>` extension methods), `MapGroup` plumbing, csproj and slnx entries.
- Composition-root adapters bridging one slice's port to another slice's repository.
- Aggregate constructors that only assign, and expression-bodied facts that mirror an
  existing aggregate (`NetScore`, `IsReply`).

**Throwing (the user's logic):**

- Every MediatR handler body — `throw new NotImplementedException()`.
- Seed generators (`Sample*` classes) and the `SampleSeed` coordinator in `Podkop.Server`.
- Frontend service methods and **new** store methods — `throw new Error('not implemented')`.
- When an already-green method must change to a new contract (e.g. a store's `load` gaining
  a second parallel request): **leave its body untouched** and rewrite the specs to the new
  contract instead. Red should come from missing behavior, not from injected exceptions that
  break what already worked.

## Red/green discipline

- Every new or rewritten spec must fail; every untouched suite must stay green. Verify the
  exact split before committing and state the counts in the commit message.
- Rewriting existing specs to a changed contract is expected — the previously green tests
  that now flush an extra request or assert new states become part of the red set.
- The running app is allowed to break on throwing seams (a scaffolded seed coordinator can
  take the whole API down until implemented). Keep seed registration **lazy** —
  `Func<IReadOnlyList<T>>` factories resolved on first repository use — so tests that
  override repositories never trigger generation, and flag any runtime breakage prominently
  in the commit message and the handoff summary.

## Backend recipe

- Five projects per slice under `Features/<Feature>/` — Domain, Application, Infrastructure,
  Server, Tests (Contracts only when a cross-feature event ships, per ADR 0003). Copy csproj
  contents from the Findings siblings — package versions are pinned there. Register all five
  in `Podkop.slnx` under `/Features/<Feature>/`; reference the slice's Server and
  Infrastructure projects from `Podkop.Server.csproj`.
- A slice needing a fact from another slice defines a **port in its own Application project**
  (e.g. `IFindingLookup`) and the composition root implements it over the other slice's
  repository. Slices never reference each other's internals (ADR 0003).
- Each slice's Infrastructure owns its sample-data generator; cross-slice seed coherence is
  coordinated by `SampleSeed` in `Podkop.Server`, the only place that sees every slice.
- Tests (xUnit, inside the slice's own Tests project):
  - **Primary seam**: HTTP through `WebApplicationFactory<Program>`, overriding repositories
    in `ConfigureServices` (the test's registration wins; lazy default seeds never run).
  - A Tests project **may** reference another slice's Domain/Infrastructure to seed the
    composition root — production projects never may.
  - Response shapes as records **private to the test class**; deserialization uses the
    System.Text.Json Web defaults (`GetFromJsonAsync` / `JsonSerializerDefaults.Web`).
  - Seed-coherence tests run the default factory with **no overrides** and assert through
    the same HTTP surface the frontend uses.
  - Make orderings falsifiable: choose seed values so every plausible wrong ordering (raw
    counts, age, insertion order) produces a *different* sequence than the specified one.

## Frontend recipe

- Feature folder under `frontend/src/` (or grow an existing one). Standalone components with
  `imports: []` left empty for the user to fill as their template takes shape; TS files keep
  only what the specs need to compile (inputs/outputs, injected dependencies, state shape,
  throwing bodies). One HTTP service per backend slice; the SignalStore's state shape
  extended up front (state keys and DTO types are structure).
- `.html`/`.scss` scaffolds are **empty except for a guidance comment block** — no markup,
  no CSS rules, not even "structurally complete" skeleton markup. The comment describes the
  _what_, never the _how_: the states to cover, the hook classes and exact UI copy the
  colocated specs assert — never the language constructs, framework APIs, or libraries to
  reach for (no `@if`/`@for`, no `patchState`/`switchMap`, no "use Material module X", no
  SCSS technique hints). Guidance may name project seams (services, scaffolded components,
  the store). Choosing the right tool is the user's learning exercise; the failing specs
  define done.
- Shared fixtures live in `<feature>.fixtures.ts`. Data the server orders is rendered as-is
  — the frontend never re-sorts — and fixtures say so in a comment.
- Spec idioms:
  - **Page components**: `RouterTestingHarness` + `provideRouter` with stub components for
    the other routes + `provideHttpClientTesting`; assert on `routeNativeElement`.
  - **Leaf components**: `TestBed.createComponent` + `fixture.componentRef.setInput`.
  - **Stores**: driven directly against `HttpTestingController`.
  - jsdom has no layout: assert geometry-flavored behavior at a DOM seam (e.g. capture
    `Element.prototype.scrollIntoView` calls and inspect target and options) and say so in
    a comment next to the assertion.

## Process checklist

1. Read the ticket, its parent spec, and every ADR either names; confirm blockers are closed.
2. Branch, commit, and hand off for merge per CLAUDE.md's **Git Conventions**.
3. Mirror the canonical templates; write skeletons and specs on both ends.
4. Verify: `dotnet build` and `dotnet test`; `npm run build` and `npx vitest run` from
   `frontend/` (bare vitest needs `frontend/vite.config.ts` — on older branches use
   `npm test -- --watch=false`).
5. Confirm the red/green split is exactly new-red / untouched-green; state the split and any
   runtime breakage in the commit message.
