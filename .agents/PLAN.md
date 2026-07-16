# Plan

## Status

in-progress — 2026-07-17 (UI task rev 2 — Phase 1 complete; Phase 2 next)

## Task Type

Feature / UI redesign.

## User Request

Turkish, summarized: The current UI looks too basic. Redesign all pages with a professional, polished ("kamusal") finance-app feel. The portfolio experience should give the user much more information and detail about the stocks they hold.

## Goal

The site looks like a credible finance product (consistent theme, dashboard-style home, polished tables/cards) and the portfolio page becomes an information-rich dashboard: per-position daily change, weight, realized P/L, cost basis, allocation chart, per-symbol history, and a portfolio value-over-time chart.

## Current State

- Live app: ASP.NET Core 8 MVC, Bootstrap 5 (default look), Turkish UI. Deployed to Render (auto-deploys on push to `main`); DB = Supabase Postgres.
- Views: `Views/Home/Index.cshtml` (placeholder hero), `Views/Stocks/Index.cshtml` (150-row quote table + search/filter), `Views/Stocks/Details.cshtml` (rich: lightweight-charts + indicators + TradingView widgets + AI card), `Views/Portfolio/{Index,Create,Details,Trade}.cshtml` (functional but plain).
- `PortfolioSnapshot` (Models/PortfolioSnapshot.cs) currently exposes: positions (qty, avg cost, current price, value, unrealized P/L ₺/%), cash, transactions, totals. NO daily change, weight, realized P/L, or history series.
- `Quote` model already carries daily change data (used by stock list for change %).
- `YahooMarketDataService.GetHistoryAsync` exists with 1 h cache — reusable for portfolio value history.
- Charts: lightweight-charts CDN already in layout. No pie/donut capability yet.
- IMPORTANT: every push to `main` auto-deploys to production (Render blueprint). Commit per phase; only push when the phase is verified locally.

## Architecture Decisions (made by planner)

- Keep Bootstrap 5 as the base; build a custom finance theme over it in `site.css` using CSS variables. No CSS framework swap, no npm pipeline.
- Palette: dark navy/graphite header + light content, single accent color, green/red only for gains/losses. Add Bootstrap Icons + Inter font via CDN in `_Layout.cshtml`.
- Donut/allocation chart: Chart.js via CDN (lightweight-charts has no pie type). Line/area charts keep using lightweight-charts.
- Realized P/L: compute per symbol with the average-cost method from the transaction ledger (sell proceeds − avg cost at sale time × qty). Pure function in `PortfolioService`; no schema change needed.
- Portfolio value history: reconstruct server-side from the ledger + daily close history (`GetHistoryAsync`, range = since portfolio creation, capped at 1y). Holdings per day derived from transactions; missing close for a day → carry last known. Endpoint returns date/value series; cached 1 h per portfolio.
- No database schema changes in this task. No new tables (so no new RLS work).

## Proposed Changes

One phase per Codex run; each leaves the app building, working, and locally verified before push.

### Phase 1 — Global theme + layout + home dashboard

- [x] `_Layout.cshtml`: sticky dark navbar with active navigation and account menu, official Bootstrap Icons 1.13.1 + Inter CDN links, and a professional footer with delay/disclaimer notes and GitHub link.
- [x] `site.css`: finance-theme variables, navy/light surfaces, consistent radius/shadows, polished cards/tables/forms/buttons, `.gain`/`.loss` states, focus styles, reduced-motion support, and mobile reflow rules.
- [x] Home page → live market dashboard: four internal-only index snapshots (XU100, USD/TRY, S&P 500, NASDAQ 100), linked top-five gainers/losers from the 150-stock catalog, anonymous CTA, and authenticated quick actions. Index symbols remain absent from `symbols.json`.
- [x] Error/Privacy pages: replaced English placeholders with themed Turkish content and safe recovery/privacy guidance.
- [x] Verify: Release build clean; local Supabase-backed runtime rendered four populated snapshot cards and ten mover rows, navbar/footer across pages, and themed Privacy/Error pages. Desktop and narrow headless-browser views were visually checked; the 375 px breakpoint, flex reflow, and no-absolute-footer rules were also checked statically.

### Phase 2 — Stocks list + detail polish

- [ ] `Stocks/Index.cshtml`: sortable columns (symbol, price, change %) client-side; colored change chips (▲/▼ + %); volume column if quote data has it; sticky table header; keep existing search + market filter; row hover → whole row clickable to details.
- [ ] `Stocks/Details.cshtml`: header block with big price + daily change chip + market badge; quick "Portföye ekle" button kept visible; tidy the indicator summary cards into a consistent grid; section headings/tabs styled per theme. Do NOT touch chart/API logic.
- [ ] Verify: sorting works on all three columns both directions; THYAO.IS and AAPL pages render correctly; AI card still works.

### Phase 3 — Portfolio detail enrichment (the core of this task)

- [ ] `PortfolioService`/`PortfolioSnapshot` additions (extend records, keep names consistent):
  - Per position: daily change ₺/% (from quote), weight % of total portfolio value, total cost basis, realized P/L for that symbol, first purchase date.
  - Portfolio totals: day change ₺/%, total realized P/L, total cost basis, position count.
- [ ] `Portfolio/Details.cshtml` redesign:
  - Summary row: Toplam değer (+ day change chip), Toplam K/Z (unrealized), Gerçekleşen K/Z, Nakit — themed stat cards.
  - Allocation donut (Chart.js): positions by value + cash slice; legend with weights.
  - Holdings table: add Günlük değişim, Ağırlık %, Gerçekleşen K/Z, Maliyet columns; row click or chevron expands per-symbol transaction list (buys/sells with dates/prices) rendered from existing transaction data; link to stock details.
  - Transactions section: keep, add type badges (Alış/Satış) and running order; collapse by default if long.
- [ ] `Portfolio/Trade.cshtml`: live preview — on symbol+quantity input show current price, estimated total, cash after trade, owned quantity (small fetch endpoint or reuse quote API); Turkish validation messages kept.
- [ ] `Portfolio/Index.cshtml`: portfolio cards with total value, day change, position count instead of bare list.
- [ ] Verify: with a portfolio holding ≥2 symbols (one BIST one US), all new columns show correct values (hand-check weight sums ≈100% incl. cash; realized P/L matches a manual partial-sell calculation); donut renders; expand shows the right transactions; trade preview matches quote.

### Phase 4 — Portfolio value-over-time chart

- [ ] `GET /api/portfolios/{id}/value-history` (auth, owner-only): reconstruct daily portfolio value (positions × daily closes + cash after each day's transactions) since creation (cap 1y); 1 h cache per portfolio; missing quote days carry forward.
- [ ] `Portfolio/Details.cshtml`: area chart (lightweight-charts) above the holdings table with the value series + a baseline line at initial cash; empty/one-day portfolios show a friendly note instead.
- [ ] Verify: series starts at ≈initial cash on creation day, matches current TotalValue at the end (±quote drift); endpoint 404s for other users' portfolios.

## Acceptance Criteria

- Consistent professional theme on every page (navbar, footer, cards, tables, buttons); no default-Bootstrap-gray placeholder feel.
- Home shows live index snapshot + top movers.
- Portfolio details answers at a glance: what do I hold, how much of my portfolio is it, how did it do today, how much have I realized, how has my portfolio value evolved.
- All existing behavior intact: auth, trading validation, charts, TradingView widgets, AI commentary.
- `dotnet build` clean; site verified locally before each push; production (Render) still healthy after final push.

## Verification

- `dotnet build BorsaAnaliz.sln`; `dotnet run --project src/BorsaAnaliz.Web` + browser checks per phase (desktop + ~375 px width).
- Hand-check the realized P/L and weight math against a scripted buy/sell sequence.
- After final push: HTTP-check https://borsa-analiz-aqr9.onrender.com key pages.

## Assumptions

- Turkish-only UI stays; tr-TR display formatting stays.
- CDN dependencies (Bootstrap Icons, Inter, Chart.js) are acceptable, matching the existing CDN approach.
- No schema/DB changes; snapshot enrichment is computed on the fly from ledger + quotes.
- Index symbols (XU100.IS, USDTRY=X, ^GSPC, ^NDX) work through the existing Yahoo endpoint (they do — same chart API).

## Open Questions

- None blocking.

## Risks

- Every push to `main` deploys to production — a broken phase push breaks the live site. Mitigation: verify locally before push; Render keeps previous deploys for manual rollback.
- Value-history reconstruction is the most complex logic (Phase 4, isolated): transaction-day boundaries and BIST/US calendar gaps — carry-forward rule keeps it simple; hand-check against a known ledger.
- Chart.js + lightweight-charts on one page: keep donut config minimal to avoid bundle/em weight concerns.
- Yahoo rate limits when the home page adds 4 index quotes: they join the existing 60 s quote cache — negligible.

## Rollback / Recovery

- Git per-phase commits; `git revert` any bad phase. Render → previous deploy can be redeployed from the dashboard if production breaks.

## Out Of Scope

- Chart pattern detection (stays in backlog below).
- Dark-mode toggle, i18n framework, accessibility audit beyond sensible markup.
- New data sources; sectors/fundamentals (Yahoo fundamentals API is a different surface — future).
- Schema changes, price alerts, notifications.

## Future Features (backlog — NOT part of this plan; do not implement)

- **Chart pattern detection (formasyon tespiti)**: detect classic patterns (double top/bottom, head & shoulders, triangles, VCP) server-side and show them on the stock detail chart + feed them into the AI commentary prompt. Decided approach: implement 3–4 patterns natively in C# alongside `IndicatorCalculator` (pure functions over OHLC). Reference for algorithms/ideas only: https://github.com/BennyThadikaran/stock-pattern (Python, GPL-3.0 — do NOT copy code). Planner will write a dedicated plan when the user asks.

## Notes For Codex

- One phase per run; update Status + Implementation Report; list every created/edited file.
- Commit at each phase end; push only after local verification (push = production deploy).
- Keep controllers thin; new computations live in `PortfolioService` as pure, unit-checkable functions.
- Preserve existing Turkish strings and tr-TR formatting; `decimal` for money; invariant culture for API JSON.
- Don't rename existing routes/endpoints; the AI endpoint and stock APIs are consumed by existing JS.
- Secrets rules unchanged: nothing secret in the repo; `.env` stays untracked.

## Prior Tasks (archive)

1. **Build task** — `done` 2026-07-16: 5 phases (SDK install, scaffold+Identity+SQLite, Yahoo data + 150-symbol list, charts+indicators+TradingView, virtual portfolio, Gemini AI commentary `gemini-3.5-flash`).
2. **Deploy task** — `done` 2026-07-16: SQLite→Supabase Postgres (Npgsql, fresh `InitialPostgres` migration, RLS enabled on all tables incl. via-migration for new ones), Dockerfile + ForwardedHeaders + DB-persisted Data Protection keys, git+GitHub (`Farukckr/borsaAnaliz`), Render blueprint deploy. Live and publicly smoke-tested: https://borsa-analiz-aqr9.onrender.com (all key pages HTTP 200, portfolio redirects to https login). Pending user hygiene item outside Codex scope: rotate Supabase DB password (update Render env var + local user-secrets together).

## Implementation Report

### 2026-07-17 — UI Phase 1 complete

- Rebuilt the global shell with a sticky navy finance navbar, active-page navigation, branded icon mark, responsive account dropdown/login actions, Inter typography, official Bootstrap Icons 1.13.1 CDN, and a structured footer carrying the delayed-data and investment-disclaimer notices.
- Replaced the starter CSS with a reusable design system: palette/radius/shadow variables, polished cards, tables, forms, buttons, badges, gain/loss pills, focus visibility, reduced-motion handling, chart compatibility, and dedicated responsive behavior. Removed the old scoped absolute-footer rules.
- Added `HomeDashboardViewModel` and made `HomeController.Index` asynchronous. It loads the existing 150-stock catalog plus four internal-only market symbols in one cached quote flow, builds the four market snapshot cards, and selects the five strongest/weakest daily movers without adding non-tradeable indices to `symbols.json`.
- Replaced the placeholder home page with a professional hero, feature summary, live global snapshot, linked mover panels, and authentication-aware CTA/quick-action sections. Reworked Privacy and Error pages in Turkish to match the theme.
- Verification: `dotnet build BorsaAnaliz.sln --configuration Release --no-restore` succeeded with 0 warnings and 0 errors. Local runtime checks returned four populated market cards, ten mover links, zero missing snapshot cards, the new navbar/footer on Home and Stocks, and the themed Privacy/Error content. Headless Chrome screenshots were visually inspected at 1440 px and its reliable narrow 500 px viewport; explicit 375 px CSS breakpoint/reflow checks passed. Static guard found zero index-symbol additions in `Data/symbols.json`; `git diff --check` passed.
- File inventory: created `src/BorsaAnaliz.Web/ViewModels/HomeDashboardViewModel.cs`; edited `.agents/PLAN.md`, `Controllers/HomeController.cs`, `Views/Home/Index.cshtml`, `Views/Home/Privacy.cshtml`, `Views/Shared/Error.cshtml`, `Views/Shared/_Layout.cshtml`, `Views/Shared/_Layout.cshtml.css`, `Views/Shared/_LoginPartial.cshtml`, and `wwwroot/css/site.css`. No database schema, route, secret, or runtime data changed.
- UI Phase 1 is complete. Status remains `in-progress`; Phase 2 (stocks list/detail polish without chart/API changes) is next.
