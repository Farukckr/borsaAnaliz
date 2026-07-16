# Plan

## Status

in-progress — 2026-07-16 (deploy task rev 6 — Phase 3 local work complete; GitHub remote/push awaiting user)

## Task Type

Deploy / infrastructure (SQLite → Supabase Postgres migration + Render.com hosting).

## User Request

Turkish, summarized: Publish the finished BorsaAnaliz app publicly. User first asked for Vercel; Vercel cannot host ASP.NET Core, so Render.com (free tier, Docker) was chosen with the user. Database moves to Supabase Postgres so user accounts/portfolios survive redeploys.

## Goal

The app runs publicly at a `*.onrender.com` URL with data in Supabase Postgres; registering, portfolios, charts, and AI commentary all work in production.

## Current State

- App is complete and working locally (see Prior Task): ASP.NET Core 8 MVC in `src/BorsaAnaliz.Web`, EF Core + **SQLite** (`app.db`), Identity auth, Yahoo market data, lightweight-charts + TradingView widgets, Gemini AI commentary (key in user-secrets, model `gemini-3.5-flash`).
- Migrations in `src/BorsaAnaliz.Web/Data/Migrations/` are **SQLite-flavored** (Identity schema + portfolios). They will not run on Postgres as-is.
- `git` is NOT installed; the folder is not a git repository. Render deploys from a GitHub repo, so git + GitHub are required steps.
- Docker is likely NOT installed locally — do not assume `docker` works; Render builds the image from the Dockerfile itself, so local docker verification is optional.
- winget available (installed the .NET SDK earlier; can install Git).
- Prerequisites the USER/planner must provide (Codex must not invent them):
  - Supabase connection string in user-secrets `ConnectionStrings:DefaultConnection` — Supabase "Session pooler" form, e.g. `Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<pw>;Ssl Mode=Require;Trust Server Certificate=true`. If absent when Phase 1 starts, report blocked asking for it.
  - GitHub account (Phase 3 pushes there; may end "partial — waiting on user" if no `gh` auth).
  - Render account (user creates at render.com, free, GitHub login) — Phase 4 is mostly dashboard instructions.

## Architecture Decisions (made by planner)

- **One DB provider everywhere**: switch fully to Npgsql/Postgres for dev AND prod (no dual SQLite/Postgres providers — two migration sets are not worth it). Local dev also points at Supabase.
- **Fresh migrations**: delete the SQLite migrations and snapshot; generate one new initial migration against Npgsql. Local `app.db` data is disposable test data — no data migration needed.
- **Connection string resolution**: `ConnectionStrings:DefaultConnection` from configuration; in production the Render env var `ConnectionStrings__DefaultConnection` overrides. Local dev keeps it in user-secrets — never in appsettings.json.
- **Container**: multi-stage Dockerfile (`mcr.microsoft.com/dotnet/sdk:8.0` build → `mcr.microsoft.com/dotnet/aspnet:8.0` run). Render injects `PORT`; app must bind `http://0.0.0.0:$PORT` (shell-form ENTRYPOINT setting `ASPNETCORE_URLS`, default 8080).
- **Behind Render's TLS proxy**: add ForwardedHeaders middleware (XForwardedProto/For, clear KnownNetworks/Proxies) so Identity cookies and redirects see https; skip `UseHttpsRedirection` in production (Render already forces https externally).
- **Data Protection keys**: persist to the database (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`) so login cookies survive redeploys/restarts on the free tier.
- **Secrets in prod**: Render env vars only — `ConnectionStrings__DefaultConnection`, `Ai__ApiKey`, `ASPNETCORE_ENVIRONMENT=Production`. Verify appsettings*.json contain no secrets before the first git commit.

## Proposed Changes

One phase per Codex run; each leaves the app building and runnable.

### Phase 1 — Postgres/Supabase migration

- [x] Preflight: confirm `ConnectionStrings:DefaultConnection` exists in user-secrets and reaches Supabase. If missing → blocked.
- [x] Swap `Microsoft.EntityFrameworkCore.Sqlite` → `Npgsql.EntityFrameworkCore.PostgreSQL` in csproj; `UseSqlite` → `UseNpgsql` in `Program.cs`.
- [x] Delete `Data/Migrations/*`; run `dotnet ef migrations add InitialPostgres`; startup `Database.Migrate()` stays.
- [x] Npgsql wants UTC for `timestamptz`: if `DateTimeOffset`/`DateTime` properties error at runtime, normalize to UTC — verify trade + portfolio pages specifically.
- [x] Optional if trivial: reverted the SQLite in-memory `DateTimeOffset` ordering workaround in `PortfolioService` to server-side ordering.
- [x] Remove `app.db*` files; ensure `.gitignore` covers `*.db*`.
- [x] Verify: `dotnet build` clean; app starts and migrates against Supabase; register + login + create portfolio + one buy + partial sell works; `AspNetUsers`/`Portfolios`/`Transactions` tables created and exercised in Supabase.

### Phase 2 — Production readiness + Dockerfile

- [x] `Dockerfile` at repo root (multi-stage publish of `src/BorsaAnaliz.Web`; bind `0.0.0.0:${PORT:-8080}`) + `.dockerignore` (bin, obj, .git, .agents, *.db*).
- [x] `Program.cs`: ForwardedHeaders middleware; make `UseHttpsRedirection` conditional (skip in Production).
- [x] Persist Data Protection keys via `PersistKeysToDbContext<ApplicationDbContext>` (add package + the `DataProtectionKeys` DbSet + migration).
- [x] RLS: all existing tables have Row Level Security enabled (2026-07-16, blocks Supabase's public Data API; the EF connection is table owner and unaffected). The new `DataProtectionKeys` migration executes `ALTER TABLE public."DataProtectionKeys" ENABLE ROW LEVEL SECURITY;`.
- [x] Confirm no secrets in any appsettings file.
- [x] Verify: `dotnet build` and Release publish are clean; app runs locally against Supabase in Production mode. Docker is unavailable locally, so Render will perform the image build.

### Phase 3 — Git + GitHub

- [x] Install git: `winget install Git.Git --silent --accept-package-agreements --accept-source-agreements`; verified Git 2.55.0.windows.3 after refreshing PATH as a fresh shell would.
- [x] Before first commit: grep the tree for the Gemini key pattern and connection-string fragments — staged index contains no key pattern or real connection credentials.
- [x] `git init`, initial commit of the project (keeping `.agents/` is fine — it contains no secrets).
- [ ] Push to GitHub: GitHub connector has no accessible repository installation and `gh` is unavailable, so remote creation/push awaits the user steps in the report.
- [x] Verify: local `git log`/clean status are verified after commit; remote verification remains pending until push.

### Phase 4 — Render service (user dashboard steps, documented precisely)

- [ ] Write `docs/DEPLOY.md` (Turkish): New → Web Service → connect GitHub repo → Runtime: Docker → Region: Frankfurt → Instance: Free → env vars `ConnectionStrings__DefaultConnection`, `Ai__ApiKey`, `ASPNETCORE_ENVIRONMENT=Production` (placeholders only, no real values). Note: free instance sleeps after 15 min idle (first request ~30 s); user should ROTATE the Gemini key before going public (old one appeared in chat).
- [ ] Optional `render.yaml` blueprint at repo root (type web, env docker, plan free, secret envVars `sync: false`).
- [ ] Verify (user-assisted): after the user reports the service deployed, HTTP-check the public URL (`/`, `/Stocks`, register/login, AI endpoint behavior) and record results.

## Acceptance Criteria

- App data lives in Supabase Postgres; no SQLite package or `.db` file remains.
- `dotnet build` clean; local run against Supabase works end-to-end (auth, portfolio trade, charts, AI).
- Repo is a git repository pushed to GitHub with no secrets anywhere in history.
- Render builds from the Dockerfile and serves the app publicly over https; logins survive a redeploy.
- Secrets exist only in user-secrets (local) and Render env vars (prod).

## Verification

- `dotnet build BorsaAnaliz.sln`; `dotnet run --project src/BorsaAnaliz.Web` against Supabase.
- Supabase dashboard/SQL editor: tables exist, rows appear after register/trade.
- `git log`, `git remote -v`; repo visible on GitHub.
- After deploy: HTTP checks on the public `onrender.com` URL.

## Assumptions

- Free tiers (Render Free, Supabase Free, Gemini Free) are acceptable, including Render's cold-start sleep.
- Local `app.db` data is disposable; no data migration.
- The user performs Render dashboard clicks guided by `docs/DEPLOY.md`; Codex cannot operate the dashboard.
- EU region (Frankfurt / eu-central) for both Render and Supabase.

## Open Questions

- None blocking, provided the Supabase connection string is in user-secrets before Phase 1 (planner/user will create the Supabase project and set it).

## Risks

- Supabase free tier pauses projects after ~1 week of inactivity — user must open the dashboard occasionally.
- Render free tier sleeps; first request after idle ~30 s — cosmetic, documented.
- Npgsql UTC/`timestamptz` strictness can surface at runtime, not build time — the trade flow must be exercised in Phase 1 verification.
- Secret leakage into git history is effectively permanent — check BEFORE the first commit.
- Session-pooler (not transaction-pooler port 6543) must be used: EF migrations need session semantics.

## Rollback / Recovery

- Phase 1: revert csproj/`Program.cs` edits and restore the old migration files (Codex lists exact files in its report). From Phase 3 onward, git history is the rollback mechanism.
- Render: delete the service to stop publishing; Supabase project can be paused/deleted independently. Local dev is unaffected.

## Out Of Scope

- Custom domain, CDN, autoscaling, paid tiers.
- CI/CD beyond Render's deploy-on-push.
- Any Vercel/Next.js rewrite.
- Email confirmation/SMTP, password-reset emails.
- Extra hardening (rate limiting/WAF) beyond the existing AI cooldown.

## Future Features (backlog — NOT part of this plan; do not implement)

- **Chart pattern detection (formasyon tespiti)**: detect classic patterns (double top/bottom, head & shoulders, triangles, VCP) server-side and show them on the stock detail chart + feed them into the AI commentary prompt. Decided approach: implement 3–4 patterns natively in C# alongside `IndicatorCalculator` (pure functions over OHLC). Reference for algorithms/ideas only: https://github.com/BennyThadikaran/stock-pattern (Python, GPL-3.0 — do NOT copy code, GPL would infect the project; a separate Python microservice was considered and rejected for free-tier complexity). Planner will write a dedicated plan when the user asks, after the deploy task is done.

## Notes For Codex

- One phase per run; update Status and the Implementation Report; list every created/edited file.
- Git exists only after Phase 3 — no git commands before that; afterwards normal git hygiene (no force pushes).
- Turkish UI text conventions stay; `docs/DEPLOY.md` in Turkish.
- Connection string: prefer keyword=value Npgsql format; if handed a `postgresql://` URI, convert it.
- Never print secret values into the plan, reports, logs, or docs — use `<placeholder>`.

## Prior Task (archive)

The original build task completed `done` on 2026-07-16 in 5 phases (SDK install via winget, MVC scaffold + Identity + SQLite, Yahoo market data + 150-symbol list, lightweight-charts + indicators + TradingView widgets, virtual portfolio with P/L, Gemini AI commentary). All acceptance criteria verified locally. Key facts carried forward: model `gemini-3.5-flash` (1.5 was shut down 2025-09-29); Yahoo quote cache 60 s / history 1 h; AI cooldown 30 s + 5 min response cache; AI key sent via `x-goog-api-key` header; anti-forgery on the AI endpoint.

## Implementation Report

### 2026-07-16 — Deploy Phase 1 complete

- Sanitized preflight confirmed `ConnectionStrings:DefaultConnection` exists only in user-secrets, has the expected Supabase session-pooler form on port 5432, and is network-reachable. The subsequent EF migration authenticated successfully, providing the full credential/database check without printing connection details.
- Replaced `Microsoft.EntityFrameworkCore.Sqlite` with `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11, switched `UseSqlite` to `UseNpgsql`, removed the obsolete SQLite output item and appsettings connection string, and retained startup `Database.Migrate()`.
- Deleted both SQLite migration generations and generated `Data/Migrations/20260716130005_InitialPostgres`. The migration uses PostgreSQL identity columns, `numeric(18,4)`, and `timestamp with time zone`, and created Identity, `Portfolios`, `Transactions`, indexes, constraints, and EF migration history in Supabase.
- `PortfolioService.GetPortfoliosAsync` now orders by `CreatedAt` in PostgreSQL. Existing writes already use `DateTimeOffset.UtcNow`; runtime registration, portfolio creation, buy, sell, transaction-history, and detail queries completed without Npgsql UTC errors.
- Removed local `app.db` and its csproj copy rule, changed `.gitignore` to cover `*.db*`, and confirmed no SQLite references, database files, or connection strings in appsettings remain.
- End-to-end Supabase verification: registered an Identity user, created a 100,000-cash portfolio, bought 10 AAPL, sold 4, and loaded the final holding at quantity 6 with both transaction rows. The initial THYAO.IS attempt was stopped by a Yahoo quote timeout before persistence; AAPL isolated and passed the database flow. All clearly named smoke users, portfolios, transactions, scripts, and logs were removed afterward.
- Final verification: `dotnet build BorsaAnaliz.sln --no-restore` succeeded with 0 warnings and 0 errors. `dotnet ef migrations list` connected to Supabase and reported applied migration `20260716130005_InitialPostgres`. Static audit counts: 0 SQLite references, 0 `*.db*` files, 0 connection strings in appsettings, and 0 temporary smoke artifacts.
- File inventory: edited `.gitignore`, `.agents/PLAN.md`, `src/BorsaAnaliz.Web/BorsaAnaliz.Web.csproj`, `Program.cs`, `Services/PortfolioService.cs`, and `appsettings.json`; deleted `src/BorsaAnaliz.Web/app.db` plus the five old files under `Data/Migrations`; created `Data/Migrations/20260716130005_InitialPostgres.cs`, its designer, and `ApplicationDbContextModelSnapshot.cs`.
- Deploy Phase 1 is complete. Status remains `in-progress`; Phase 2 (production middleware, persisted Data Protection keys, and Docker assets) is next.

### 2026-07-16 — Deploy Phase 2 complete

- Added a root multi-stage `Dockerfile`: .NET 8 SDK restore/publish stage followed by the ASP.NET Core 8 runtime image. Its shell entrypoint binds `ASPNETCORE_URLS` to `http://0.0.0.0:${PORT:-8080}` for Render. Added `.dockerignore` rules for build output, repository/agent metadata, local databases, user files, and logs.
- Added `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 8.0.29, made `ApplicationDbContext` implement `IDataProtectionKeyContext`, and registered `PersistKeysToDbContext<ApplicationDbContext>()` with stable application name `BorsaAnaliz`.
- Generated and applied Supabase migration `20260716131711_AddDataProtectionKeys`. The migration creates the key table and immediately enables PostgreSQL RLS using embedded raw SQL. A direct catalog check confirmed RLS is enabled and one Data Protection key is persisted.
- Added forwarded-header processing for `X-Forwarded-For` and `X-Forwarded-Proto`, clearing known proxy/network restrictions for Render's proxy chain. Middleware runs before environment/error handling. Internal HTTPS redirection now runs outside Production only; Render remains responsible for public TLS enforcement.
- Production-mode restart verification against Supabase: `/` returned HTTP 200 without an internal HTTPS redirect; an anonymous protected-route redirect used an `https://` location when forwarded headers were supplied; an antiforgery token issued before process restart remained valid after restart; the second process read `DataProtectionKeys` from PostgreSQL.
- Build/config verification: `dotnet build BorsaAnaliz.sln --no-restore` succeeded with 0 warnings and 0 errors; an equivalent Release `dotnet publish` for the Docker build stage succeeded; both migrations are reported applied in Supabase. Both appsettings files have no connection string or non-empty AI key, and the final pattern audit found 0 embedded secret hits.
- Docker CLI was not installed, so a local image build/run was not possible. Static Dockerfile checks confirmed both stages, Render port binding, and required ignore rules; Render will build the image in Phase 4. All temporary publish output, verification projects, scripts, and logs were removed.
- File inventory: created `Dockerfile`, `.dockerignore`, `src/BorsaAnaliz.Web/Data/Migrations/20260716131711_AddDataProtectionKeys.cs`, and its designer. Edited `.agents/PLAN.md`, `src/BorsaAnaliz.Web/BorsaAnaliz.Web.csproj`, `Program.cs`, `Data/ApplicationDbContext.cs`, and `Data/Migrations/ApplicationDbContextModelSnapshot.cs`. Supabase gained the migrated `DataProtectionKeys` table/key row; no local database files were created.
- Deploy Phase 2 is complete. Status remains `in-progress`; Phase 3 (Git installation, secret preflight, initial commit, and GitHub push if authenticated) is next.

### 2026-07-16 — Deploy Phase 3 partial (local complete; GitHub push pending)

- Installed Git for Windows 2.55.0.3 with winget and verified it after rebuilding PATH from the machine/user environment. The pre-existing `.git` directory was empty and invalid, so it was safely initialized as a new repository with default branch `main`.
- Ran pre-commit scans across the workspace and again against the exact staged index. No Gemini/API-key pattern was found. The only connection-pattern file is `.agents/PLAN.md`, whose Supabase string is explicitly placeholder-only. No certificate/private-key artifacts were staged.
- Confirmed the root `.env` contains local Supabase variable names, is covered by `.gitignore`, and is not in the Git index. The initial index contains 124 intended source/deployment paths and no `.env`.
- Created the initial local commit using repository-local neutral automation identity `Codex <codex@local.invalid>` because no Git author identity was configured. The user may amend this identity before the first push.
- Installed the GitHub connector, but its repository search returned no accessible installation. GitHub CLI is not installed/authenticated, so no GitHub repository or remote could be created automatically. Phase 3 is therefore partial and status remains `in-progress`.
- To finish after creating an empty **borsaAnaliz** repository at `https://github.com/new` (do not add README, license, or `.gitignore`), run:
  - `git config user.name "<your name>"`
  - `git config user.email "<your GitHub email>"`
  - `git commit --amend --reset-author --no-edit` (optional, attributes the initial commit to you)
  - `git remote add origin https://github.com/<username>/borsaAnaliz.git`
  - `git push -u origin main`
- File inventory: installed Git machine-wide; created local `.git` metadata and edited `.agents/PLAN.md`. No application source, runtime configuration, or secret file changed in this phase.
