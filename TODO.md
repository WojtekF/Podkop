# Podkop — TODO

A running backlog of planned work. Not a special Claude file — just a human-readable
list. (For durable project conventions/instructions that Claude auto-loads each
session, use `CLAUDE.md` instead.)

## In progress / planned

- [ ] Finding detail page with comments — spec agreed, see
      [#13](https://github.com/WojtekF/AngularLearning/issues/13)
- [ ] Comment paging/lazy-loading on the finding detail page (`GET .../comments` currently
      specced in #13 to return all threads at once; revisit when comment volumes justify it)
- [ ] Limit finding descriptions to a reasonable maximum length — decide the limit and
      where it's enforced (domain validation vs. UI truncation on the detail page)
- [ ] Remove the `@ngrx/signals` override block in `frontend/package.json` (pins its
      `@angular/core`/`@angular/common` peers to the workspace Angular 22) once NgRx
      ships an Angular 22-compatible release, and bump `@ngrx/signals` to that release.

## Done

- [x] Add **Scalar** API reference UI backed by OpenAPI (`Podkop.Server`)
      - Package: `Scalar.AspNetCore` 2.16.10
      - Wired in `Program.cs`: `app.MapScalarApiReference()` (Development only)
      - UI: `/scalar/v1` · OpenAPI doc: `/openapi/v1.json`

## Notes / follow-ups

- [ ] **Security warning (pre-existing):** `Microsoft.OpenApi` 2.0.0 is pulled
      transitively by `Microsoft.AspNetCore.OpenApi` 10.0.8 and has a known
      high-severity advisory (GHSA-v5pm-xwqc-g5wc / NU1903). Options: pin an
      explicit `<PackageReference Include="Microsoft.OpenApi" Version="2.9.0" />`
      (verify compat with ASP.NET Core 10.0.8), or wait for a patched
      `Microsoft.AspNetCore.OpenApi`.
