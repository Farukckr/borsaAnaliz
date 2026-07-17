# Plan

## Status

in-progress — 2026-07-17 (UI task rev 12 — final Phase 5 live verification underway)

> **Blocker resolution (planner, 2026-07-17):** (1) Local `Ai:ApiKey` user-secret was stale after the user rotated the Gemini key — updated to the new key and verified directly against `gemini-3.5-flash` (successful response). (2) Render was mid-deploy when checked; the live site now serves the latest `main` (Phase 1 theme markers AND the Phase 5 AI meta line are present on https://borsa-analiz-aqr9.onrender.com). Remaining work for the next run: live AKSA.IS/AAPL AI commentary checks (complete sectioned output, no truncation across 3 tries) locally; production AI check needs an authenticated browser session — if not feasible, verify locally and note that Render's `Ai__ApiKey` env var must hold the rotated key (user entered it during blueprint setup). Then mark the plan `done`.

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

- [x] `Stocks/Index.cshtml`: added accessible client-side sorting for symbol, price, and change %, colored direction chips, a sticky table header, keyboard/click whole-row navigation, and polished market badges while retaining the existing search and market filter. `Quote` has no volume property, so the conditional volume column was correctly omitted.
- [x] `Stocks/Details.cshtml`: added a professional quote header with large price, absolute/percentage daily-change chip, market/symbol badges, and a prominent quick "Portföye ekle" action; normalized the indicator summary grid and themed chart, AI, and TradingView section headings. The chart/API and AI JavaScript source remained unchanged.
- [x] Verify: real headless-browser clicks confirmed ascending and descending sorting on all three columns; THYAO.IS and AAPL rendered with populated quote/indicator layouts; history and indicator endpoints returned data; the AI card IDs, anonymous login action, and unchanged client/controller flow remained intact.

### Phase 3 — Portfolio detail enrichment (the core of this task)

- [x] `PortfolioService`/`PortfolioSnapshot` additions (extend records, keep names consistent):
  - Per position: daily change ₺/% (from quote), weight % of total portfolio value, total cost basis, realized P/L for that symbol, first purchase date.
  - Portfolio totals: day change ₺/%, total realized P/L, total cost basis, position count.
- [x] `Portfolio/Details.cshtml` redesign:
  - Summary row: Toplam değer (+ day change chip), Toplam K/Z (unrealized), Gerçekleşen K/Z, Nakit — themed stat cards.
  - Allocation donut (Chart.js): positions by value + cash slice; legend with weights.
  - Holdings table: add Günlük değişim, Ağırlık %, Gerçekleşen K/Z, Maliyet columns; row click or chevron expands per-symbol transaction list (buys/sells with dates/prices) rendered from existing transaction data; link to stock details.
  - Transactions section: keep, add type badges (Alış/Satış) and running order; collapse by default if long.
- [x] `Portfolio/Trade.cshtml`: added an authenticated owner-only live preview endpoint and responsive preview card showing current price, estimated total, post-trade cash, owned quantity, direction-aware validation, and the existing Turkish server validation flow.
- [x] `Portfolio/Index.cshtml`: replaced the bare portfolio list with live snapshot cards showing total value, daily change, position count, cash, and quote-health state.
- [x] Verify: a temporary portfolio with THYAO.IS and AAPL plus a partial THYAO sale rendered every new field; cash-inclusive allocation weights summed to exactly 100%; a deterministic average-cost script produced the expected ₺200 realized P/L; donut, symbol expansion, transaction rows, and quote-matched trade preview all worked. The temporary account and data were deleted afterward through the application UI.

### Phase 4 — Portfolio value-over-time chart

- [x] `GET /api/portfolios/{id}/value-history` (auth, owner-only): reconstruct daily portfolio value (positions × daily closes + cash after each day's transactions) since creation (cap 1y); 1 h cache per portfolio; missing quote days carry forward.
- [x] `Portfolio/Details.cshtml`: area chart (lightweight-charts) above the holdings table with the value series + a baseline line at initial cash; empty/one-day portfolios show a friendly note instead.
- [x] Verify: series starts at ≈initial cash on creation day, matches current TotalValue at the end (±quote drift); endpoint 404s for other users' portfolios.

### Phase 5 — AI commentary quality fix (user-reported bug + UX)

User report 2026-07-17: AI output arrives truncated mid-sentence (e.g. AKSA.IS → "hissesi, orta ve" then disclaimer) and carries no takeaway.

- [x] Root cause fix in `GeminiCommentaryService`: `maxOutputTokens = 800` is consumed by Gemini 3.5's internal thinking tokens, truncating the visible text. Raise to 2048 and CHECK `candidates[0].finishReason`: on `MAX_TOKENS` treat as failure ("AI yorumu tamamlanamadı, tekrar deneyin") instead of appending the disclaimer to a truncated fragment — never present cut-off text as a finished analysis.
- [x] Structured output: rewrite the prompt to demand fixed Markdown sections in this exact order — `## Özet` (2 sentences + one label line `**Genel Görünüm:** Olumlu/Nötr/Olumsuz`), `## Trend`, `## Göstergeler` (one bullet per indicator with its reading, e.g. "RSI 71 — aşırı alım bölgesinde"), `## Destek ve Direnç` (concrete price levels from the data), `## Riskler`. Keep existing guardrails (no buy/sell instructions, no invented news, end with the exact disclaimer).
- [x] Response sanity check: if the returned text lacks the `## Özet` heading, treat as failure with retry message (guards against format drift).
- [x] AI card UX in `Stocks/Details.cshtml`: show a meta line above the result — "Son 60 günlük OHLC verisi ve teknik göstergelere göre üretildi · <timestamp>" — so users know what the analysis covers; keep the existing error/cooldown handling.
- [ ] Verify: live AKSA.IS and AAPL commentary returns complete, sectioned output ending with the disclaimer (no mid-sentence cuts across 3 consecutive tries); a simulated MAX_TOKENS/short response shows the retry message, not a fragment.

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

### 2026-07-17 — UI Phase 2 complete

- Reworked the 150-stock discovery table with a sticky header, accessible sort buttons and `aria-sort` state, stable missing-value handling, branded market/change pills, and whole-row mouse/keyboard navigation. Existing Turkish search, BIST/ABD filtering, live result count, and empty-result feedback were preserved.
- Rebuilt the stock detail header into a responsive finance quote surface showing market and symbol context, a large localized price, absolute and percentage daily movement, and a prominent portfolio action. Consolidated the four indicator cards and themed the native chart, AI commentary, and TradingView section headers without changing their runtime IDs.
- Volume was intentionally not added: the current `Quote` record exposes price, previous close, change, change percentage, currency, and market time but no volume field.
- Verification: `dotnet build BorsaAnaliz.sln --configuration Release --no-restore` succeeded with 0 warnings and 0 errors. Local `/Stocks`, `/Stocks/Details/THYAO.IS`, `/Stocks/Details/AAPL`, AAPL history, and AAPL indicator requests all returned HTTP 200; the APIs returned 251 history points and a populated 1y indicator response. Headless Chrome executed both directions for symbol, price, and change sorting and returned correctly ordered leading values. Desktop 1440 px and narrow 390 px screenshots were visually inspected. The details `@section Scripts` content, `StocksController`, and AI card IDs remained unchanged; `git diff --check` passed.
- File inventory: edited `.agents/PLAN.md`, `src/BorsaAnaliz.Web/Views/Stocks/Index.cshtml`, `src/BorsaAnaliz.Web/Views/Stocks/Details.cshtml`, and `src/BorsaAnaliz.Web/wwwroot/css/site.css`. No model, controller, route, database schema, secret, or runtime data changed.
- UI Phase 2 is complete. Status remains `in-progress`; Phase 3 (portfolio detail enrichment) is next.

### 2026-07-17 — UI Phase 3 complete

- Extended `PortfolioSnapshot` and each open position with daily movement, portfolio weight, total cost basis, realized P/L, and first-purchase date; added portfolio totals for daily movement, unrealized/realized P/L, cost basis, and position count. A reusable pure `PortfolioService.CalculateLedger` function now applies average-cost accounting in chronological order and drives cash, open quantity, cost basis, and realized P/L consistently across snapshots, previews, buys, and sells.
- Added batched portfolio snapshot loading for the index and an authenticated, owner-scoped `GET /api/portfolios/{portfolioId}/trade-preview` endpoint. The endpoint uses the same quote and ledger logic as order execution and returns current price, estimated total, cash after the selected direction, owned quantity, and an executable/warning state.
- Rebuilt the portfolio index as information-rich cards. Rebuilt details with four summary cards, a Chart.js 4.5.1 allocation donut plus server-rendered weighted legend, a detailed holdings table, first-purchase metadata, per-symbol collapsible transaction histories, type badges, and a long-history collapse. Rebuilt the trade page with buy/sell direction controls, debounced live preview, responsive validation state, and mobile-first presentation.
- Verification: `dotnet build BorsaAnaliz.sln --configuration Release --no-restore` succeeded with 0 warnings and 0 errors. A deterministic ledger scenario (THYAO buys 10@100 and 10@120, sell 5@150; AAPL buy 4@200) returned cash 97,750, THYAO cost basis 1,650, realized P/L 200, and cash-inclusive weights totaling 100.000000%. A disposable authenticated integration account created a 100,000 ₺ portfolio, bought THYAO.IS and AAPL, partially sold THYAO, and confirmed 2 positions, 3 transactions, a three-slice ready Chart.js donut, working symbol expansion, and a trade preview matching the displayed ₺330 quote with owned quantity 7. Live DOM measurement showed allocation weights totaling exactly 100% and no mobile horizontal document overflow. Desktop portfolio index/detail and 390 px trade/detail screenshots were visually inspected. Anonymous portfolio and preview requests both redirected to login; the disposable account was deleted through Identity UI. `git diff --check` and the secret-pattern scan passed.
- File inventory: edited `.agents/PLAN.md`, `Controllers/PortfolioController.cs`, `Models/PortfolioSnapshot.cs`, `Services/IPortfolioService.cs`, `Services/PortfolioService.cs`, `Views/Portfolio/Index.cshtml`, `Views/Portfolio/Details.cshtml`, `Views/Portfolio/Trade.cshtml`, and `wwwroot/css/site.css`. No database schema, migration, secret, or persistent test data remains.
- UI Phase 3 is complete. Status remains `in-progress`; Phase 4 (portfolio value-over-time chart) is next.

### 2026-07-17 — UI Phase 4 complete

- Added an authenticated, owner-scoped `GET /api/portfolios/{id}/value-history` endpoint and a pure `PortfolioService.CalculateValueHistory` reconstruction routine. It applies the transaction ledger chronologically, values open quantities with daily closes, carries the last known price across non-trading days, seeds missing prices from transaction prices, limits the series to one year, and caches each user/portfolio result for one hour. Successful trades invalidate the relevant cache entry.
- Added a responsive Lightweight Charts area chart above the holdings table with a dashed initial-cash baseline, localized TRY values, automatic fitting/resizing, loading and failure states, and friendly server/client empty states for portfolios without transactions or enough value days.
- Verification: `dotnet build BorsaAnaliz.sln --configuration Release --no-restore` succeeded with 0 warnings and 0 errors. A deterministic five-day ledger returned exactly 1,000, 1,020, 1,020, 1,060, and 1,070, proving buy/sell cash flow and missing-day carry-forward behavior. A disposable authenticated portfolio produced 13 daily points beginning at exactly 100,000 and ending at 99,972 versus a 100,000 current snapshot (28 quote-drift difference); a repeated request returned the identical cached series. Desktop and 390 px chart screenshots were visually inspected, and an empty portfolio rendered the friendly no-history state. A second authenticated user received `404` for the owner-only endpoint, while an anonymous request redirected to login with `302`. Both disposable accounts and their data were deleted afterward.
- File inventory: edited `.agents/PLAN.md`, `src/BorsaAnaliz.Web/Controllers/PortfolioController.cs`, `src/BorsaAnaliz.Web/Models/PortfolioSnapshot.cs`, `src/BorsaAnaliz.Web/Services/IPortfolioService.cs`, `src/BorsaAnaliz.Web/Services/PortfolioService.cs`, `src/BorsaAnaliz.Web/Views/Portfolio/Details.cshtml`, and `src/BorsaAnaliz.Web/wwwroot/css/site.css`. No database schema, migration, secret, or persistent test data remains.
- UI Phase 4 is complete. Status remains `in-progress`; Phase 5 (AI commentary truncation and finish-reason handling) is next.

### 2026-07-17 — UI Phase 5 implemented; live verification blocked

- Raised Gemini output capacity from 800 to 2048 tokens and now reads the first candidate's `finishReason`. `MAX_TOKENS` responses discard all partial text and return the retry message; responses without an exact `## Özet` heading are rejected through the same safe path, so a fragment can no longer be presented as a completed analysis.
- Replaced the free-form prompt with the required ordered Markdown contract (`Özet`, `Trend`, `Göstergeler`, `Destek ve Direnç`, `Riskler`), exact general-view label, concrete OHLC-derived price levels, per-indicator readings, existing no-advice/no-invented-news guardrails, and exact disclaimer ending.
- Extended AI responses and the five-minute success cache with `GeneratedAt` and `Succeeded`. The AI card now shows the requested 60-day OHLC/indicator meta line with a localized timestamp only for successful analyses, preserves the original cooldown/HTTP error behavior, and renders safe service failures as warnings.
- Verification completed: `dotnet build BorsaAnaliz.sln --configuration Release --no-restore` succeeded with 0 warnings and 0 errors. A fake-HTTP service verifier confirmed `maxOutputTokens = 2048`, prompt section order, exact disclaimer instruction, discarded `MAX_TOKENS` fragments, and rejection of a short response without `## Özet`. Authenticated browser DOM tests rendered all five sections, the general-view label, exact disclaimer, and localized meta timestamp; desktop and 390 px screenshots were visually inspected. The disposable UI/API accounts were deleted.
- External verification blocker: the configured local `Ai:ApiKey` is rejected by Google's API with `401 UNAUTHENTICATED`, including for a minimal direct request, so three real AKSA.IS/AAPL generations cannot be completed. The application correctly returned its safe failure text instead of a fragment. Render also still serves the pre-Phase-4 build (`value-history` returns `404` and the Phase 4 CSS marker is absent), so production verification cannot proceed until a valid Gemini key is configured and the queued/missing Render deployment is manually resolved.
- File inventory: edited `.agents/PLAN.md`, `src/BorsaAnaliz.Web/Controllers/StocksController.cs`, `src/BorsaAnaliz.Web/Models/AiCommentaryResponse.cs`, `src/BorsaAnaliz.Web/Services/GeminiCommentaryService.cs`, and `src/BorsaAnaliz.Web/Views/Stocks/Details.cshtml`. No database schema, migration, secret, or persistent test data changed.
- Phase 5 code is implemented, but the plan remains `blocked`; after external configuration is fixed, rerun the unchecked live AKSA.IS/AAPL verification and production smoke tests before setting the plan to `done`.
