# Plan

## Status

done — 2026-07-17 (all three phases implemented, verified, and deployed.)

## Task Type

Feature (3 sub-features: market segmentation, per-user watchlist, KAP disclosure news).

## User Request

Turkish, summarized: (1) Separate stock lists: BIST 100, BIST 500 (official index `XU500`, confirmed live by Codex preflight), and US market. (2) A per-user watchlist ("Takip Listem"). (3) A system for current important KAP (Kamuyu Aydınlatma Platformu) disclosures: a detailed dedicated page + an attention-grabbing placement on the home page.

## Goal

Users can browse BIST 100 / BIST 500 / ABD as separate tabs, star any stock into their personal watchlist, and read the latest KAP disclosures on a dedicated news page with a prominent home-page panel.

## Current State

- Live production app (ASP.NET Core 8 MVC + Supabase Postgres + Render auto-deploy on push to `main`). All prior tasks `done`.
- Catalog: `Data/symbols.json` — 100 BIST (XU100) + 50 US entries `{symbol, tvSymbol, name, market}`; loaded by `JsonStockCatalogService`.
- `StocksController.Index` renders all 150 rows with quotes in one batched call (20-symbol chunks, 60 s cache); client-side search/sort + BIST/US filter buttons exist.
- Auth = ASP.NET Identity; portfolio tables exist; ALL tables have RLS enabled — **any new table's migration must embed `ALTER TABLE public."<Table>" ENABLE ROW LEVEL SECURITY;`** (established pattern, see `20260716131711_AddDataProtectionKeys`).
- Home page: index snapshot cards + top movers (computed from the 150-symbol catalog).
- No news/KAP integration exists.
- KAP has an **unauthenticated JSON API** used by its own site (Next.js SPA backend). Community-documented endpoint: `https://www.kap.org.tr/tr/api/disclosure/members/byCriteria` (results hard-capped at 2000; use 1–3 day windows). Reference notes: https://github.com/caganco/trailingedge/blob/master/docs/KAP_ENDPOINT_NOTES.md and https://github.com/cahitihac/kap-notifier. Unofficial — implement defensively.

## Architecture Decisions (made by planner)

- **Catalog schema change**: add `"indices"` array to each BIST entry in `symbols.json` — XU100 members get `["XU100","XU500"]` (XU100 ⊂ XU500; sanity-check against the official constituent lists), remaining BIST 500 members get `["XU500"]`. US entries get `"indices": []` or omit. BIST 500 constituents sourced from Borsa İstanbul's official index constituent list (same approach as the earlier XU100 CSV); expect ~500 symbols total. Names in Turkish uppercase as before; every BIST symbol keeps `.IS` suffix + `BIST:<code>` tvSymbol.
- **List performance**: BIST 500 (~500 rows) must NOT quote everything in one request. Tabs are server-rendered per segment (`/Stocks?list=xu100|xu500|us`); XU100 and US tabs keep current eager quoting; the XU500 tab is paginated server-side (50 rows/page) with quotes fetched only for the current page. Search stays client-side within the rendered tab; note on the tab that full BIST 500 search happens there.
- **Watchlist storage**: new table `WatchlistItems` (Id, UserId FK→AspNetUsers cascade, Symbol, CreatedAt) with unique index (UserId, Symbol). RLS enabled inside the migration (raw SQL, established pattern).
- **Watchlist UX**: star toggle (bi-star / bi-star-fill) on stock list rows and the detail page header; anonymous click → login redirect. "Takip Listem" as a nav link + as an extra tab on the stocks page rendering the user's symbols with quotes. Toggle via authenticated anti-forgery POST endpoint returning JSON.
- **KAP service**: `IKapNewsService` + `KapNewsService` calling the byCriteria endpoint for the last 2 days (fromDate/toDate), browser-like User-Agent, 10 s timeout, `IMemoryCache` 5 min, graceful empty list + logged warning on any failure (schema drift, 4xx/5xx). Map disclosures to catalog symbols by matching KAP stock codes (e.g. `AKBNK` → `AKBNK.IS`) so items can link to our detail pages. Each item links to the official disclosure at `https://www.kap.org.tr/tr/Bildirim/{id}`.
- **News UI**: dedicated `/Haberler` page (controller `NewsController`) — list of disclosures with time, company (linked to our stock page when matched), disclosure type/category badge, subject line, KAP link; client-side text filter. Home page gets a prominent "Son KAP Bildirimleri" panel near the top (below index cards, above movers) showing the latest 6 with a "Tümünü gör" link.
- No AI/portfolio changes in this task.

## Proposed Changes

One phase per Codex run; each leaves the app building, locally verified, then pushed (push = production deploy).

### Phase 1 — Market segments: BIST 100 / BIST 500 / ABD

- [x] Extend `symbols.json`: add `indices` to existing 100 BIST entries (`["XU100","XU500"]`, after confirming each is in the current official XU100 list); append remaining BIST 500 constituents (`["XU500"]`) from Borsa İstanbul's official XU500 constituent list with correct names + tvSymbols. Keep the 50 US entries unchanged. Validate: no duplicate symbols, all BIST have `.IS`, XU100 entries are a subset of XU500, total BIST count ≈500.
- [x] `StockSymbol` model + `JsonStockCatalogService`: parse `indices`; add segment query helpers (ByIndex("XU100"), ByIndex("XU500"), ByMarket("US"), All).
- [x] `StocksController.Index(list, page)`: tabs BIST 100 (default) / BIST 500 / ABD; BIST 500 paginated 50/page with Turkish pager; quotes only for rendered rows. Preserve sorting/search/chips within the page.
- [x] Home movers: restrict to XU100 + US (current behavior, explicit) so home stays fast.
- [x] Verify: all three tabs render with live quotes; BIST 500 pager works and each page loads in a few seconds; search/sort intact; a symbol outside XU100 (e.g. a small-cap XU500 member) opens its detail page with charts working (spot-check Yahoo coverage for a few).

### Phase 2 — Per-user watchlist (Takip Listem)

- [x] `WatchlistItem` entity + migration (unique UserId+Symbol, cascade delete) **with RLS enable SQL embedded in the migration**.
- [x] `WatchlistService` (`IWatchlistService`): get user's symbols, toggle (validate symbol exists in catalog), count.
- [x] `POST /api/watchlist/toggle` (auth + anti-forgery) returning `{added: bool}`; star buttons on stock list rows + detail header reflect state (filled/empty), anonymous → login link.
- [x] "Takip Listem" tab on `/Stocks` (auth; friendly empty state with CTA) + navbar link with `bi-star` icon.
- [x] Verify: toggle from list and detail persists across reload; unique constraint holds (double-toggle removes); another user's watchlist is not visible; anonymous sees login prompt; RLS confirmed enabled on the new table.

### Phase 3 — KAP disclosures (news system)

- [x] `Models/KapDisclosure` (id, time, companyName, stockCodes, matchedSymbol?, category/type, subject/title) + `IKapNewsService`/`KapNewsService` per Architecture Decisions. First implementation step: fetch the endpoint once, inspect the real response shape, and parse defensively (unknown fields tolerated, log+empty on parse failure).
- [x] `NewsController` + `Views/News/Index.cshtml` (`/Haberler`): latest ~100 disclosures (2-day window), rows with relative time ("12 dk önce"), company link to our detail page when matched, category badge, KAP source link (new tab); client-side text filter box. Empty/failed state: "KAP bildirimlerine şu anda ulaşılamıyor."
- [x] Home page: "Son KAP Bildirimleri" card panel (latest 6, compact rows, "Tümünü gör →" to /Haberler) placed prominently below the index cards; renders nothing (collapsed) if the service returns empty — never an error.
- [x] Navbar: "Haberler" link.
- [x] Optional if trivial: on stock detail, a small "Şirket bildirimleri" list filtered to that symbol. (Reviewed and intentionally omitted: it would expand the stock-detail model/controller path; the dedicated page and home panel satisfy the required scope.)
- [x] Verify: /Haberler lists real current disclosures with working KAP links and matched company links; home panel shows 6 and degrades to hidden when the service is forced to fail; repeated loads hit the 5 min cache (no request spam in logs).

## Acceptance Criteria

- Stocks page has BIST 100 / BIST 500 / ABD (+ Takip Listem when logged in) tabs; BIST 500 covers the official XU500 universe with acceptable page loads (pagination).
- Logged-in users can star/unstar any stock from list or detail and see them under Takip Listem; data is per-user and RLS-protected.
- /Haberler shows current KAP disclosures with source links; home page surfaces the latest 6 prominently; KAP outages degrade silently.
- Existing features (portfolio, charts, AI) untouched and working; `dotnet build` clean; production healthy after each push.

## Verification

- `dotnet build BorsaAnaliz.sln`; local run against Supabase; browser checks per phase (desktop + ~375 px).
- Supabase: `WatchlistItems` exists with RLS enabled; rows scoped to the right UserId.
- Live HTTP checks on https://borsa-analiz-aqr9.onrender.com after each push.

## Assumptions

- BIST 500 = official `XU500` index (~500 constituents, superset of XU100). BIST Tüm/other indices can be added later as extra tags in `indices`.
- Yahoo Finance serves quotes/history for all XU500 symbols with `.IS` suffix (spot-check a few small-caps; missing quotes render "-" as today).
- KAP's unofficial API stays reachable from Render's Frankfurt IPs; if KAP blocks datacenter IPs, the feature degrades gracefully (empty panel) — acceptable for now, noted in Risks.
- "Önemli haberler" = latest disclosures (all categories) for now; importance filtering (e.g. only material-event categories) can be a later refinement once real category values are observed.

## Open Questions

- None blocking.

## Risks

- KAP API is unofficial: shape/paths may change, and it may rate-limit or block cloud IPs — defensive parsing, 5 min cache, silent degradation are mandatory; test from production after deploy, not only locally.
- XU500 constituent list quality (renames, delistings): validate for duplicates; missing Yahoo data for tiny symbols must not break rows.
- ~550-symbol catalog (500 BIST + 50 US) raises memory/CPU slightly — pagination keeps quote fan-out bounded.
- New table = new RLS surface — the migration-embedded RLS pattern is non-negotiable.
- Every push auto-deploys; verify locally first (previous deploy crashed once with status 139 — transient, but don't push unverified).

## Rollback / Recovery

- Per-phase git commits; `git revert` + push redeploys the previous state. Watchlist rollback: revert migration (`dotnet ef migrations remove` before push, or a down-migration after). Render dashboard can redeploy any earlier successful build.

## Out Of Scope

- Real-time streaming news/websockets; push notifications.
- AI summarization of KAP disclosures (possible future: feed matched disclosures into the AI commentary prompt).
- Sentiment/importance scoring; non-KAP news sources (US market news).
- Watchlist price alerts.
- Chart pattern detection (still in backlog).

## Future Features (backlog — NOT part of this plan; do not implement)

- **Chart pattern detection (formasyon tespiti)**: 3–4 classic patterns in C# alongside `IndicatorCalculator`; reference (ideas only, GPL — no code copying): https://github.com/BennyThadikaran/stock-pattern.
- **KAP → AI**: include a company's recent disclosures in the Gemini commentary prompt.

## Notes For Codex

- One phase per run; update Status + Implementation Report; list every created/edited file; verify locally before push.
- New tables: embed `ALTER TABLE public."<Table>" ENABLE ROW LEVEL SECURITY;` in the migration (see AddDataProtectionKeys migration for the pattern).
- KAP requests: browser-like User-Agent + `Accept: application/json`; never let a KAP failure surface as a 500 anywhere.
- Turkish UI strings; tr-TR display formatting; invariant culture for parsing; `decimal` for money.
- Don't touch AI, portfolio, or chart code paths in this task.
- Secrets rules unchanged; no new secrets needed (KAP is keyless).

## Prior Tasks (archive)

1. **Build task** — done 2026-07-16 (app: market data, charts+indicators+TradingView, virtual portfolio, Gemini AI).
2. **Deploy task** — done 2026-07-16 (Supabase Postgres + RLS-all-tables, Docker, GitHub `Farukckr/borsaAnaliz`, Render blueprint; live at https://borsa-analiz-aqr9.onrender.com). Pending user hygiene: rotate Supabase DB password.
3. **UI redesign task** — done 2026-07-17 (finance theme, home dashboard, sortable lists, enriched portfolio analytics + value-history chart, AI commentary quality fix `gemini-3.5-flash` thinking=minimal/2048 tokens). Post-task hotfixes: BIST TradingView advanced chart hidden (licensing fallback showed AAPL), rising-blocks SVG logo + favicon added (planner-implemented, commit 320cf17).

## Implementation Report

### 2026-07-17 — Phase 1 preflight blocked

- No application code was changed. Preflight found a blocking contradiction between the user request and the planner's scope interpretation.
- The plan states that no BIST 500 index exists and therefore translates the requested “BIST 500” tab to BIST Tüm (`XUTUM`). Borsa İstanbul's current official index page lists **BIST 500 (`XU500`)** as a live indicator index, while BIST Tüm (`XUTUM`) is a separate, broader index. The official site currently reports roughly 500 versus 566 constituents, so this is a material catalog and UX decision rather than a naming-only adjustment.
- Planner/user decision required: implement the originally requested BIST 500 (`XU500`) tab, or intentionally use the broader BIST Tüm (`XUTUM`) tab. After that choice, revise the catalog schema, Phase 1 labels, validation counts, assumptions, and acceptance criteria consistently.
- File inventory: edited `.agents/PLAN.md` only to record the blocker. Phase 1 checkboxes remain open; no catalog, model, controller, view, secret, database, or deployment change was made.

### 2026-07-17 — Phase 1 market segmentation complete

- Expanded the stock catalog to the official 17/07/2026 XU500 universe: 500 BIST symbols and the unchanged 50-symbol US list. XU100 has exactly 100 members, is a complete XU500 subset, and both memberships match Borsa İstanbul's constituent CSV exactly after normalizing its `.E` suffix.
- Added index/market catalog queries and server-rendered BIST 100, BIST 500, and ABD tabs. BIST 500 quotes only its current 50-row page; invalid lists fall back to BIST 100 and page numbers are clamped safely. Home movers explicitly remain limited to XU100 + US.
- Verification: `dotnet build src/BorsaAnaliz.Web/BorsaAnaliz.Web.csproj -c Release --no-restore` completed with 0 warnings/errors; `git diff --check` passed. Local HTTP checks returned 100/50/50 rows for XU100/XU500/US, both XU500 page 1 and page 10 returned 50 rows, invalid/clamped queries behaved correctly, and `A1CAP.IS` detail plus one-month history returned HTTP 200 with 22 candles. Search and sort scripts remain attached to the rendered rows.
- File inventory: `.agents/PLAN.md`; `src/BorsaAnaliz.Web/Controllers/HomeController.cs`; `src/BorsaAnaliz.Web/Controllers/StocksController.cs`; `src/BorsaAnaliz.Web/Data/symbols.json`; `src/BorsaAnaliz.Web/Models/StockSymbol.cs`; `src/BorsaAnaliz.Web/Services/IStockCatalogService.cs`; `src/BorsaAnaliz.Web/Services/JsonStockCatalogService.cs`; `src/BorsaAnaliz.Web/ViewModels/StocksIndexViewModel.cs`; `src/BorsaAnaliz.Web/Views/Stocks/Index.cshtml`.
- No database schema, migration, secret, AI, portfolio, or chart implementation was changed in this phase.

### 2026-07-17 — Phase 2 per-user watchlist complete

- Added `WatchlistItems` with a cascade FK to `AspNetUsers`, a unique `(UserId, Symbol)` index, and migration-embedded `ENABLE ROW LEVEL SECURITY`. The migration was applied to Supabase successfully.
- Added a user-scoped watchlist service with catalog validation, ordered symbol reads, toggle, and count operations. All reads and mutations include the authenticated user's id.
- Added the authenticated anti-forgery-protected `POST /api/watchlist/toggle` endpoint, shared client-side toggle behavior, filled/empty stars on stock rows and detail pages, anonymous login links, an authenticated "Takip Listem" stocks tab with empty-state CTA, and an authenticated navbar link.
- Verification: `dotnet build BorsaAnaliz.sln -c Release --no-restore` completed with 0 warnings/errors and `git diff --check` passed. PostgreSQL catalog checks returned `rls=True`, `uniqueIndex=True`, and `cascadeFk=True`. Two temporary users verified persistence, double-toggle removal, and cross-user isolation; a separate detail-page test persisted `AKBNK.IS`; anonymous list/direct-watchlist behavior routed to login; missing anti-forgery and invalid symbols returned HTTP 400. A 375×812 headless-browser check confirmed the star remains visible as the first responsive table column. All temporary test users were deleted and the database check returned `leftoverTestUsers=0`.
- File inventory: `.agents/PLAN.md`; `src/BorsaAnaliz.Web/Controllers/HomeController.cs`; `src/BorsaAnaliz.Web/Controllers/StocksController.cs`; `src/BorsaAnaliz.Web/Data/ApplicationDbContext.cs`; `src/BorsaAnaliz.Web/Data/Migrations/20260717101243_AddWatchlist.cs`; `src/BorsaAnaliz.Web/Data/Migrations/20260717101243_AddWatchlist.Designer.cs`; `src/BorsaAnaliz.Web/Data/Migrations/ApplicationDbContextModelSnapshot.cs`; `src/BorsaAnaliz.Web/Models/WatchlistItem.cs`; `src/BorsaAnaliz.Web/Program.cs`; `src/BorsaAnaliz.Web/Services/IWatchlistService.cs`; `src/BorsaAnaliz.Web/Services/WatchlistService.cs`; `src/BorsaAnaliz.Web/ViewModels/StockDetailsViewModel.cs`; `src/BorsaAnaliz.Web/ViewModels/StockListItemViewModel.cs`; `src/BorsaAnaliz.Web/Views/Shared/_Layout.cshtml`; `src/BorsaAnaliz.Web/Views/Stocks/Details.cshtml`; `src/BorsaAnaliz.Web/Views/Stocks/Index.cshtml`; `src/BorsaAnaliz.Web/wwwroot/js/site.js`.
- No new secret, AI, portfolio transaction, chart, or KAP implementation was introduced in this phase.

### 2026-07-17 — Phase 3 KAP disclosures complete

- Inspected KAP's current official client request and response before implementation. The endpoint is a JSON `POST`; the live response is a flat array containing `publishDate`, `kapTitle`, disclosure class/type, subject/summary, stock codes/related stocks, and `disclosureIndex`.
- Added a defensive KAP service using a browser-like request, a two-day date window, 10-second timeout, five-minute shared memory cache, tolerant field parsing, catalog symbol matching, Turkish category labels, a 100-item limit, and logged empty-list degradation on network, timeout, or JSON failures.
- Added `/Haberler` with relative times, category badges, text filtering, matched stock-detail links, official KAP links, and a friendly unavailable state. Added the latest six disclosures below the home market cards and a navbar "Haberler" link. The optional stock-detail disclosure panel was deliberately omitted to avoid expanding that otherwise unrelated controller/model path.
- Verification: `dotnet build BorsaAnaliz.sln -c Release --no-restore` completed with 0 warnings/errors and `git diff --check` passed. Local live-data checks rendered 100 disclosures, 100 KAP links, 55 catalog-matched stock links, correct Turkish text, and exactly six home rows; repeated `/Haberler` and home requests reused the five-minute cache. The first official KAP source link and matched `ALCAR.IS` detail link both returned HTTP 200. With `Kap:BaseUrl` forced to an unreachable address, home returned HTTP 200 with no KAP panel and `/Haberler` returned HTTP 200 with the required unavailable message; the repeated failure request returned from the cached empty result. The 375×812 browser view rendered the responsive news cards and filter.
- File inventory: `.agents/PLAN.md`; `src/BorsaAnaliz.Web/Controllers/HomeController.cs`; `src/BorsaAnaliz.Web/Controllers/NewsController.cs`; `src/BorsaAnaliz.Web/Models/KapDisclosure.cs`; `src/BorsaAnaliz.Web/Program.cs`; `src/BorsaAnaliz.Web/Services/IKapNewsService.cs`; `src/BorsaAnaliz.Web/Services/KapNewsService.cs`; `src/BorsaAnaliz.Web/ViewModels/HomeDashboardViewModel.cs`; `src/BorsaAnaliz.Web/Views/Home/Index.cshtml`; `src/BorsaAnaliz.Web/Views/News/Index.cshtml`; `src/BorsaAnaliz.Web/Views/Shared/_Layout.cshtml`; `src/BorsaAnaliz.Web/wwwroot/css/site.css`.
- No database schema, migration, secret, AI, portfolio, or chart implementation was changed in this phase.
