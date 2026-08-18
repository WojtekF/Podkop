# Auth options: ASP.NET Core Identity vs Keycloak vs token-based auth

Resolves the wayfinder ticket [Auth options: Identity vs Keycloak vs tokens (#58)](https://github.com/WojtekF/Podkop/issues/58). Research date: 2026-08-18.

Sources consulted (primary): [Microsoft Learn ASP.NET Core security docs](https://learn.microsoft.com/en-us/aspnet/core/security/) (Identity intro, Identity API endpoints, choose-an-identity-solution, account confirmation, antiforgery, claims mapping, CORS, GDPR, integration testing, .NET 10 release notes, Identity model customization), [aspire.dev Keycloak integration](https://aspire.dev/integrations/security/keycloak/), [keycloak.org docs](https://www.keycloak.org/documentation) (server admin, import/export, OIDC layers, javascript adapter, release posts), [angular.dev](https://angular.dev/best-practices/security) (security, dev-server proxy), [OWASP Cheat Sheet Series](https://cheatsheetseries.owasp.org/) (Session Management, HTML5 Security), [IETF OAuth for Browser-Based Apps draft](https://datatracker.ietf.org/doc/draft-ietf-oauth-browser-based-apps/), [duendesoftware.com](https://duendesoftware.com/pricing), [OpenIddict](https://github.com/openiddict/openiddict-core), NuGet package pages, dotnet/aspnetcore source.

## TL;DR

- **ASP.NET Core Identity + EF Core stores + cookie auth** covers everything Podkop needs (registration, PBKDF2 password hashing, lockout, email confirmation, roles) with **zero new infrastructure** in the Aspire loop — the Identity tables land in the existing PostgreSQL, inside the Users slice's DbContext/`users` schema. Microsoft's own guidance: for a single app with only its own client UIs, [use built-in Identity, and prefer cookies over tokens](https://learn.microsoft.com/en-us/aspnet/core/security/how-to-choose-identity-solution?view=aspnetcore-10.0).
- **`MapIdentityApi` is email+password only** — its `/register` body has no username field — so Podkop (usernames are core domain) will hand-roll minimal-API endpoints over `UserManager`/`SignInManager`, using `MapIdentityApi` as a reference implementation, not a dependency.
- **Aspire's Keycloak integration is still in preview at Aspire 13.4** ([`Aspire.Hosting.Keycloak` 13.4.6-preview.1](https://www.nuget.org/packages/Aspire.Hosting.Keycloak), while e.g. `Aspire.Hosting.PostgreSQL` 13.4.6 is stable). Keycloak buys SSO, a hosted login UI, and standards-compliant OIDC — at the cost of a container + realm bootstrap in every dev loop, a second PII store (GDPR), and claims-mapping glue.
- **Bearer tokens in the browser are the weakest option**: Microsoft says the `MapIdentityApi` token scheme exists only for clients that can't use cookies, and OWASP says [never keep tokens in `localStorage`/`sessionStorage`](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html) (any XSS steals them). Self-hosted OIDC (OpenIddict, Apache-2.0; Duende, commercial with a community tier) layers *on top of* Identity, so it stays available later without rework.
- **Lean: Option 1** (Identity + cookies, BFF-style), with the Angular dev server proxying `/api` to the backend so cookies and Angular's XSRF support work unchanged in dev. Input for the "Auth spec" grilling session, not a final decision.

## 1. ASP.NET Core Identity with EF Core stores + cookie auth (BFF-style)

### What Identity provides out of the box

[ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0) is the framework's built-in membership system: it "manages users, passwords, profile data, roles, claims, tokens, email confirmation, and more", with external logins (Google etc.), MFA, and account lockout available on top. Concretely relevant to Podkop:

- **Password hashing** — PBKDF2 with HMAC-SHA512, 128-bit salt, 256-bit subkey, 100 000 iterations by default (the V3 format in [`PasswordHasher.cs`](https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Core/src/PasswordHasher.cs); iteration count configurable via `PasswordHasherOptions`). Nothing to design or audit yourself.
- **Lockout** — `IdentityOptions.Lockout` (`MaxFailedAccessAttempts`, `DefaultLockoutTimeSpan`), enforced by `SignInManager` when `lockoutOnFailure: true` ([Identity intro](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)).
- **Email confirmation** — `GenerateEmailConfirmationTokenAsync` produces a data-protection token (default lifespan one day), `SignIn.RequireConfirmedEmail` blocks login until confirmed; delivery goes through an `IEmailSender` you register ([account confirmation doc](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm?view=aspnetcore-10.0)). Without one, the registered default is [`NoOpEmailSender`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.ui.services.noopemailsender?view=aspnetcore-8.0), which does nothing — see section 5.
- **Roles and claims** — a role store (`AspNetRoles`/`AspNetUserRoles`) plus per-user and per-role claims; roles surface as role claims on the signed-in `ClaimsPrincipal` — see section 6.
- **.NET 10 additions** — [passkey (WebAuthn/FIDO2) support in Identity](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0), Identity/authentication metrics, and one change that matters for SPAs: unauthenticated requests to *known API endpoints* under cookie auth now return **401/403 instead of redirecting to a login page** (via `IApiEndpointMetadata`, applied automatically to JSON-reading/writing minimal APIs). The classic "API redirects to a login HTML page" annoyance of cookie auth is gone by default.

### `MapIdentityApi` vs hand-rolled endpoints

[`AddIdentityApiEndpoints<TUser>()` + `MapIdentityApi<TUser>()`](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0) give a ready-made JSON API: `POST /register`, `POST /login`, `POST /refresh`, `GET /confirmEmail`, `POST /resendConfirmationEmail`, `POST /forgotPassword`, `POST /resetPassword`, `POST /manage/2fa`, `GET|POST /manage/info`. `/login?useCookies=true` issues the standard Identity application cookie; without it, the proprietary bearer token (section 3).

Limits that matter for Podkop:

- The `/register` request body is **`email` + `password` only** — there is no username field ([endpoint reference](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)). Podkop users have usernames (`ada_lovelace`); Wykop identity is username-first. Bending `MapIdentityApi` around that is not supported — the endpoints are not extensible in shape.
- There is **no `/logout` endpoint** in the mapped list — a cookie sign-out endpoint calling `SignInManager.SignOutAsync` is hand-rolled either way.
- Error contract, routes, and DTOs are fixed; they won't match the slice conventions.

Cookie-session mechanics worth knowing up front: the application cookie defaults to a **14-day sliding inactivity window**, adjustable via `ConfigureApplicationCookie(o => o.ExpireTimeSpan = ...)` ([account confirmation doc](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm?view=aspnetcore-10.0)), and cookie sessions are revalidated against the user's **security stamp** on an interval (`SecurityStampValidatorOptions.ValidationInterval`), which is how password changes and lockouts invalidate live sessions ([Identity API doc](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)).

The pragmatic reading: `MapIdentityApi` is a fast prototype surface and a **reference implementation**, but a username-based product ends up with hand-rolled minimal-API endpoints in a `Features/Auth` (or the Users) slice calling the same primitives the built-in endpoints use: `UserManager.CreateAsync`, `GenerateEmailConfirmationTokenAsync`/`ConfirmEmailAsync`, `SignInManager.PasswordSignInAsync` (which enforces lockout and confirmed-email), `SignOutAsync`. All the hard parts (hashing, tokens, lockout) stay in the framework; only the HTTP shapes are yours.

### What "BFF-style" concretely means here

The IETF's [OAuth 2.0 for Browser-Based Apps](https://datatracker.ietf.org/doc/draft-ietf-oauth-browser-based-apps/) (Best Current Practice; in the RFC Editor queue since December 2025) defines the Backend-For-Frontend pattern: a backend component handles all OAuth/credential concerns and holds tokens server-side, while the browser gets only an HttpOnly session cookie referencing server-side state. For Podkop the pattern degenerates pleasantly because **the API host is already the only backend**: there is no separate token server and no token to hold. "BFF-style" here means exactly:

1. The Angular SPA never sees, stores, or attaches a credential; it just calls the API.
2. `Podkop.Server` authenticates via Identity's cookie (`HttpOnly`, `Secure`, `SameSite`), so the session is invisible to JavaScript.
3. Because auth is a cookie, state-changing endpoints get CSRF protection (antiforgery + SameSite, section 4).

This matches Microsoft's stance for apps whose only clients are their own UIs ([choose an identity solution](https://learn.microsoft.com/en-us/aspnet/core/security/how-to-choose-identity-solution?view=aspnetcore-10.0)): SPA clients such as Angular count as part of the same app rather than third parties, and cookies are preferred over tokens "for both security and simplicity".

### Fit with ADR 0010 persistence (schema-per-slice)

Identity's EF store is a set of entity types (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`) configured by `IdentityDbContext` ([Identity model customization](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0)). Everything about the mapping is overridable in `OnModelCreating`: table names via `ToTable`, the key type (e.g. `Guid`), custom user types (`ApplicationUser : IdentityUser`), and — the piece that matters for schema-per-slice — `modelBuilder.HasDefaultSchema("users")`. So the natural landing spot is:

- `Podkop.Users.Infrastructure`'s existing DbContext derives from `IdentityDbContext<PodkopUser, ...>` (or a second, auth-only DbContext in the same slice sharing the `users` schema), with `HasDefaultSchema("users")`.
- Migrations flow through the already-decided migrations worker; integration tests through the already-decided Testcontainers PostgreSQL.
- The slice's domain `User` (UserName + `UserRole`) can either *be* the Identity user (custom `IdentityUser` subtype) or stay a separate read model keyed by the Identity user id — a modeling call for the spec session.

Inference: `AddIdentityCore<TUser>()` + `AddEntityFrameworkStores` + `AddIdentityCookies` (the pieces [`AddDefaultIdentity` composes](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0)) is the right registration for an API-only host — it skips the Razor default UI entirely.

**GDPR:** all PII (email, password hash, tokens) lives in Podkop's own PostgreSQL, in one schema. The template-era pattern of personal-data download/delete pages shows the intended erasure surface ([GDPR doc](https://learn.microsoft.com/en-us/aspnet/core/security/gdpr?view=aspnetcore-10.0)); Podkop's erasure flows (ADR 0007: erasure keeps content and votes) extend naturally, with `AspNetUserTokens` rows cascading on user delete.

Minimal shape (adapted from the [Identity API doc](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0) and [customize-identity-model](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0)):

```csharp
// Podkop.Users.Infrastructure
builder.Services.AddIdentityCore<PodkopUser>(o =>
    {
        o.SignIn.RequireConfirmedEmail = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<UsersDbContext>()   // HasDefaultSchema("users")
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
```

## 2. Keycloak via Aspire's hosting integration (OIDC)

### What the Aspire integration provides today (verified 2026-08-18)

- **Hosting**: [`Aspire.Hosting.Keycloak`](https://www.nuget.org/packages/Aspire.Hosting.Keycloak), latest **13.4.6-preview.1.26319.6** (published 2026-06-19). **Still preview** — the [aspire.dev page](https://aspire.dev/integrations/security/keycloak/) carries a "Preview" badge — while sibling integrations like [`Aspire.Hosting.PostgreSQL` 13.4.6](https://www.nuget.org/packages/Aspire.Hosting.PostgreSQL) are stable in the same Aspire 13.4 release train.
- **Client**: [`Aspire.Keycloak.Authentication`](https://www.nuget.org/packages/Aspire.Keycloak.Authentication) (same preview versioning) wires `AddKeycloakJwtBearer(serviceName, realm, ...)` and `AddKeycloakOpenIdConnect(...)` handlers with service discovery resolving the authority. Caveat from the docs: JWT bearer's default `RequireHttpsMetadata = true` clashes with Aspire's `https+http://` service-discovery scheme, so production needs an explicit Authority URL.
- `AddKeycloak("keycloak", 8080)` runs the `quay.io/keycloak/keycloak` container; the docs recommend a **stable port** in dev "to avoid issues with browser cookies", generate the admin password into AppHost user secrets, and offer `WithDataVolume()` for persistence.
- Keycloak itself is current and actively released: the 26.x line is the shipping series ([26.4 in October 2025](https://www.keycloak.org/2025/10/keycloak-2641-released), [26.6.4 in June 2026](https://www.keycloak.org/2026/06/keycloak-2664-released)); pin the container tag in the AppHost rather than tracking `latest`.

```csharp
// Podkop.Server — API-side validation of Keycloak-issued JWTs
// (adapted from https://aspire.dev/integrations/security/keycloak/)
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName: "keycloak",
        realm: "podkop",
        options => options.Audience = "podkop.api");
```

```csharp
// AppHost (adapted from https://aspire.dev/integrations/security/keycloak/)
var keycloak = builder.AddKeycloak("keycloak", 8080)
                      .WithDataVolume()
                      .WithRealmImport("./Realms");
builder.AddProject<Projects.Podkop_Server>("server").WithReference(keycloak);
```

### Dev repeatability: realm import/export

`WithRealmImport("./Realms")` copies realm JSON into `/opt/keycloak/data/import`, which Keycloak's [`--import-realm` startup option](https://www.keycloak.org/server/importExport) consumes; realms that already exist are skipped, so a data volume plus checked-in realm JSON gives a reproducible dev realm (clients, roles, settings). The aspire.dev docs flag realm import as **dev-only** (unsupported by `aspire publish`/`aspire deploy`). Full `kc.sh export`/`import` exists for round-tripping including users; admin-console partial export masks secrets and excludes users.

### OIDC flow for the Angular SPA

Keycloak's [securing-apps guidance](https://www.keycloak.org/securing-apps/oidc-layers) supports the standard grants; the Authorization Code flow is the recommended one for web apps, and the implicit flow is deprecated ("SHOULD NOT be used" per RFC 9700, removed in OAuth 2.1). The first-party SPA client is [`keycloak-js`](https://www.keycloak.org/securing-apps/javascript-adapter): the client is registered **public** (no secret in a browser), uses the Authorization Code flow with PKCE (`pkceMethod` with S256 as the default method), silent SSO via hidden iframe, and `updateToken()` refresh. The Angular app redirects to Keycloak's hosted login/registration pages and comes back with tokens — Podkop's login UI would be Keycloak's themed pages, not Angular components. The API then validates Keycloak-issued JWTs via `AddKeycloakJwtBearer`.

Inference: a cookie/BFF variant is also possible (server-side `AddKeycloakOpenIdConnect` + cookie to the SPA), which re-introduces everything from section 4 while still paying Keycloak's infrastructure cost — usually chosen only when Keycloak is wanted for SSO but tokens-in-browser are not acceptable.

### Registration, email verification, roles in Keycloak

Per the [Server Administration Guide](https://www.keycloak.org/docs/latest/server_admin/index.html): registration is a per-realm toggle (Realm Settings → Login → *User registration*), *Verify email* sends a verification link on registration, and SMTP is configured per realm (Realm Settings → Email; `Host` and `From` are required before any mail flows). Roles are **realm roles** (realm-wide) or **client roles** (per application), with composites and realm default roles; role mappings are encoded into tokens.

### How roles arrive as claims in ASP.NET Core

Keycloak puts realm roles in the access token under `realm_access.roles` and client roles under `resource_access.{clientId}.roles` ([Red Hat build of Keycloak server admin guide, ch. 7](https://docs.redhat.com/en/documentation/red_hat_build_of_keycloak/22.0/html/server_administration_guide/assigning_permissions_using_roles_and_groups) — Red Hat's build is the productized Keycloak documentation). Those are *nested JSON* claims, which ASP.NET Core's role checks don't read natively. Two standard fixes, both glue you own:

1. Add a Keycloak protocol mapper emitting roles as a flat multi-valued claim (e.g. `roles`), then set `TokenValidationParameters.RoleClaimType = "roles"` ([claims mapping doc](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/claims?view=aspnetcore-10.0)).
2. Or register an [`IClaimsTransformation`](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/claims?view=aspnetcore-10.0) that parses `realm_access` and adds `ClaimTypes.Role`/custom role claims after authentication.

### Coexistence with the Users slice + GDPR

Keycloak owns its users in **its own database** (the container's storage), so account PII (email, password hash) lives outside Podkop's PostgreSQL while the Users slice keeps its own `users` rows for domain data. That means: user provisioning glue (create/update the slice's row on first authenticated request or via Keycloak events), a **role source-of-truth question** (Keycloak realm roles vs the slice's `UserRole` column), and **two GDPR surfaces** — erasure flows must delete the Keycloak account *and* run Podkop's own erasure (ADR 0007).

## 3. Token-based variants

### `MapIdentityApi`'s bearer scheme

With `useCookies=false`, `/login` returns `{ tokenType, accessToken, expiresIn, refreshToken }`, and `/refresh` rotates. Microsoft is unusually direct about scope ([Identity API doc](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)): the tokens aren't standard JWTs but a deliberately proprietary format, and the token option is "not intended to be a full-featured identity service provider or token server" — it exists for clients that *can't* use cookies (mobile/desktop). The same page recommends cookies for browser apps because the browser handles them automatically without ever exposing them to JavaScript, and the [decision doc](https://learn.microsoft.com/en-us/aspnet/core/security/how-to-choose-identity-solution?view=aspnetcore-10.0) adds that the Identity-issued token isn't suitable for third-party API access. For an Angular SPA this scheme is explicitly the *not-recommended* branch of Microsoft's own fork.

### Classic self-issued JWTs

Hand-rolling `AddJwtBearer` plus your own `/login` that signs JWTs keeps Identity's user store but makes you the token authority: signing-key management and rotation, refresh-token storage and revocation, and audience/lifetime policy are all bespoke, with no standard to lean on — precisely the "service that must be installed, configured, and maintained" cost Microsoft describes for token issuance ([choose an identity solution](https://learn.microsoft.com/en-us/aspnet/core/security/how-to-choose-identity-solution?view=aspnetcore-10.0)), minus the battle-tested implementation. It also inherits the browser-storage problem below. No advantage over Option 1 for this app shape.

### Self-hosted OIDC servers (the middle ground)

If Podkop ever needs standards-based tokens (mobile app, third-party API consumers, SSO across services), the .NET route is an OIDC server **layered on the same Identity user store**:

- [OpenIddict](https://github.com/openiddict/openiddict-core) — Apache 2.0, free; OIDC client/server/validation for .NET with EF Core stores (currently v7). Library-first by design: you implement the endpoints/UI, typically over ASP.NET Core Identity ([docs](https://documentation.openiddict.com/)).
- [Duende IdentityServer](https://duendesoftware.com/products/identityserver) — the commercial standard. Free for dev/test; production needs a license: [Community Edition](https://duendesoftware.com/products/communityedition) is free including production for for-profits under \$1M projected annual revenue and under \$3M capital (feature-equivalent to Standard); paid tiers currently [Lite \$5,750 / Standard \$12,500 / Advanced \$24,900 per year](https://duendesoftware.com/pricing). Podkop would qualify for Community today, but it's an eligibility-reviewed license, not open source.

Key point for the recommendation: choosing Identity + cookies now **does not foreclose** this path — OpenIddict/Duende bolt onto the same `AspNetUsers` store later.

### Where the SPA stores tokens, and the XSS exposure

Any tokens-in-browser variant must park the access/refresh tokens somewhere JavaScript can reach:

- OWASP [Session Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html): do not store authentication tokens, session IDs, JWTs, or refresh tokens in `localStorage`/`sessionStorage` — those APIs are readable by any script in the origin, so **a single XSS discloses every token**; `HttpOnly` cookies are the mitigation, with `SameSite` as defense-in-depth against CSRF (not a replacement for CSRF tokens).
- OWASP [HTML5 Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/HTML5_Security_Cheat_Sheet.html): don't put session identifiers in local storage; cookies mitigate via `httpOnly`.
- Microsoft ([Identity API doc](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0)) recommends cookies for browser apps for exactly this reason; the IETF [browser-based apps BCP](https://datatracker.ietf.org/doc/draft-ietf-oauth-browser-based-apps/) ranks BFF (no tokens in the browser at all) as the strongest architecture.

Memory-only token storage (keycloak-js's default posture) avoids persistent storage but still exposes tokens to any successfully injected script and costs re-authentication on refresh (silent SSO iframes mitigate). For a content site full of user-generated markup — Podkop's whole product — minimizing what XSS can steal is worth real weight.

## 4. Session model for the SPA: cookies vs tokens, CSRF, and the Aspire dev topology

### CSRF with cookies, and what ASP.NET Core gives you

CSRF is a cookie-auth problem: browsers attach cookies to any request to the domain, however triggered ([antiforgery doc](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)). Mitigations in ASP.NET Core:

- `AddAntiforgery()` + `UseAntiforgery()` middleware. **Nuance for minimal APIs:** automatic validation applies to endpoints binding *form data*; plain JSON endpoints are not auto-validated, so a JSON API validates explicitly (e.g. an endpoint filter calling [`IAntiforgery.ValidateRequestAsync`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.antiforgery.iantiforgery)) on mutating routes.
- The doc's SPA pattern is exactly Angular-shaped: expose a `GET /antiforgery/token` endpoint that calls `GetAndStoreTokens` and drops the request token into a **non-HttpOnly `XSRF-TOKEN` cookie**, and set `AntiforgeryOptions.HeaderName = "X-XSRF-TOKEN"`; the doc itself notes Angular's built-in XSRF support reads a cookie named `XSRF-TOKEN`.

```csharp
// Adapted from the antiforgery doc's minimal-API sample, renamed to Angular's defaults
builder.Services.AddAntiforgery(o => o.HeaderName = "X-XSRF-TOKEN");

app.MapGet("antiforgery/token", (IAntiforgery af, HttpContext ctx) =>
{
    var tokens = af.GetAndStoreTokens(ctx);
    ctx.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!,
        new CookieOptions { HttpOnly = false });   // must be readable by Angular
    return Results.Ok();
}).RequireAuthorization();
```
- `SameSite=Lax/Strict` on the auth cookie as defense-in-depth ([OWASP Session Management](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html)). Also relevant: JSON `Content-Type` requests trigger CORS preflight cross-origin, and .NET 10's 401-for-APIs change (section 1) removes login-redirect noise.

### Angular's built-in XSRF support (verified at angular.dev)

Per [angular.dev security guidance](https://angular.dev/best-practices/security): `HttpClient`'s XSRF interceptor reads the `XSRF-TOKEN` cookie and sets an `X-XSRF-TOKEN` header **on mutating requests (e.g. POST) to relative and same-origin URLs only — not on GET/HEAD**, and the backend must both set the cookie and verify the header. Names are configurable via `withXsrfConfiguration({cookieName, headerName})`. The URL scope is the load-bearing fact: calls to an **absolute URL on a different origin get no XSRF header**.

### The Aspire dev-topology wrinkle

Under Aspire, Angular's dev server (localhost:4200) and `Podkop.Server` (localhost:5381/7460) are **different origins**. Two setups:

**A. Dev-server proxy (recommended).** Angular's [dev-server proxy](https://angular.dev/tools/cli/serve) forwards `/api/**` to the backend, so the SPA calls *relative* URLs: same-origin, cookies are first-party, Angular's XSRF interceptor fires, and no CORS exists at all. Aspire's own [Angular sample](https://aspire.dev/reference/samples/aspire-with-javascript/) wires the proxy target from the service-discovery environment variables Aspire injects (`services__{name}__{scheme}__{index}`):

```js
// proxy.conf.js — adapted from the aspire.dev "Integrating Angular, React, and Vue" sample
module.exports = {
  "/api": {
    target: process.env["services__server__https__0"] ??
            process.env["services__server__http__0"],
    secure: process.env["NODE_ENV"] !== "development",
    pathRewrite: { "^/api": "" },
  },
};
```

**B. CORS with credentials.** Keep the SPA calling `https://localhost:7460` directly: the API needs `WithOrigins("http://localhost:4200").AllowCredentials()` (combining `AllowAnyOrigin` with `AllowCredentials` is rejected as insecure), the client needs `withCredentials: true`, and Microsoft [warns allowing cross-origin credentials is itself a risk](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-10.0). Angular's XSRF interceptor won't fire for those absolute URLs, so you'd hand-write the header interceptor. Inference: cookies are host-scoped, not port-scoped, so the cookie itself does flow between localhost ports — but the extra CORS + interceptor machinery makes this strictly worse than the proxy for dev.

### Production topology

- **SPA served by the API host** (copy the Angular build into `Podkop.Server`'s static assets with a fallback route): one origin, relative URLs, cookies and Angular XSRF work identically to the dev proxy setup; no CORS. Inference: this is the shape that keeps dev and prod behavior aligned and is the natural default for Podkop.
- **SPA hosted separately** (CDN/static host on another domain): CORS-with-credentials permanently, `SameSite=None; Secure` cookies, custom XSRF wiring — the cookie model's costs concentrate here. If a separate-domain frontend ever becomes a requirement, that would be a genuine argument to revisit tokens/BFF-with-proxy.

## 5. Registration and email verification

- **Identity:** `/register` (or the hand-rolled equivalent) triggers a confirmation email through [`IEmailSender`](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm?view=aspnetcore-10.0); `RequireConfirmedEmail = true` blocks login until `ConfirmEmailAsync` succeeds. **Dev story:** the default [`NoOpEmailSender`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.ui.services.noopemailsender?view=aspnetcore-8.0) does nothing (the Razor default UI uses its presence to show a "click to confirm" link instead of sending). An API-only host has no such page, so dev needs a trivial `IEmailSender` that logs the confirmation link, or a dev mailbox container (Mailpit/Papercut) in the AppHost (inference — standard practice, no first-party Aspire mail integration found). For production Microsoft recommends a transactional mail service (SendGrid et al.) over raw SMTP.
- **Keycloak:** registration and *Verify email* are realm toggles; SMTP is realm configuration ([server admin guide](https://www.keycloak.org/docs/latest/server_admin/index.html)); the whole flow (forms, emails, resend, expiry) is Keycloak's hosted UI. Dev needs the SMTP settings in the imported realm JSON pointing at a dev mailbox container (inference, as above). Customizing look/fields means Keycloak themes and its declarative user profile, not Angular.
- **Token variants:** identical to Identity — the same `UserManager` confirmation-token machinery underneath; an OIDC layer (OpenIddict) adds nothing here by itself.

## 6. Role modeling (Member / Moderator, Administrator later)

Current state in the repo: `Podkop.Users.Domain.UserRole` is `{ Member, Moderator }` (`Features/Users/Podkop.Users.Domain/UserRole.cs`), each slice defines its own `ICurrentUser` port (currently just `UserName`, e.g. `Features/Users/Podkop.Users.Application/ICurrentUser.cs`), and `Podkop.Server/StubCurrentUser.cs` satisfies them all with a hardcoded user — its doc comment says real auth "replaces exactly this seam".

- **Identity:** either (a) use the role store — `AddRoles<IdentityRole>()`, seed `Member`/`Moderator` rows, roles become role claims in the cookie principal, `RequireAuthorization`/`RequireRole("Moderator")` on endpoints; `Administrator` later is one new row + policies — or (b) skip Identity roles and keep the Users slice's `Role` column as the single source of truth, projecting it into a role claim at sign-in via the claims principal factory. (b) avoids storing the same fact twice (`AspNetUserRoles` *and* `users.Role`); (a) is more conventional. Either way, each slice's `ICurrentUser` becomes a thin `HttpContext.User` adapter in the composition root: `UserName` from the name claim, role from the role claim — no slice touches Identity types, preserving ADR 0003 boundaries.
- **Keycloak:** define `member`/`moderator` as **realm roles** in the imported realm; they arrive as `realm_access.roles` and get flattened via a protocol mapper or `IClaimsTransformation` + `RoleClaimType` (section 2). The `ICurrentUser` adapter is identical from the slices' perspective. The cost is the duplicated source of truth: promotion to Moderator (a Podkop moderation feature) would have to call the Keycloak Admin API rather than update a row the slice owns.
- **Tokens:** same as Identity — role claims embedded in whatever the token is; the adapter reads the principal either way.

## 7. Operational cost in development

| | Identity + cookies | Keycloak (Aspire) | MapIdentityApi bearer / self-JWT |
|---|---|---|---|
| New Aspire resources | none (tables in existing PostgreSQL) | Keycloak container + realm JSON bootstrap + data volume; stable-port constraint | none |
| Config/secrets | none new now; mail API key later | admin credentials (AppHost secrets), realm export discipline, authority URLs, HTTPS-metadata wrinkle | signing-key management (self-JWT) |
| Startup/feedback loop | unchanged | container pull/start + realm import on cold start | unchanged |
| Integration tests | `WebApplicationFactory` + Testcontainers PostgreSQL (already decided): either POST the real login endpoint and reuse the cookie, or swap in a [test auth handler](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) | fake the JWT handler with the same [test-scheme pattern](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) for most tests; [Testcontainers.Keycloak 4.14.0](https://www.nuget.org/packages/Testcontainers.Keycloak) exists for true end-to-end | set `Authorization: Bearer` after calling `/login`; simplest of all to script |
| Claims/roles glue | none (framework-native) | protocol mapper or claims transformation | none |
| GDPR / PII surface | one store (own PostgreSQL, `users` schema) | two stores (Keycloak DB + app DB), erasure spans both | one store |

Microsoft's [documented test pattern](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) — a `TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>` registered via `ConfigureTestServices` as a "TestScheme" — works identically against all three stacks, so per-slice endpoint tests never need real credentials regardless of choice; the auth slice's own tests exercise the real flow.

## Recommendation for Podkop

**Lean: Option 1 — ASP.NET Core Identity with EF Core stores in the Users slice, cookie authentication, BFF-style, with hand-rolled minimal-API auth endpoints (username-first) and the Angular dev-server proxy making the SPA same-origin.** Framed explicitly as input for the "Auth spec" grilling session to accept or overturn.

Why this fits Podkop specifically:

1. **It matches Microsoft's own decision tree for this exact shape** — one app, whose only client is its own SPA, no SSO, no third-party API consumers: built-in Identity, cookies over tokens ([choose an identity solution](https://learn.microsoft.com/en-us/aspnet/core/security/how-to-choose-identity-solution?view=aspnetcore-10.0)).
2. **Zero new infrastructure** in an Aspire loop that just gained PostgreSQL (ADR 0010): Identity is tables in the `users` schema, migrated by the existing worker, tested by the existing Testcontainers setup. Keycloak would add a preview-status integration, a container, realm bootstrap, and claims glue to every `dotnet run`.
3. **Security posture for a UGC site**: no token ever reaches JavaScript (OWASP/IETF-preferred), and .NET 10's cookie/API improvements remove the old friction. CSRF is handled with documented, Angular-native machinery (antiforgery + `XSRF-TOKEN`).
4. **GDPR stays one-surface**: all account PII in Podkop's own database, so ADR 0007's erasure flows extend without a second system of record.
5. **It's not a dead end**: passkeys (new in .NET 10) and external logins are Identity features; and if the roadmap later adds a mobile app, third-party API access, or SSO, OpenIddict (Apache-2.0) layers an OIDC server onto the *same* user store — the Keycloak/token benefits become reachable without discarding this work.

The honest fork: **if** the spec session concludes that multi-client SSO or "we never want to own login screens and password flows" is a near-term certainty, Keycloak is the better long-term citizen — standards-compliant OIDC, hosted registration/verification UI, admin console — and its costs (preview Aspire integration, dev-loop weight, split PII, role-sync glue for Podkop's own promotion flows) are the price of that future. Nothing in today's Podkop (single SPA, username-centric domain, moderation features that mutate roles, GDPR erasure in-app) pays for that price yet. The `MapIdentityApi` bearer scheme and hand-signed JWTs are recommended against outright for the browser client, on Microsoft's and OWASP's own guidance.

Open questions to grill in the Auth spec session: username-vs-email as login identifier (and uniqueness/normalization rules); whether the domain `User` *is* the Identity user or a projection of it; role source of truth (Identity role store vs the slice's `Role` column) given Moderator promotion is a product feature; prod topology (SPA served by `Podkop.Server`?); email provider and the dev mailbox; cookie lifetime/sliding policy; 2FA/passkey scope for v1.
