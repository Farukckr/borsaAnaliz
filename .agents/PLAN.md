# Plan

## Status

in-progress — 2026-07-17 (Phases 1–2 complete; Phase 3 pending)

## Task Type

Feature (3 sub-features from backlog: more KAP event tabs, KAP context in AI commentary, native chart pattern detection).

## User Request

Turkish, summarized: continue with the remaining backlog features (Supabase password rotation deliberately postponed by the user): (1) dividend ("temettü") and capital-increase ("sermaye artırımı") tracking, (2) feed KAP disclosures into the AI commentary, (3) chart pattern detection ("formasyon tespiti").

## Goal

/Haberler covers dividends and capital increases as first-class tabs; the AI commentary knows about a company's recent disclosures; stock detail pages detect and display classic chart patterns computed by our own C# code.

## Current State

- Live production app (ASP.NET Core 8 MVC + Supabase Postgres + Render auto-deploy on `main`). All prior tasks `done`.
- KAP stack (reuse, don't duplicate): `KapNewsService` — byCriteria disclosures, windowed fetch with 3-day slices to beat the 2,000-row cap, subject-based filtering proven for buybacks (exact subject `"Payların Geri Alınmasına İlişkin Bildirim"`, fields `disclosureClass=ODA`, `disclosureType=CA`), 15 min cache, silent degradation. `/Haberler` has "Tüm Bildirimler" | "Geri Alımlar" tabs; per-symbol KAP section on stock detail with "Geri Alım" badges; `KapCompanyService` for ownership.
- AI stack: `GeminiCommentaryService` (`gemini-3.5-flash`, thinking=minimal, maxOutputTokens 2048, finishReason checked, structured sections Özet/Trend/Göstergeler/Destek-Direnç/Riskler, 30 s cooldown + 5 min cache, mandatory disclaimer). Prompt currently = 60 OHLC rows + indicator values.
- Charts: own lightweight-charts on detail page fed by `/api/stocks/{symbol}/history` + `/api/stocks/{symbol}/indicators`; `IndicatorCalculator` = pure static functions over closes.
- Pattern-detection reference (IDEAS ONLY — GPL-3.0, copying code is forbidden): https://github.com/BennyThadikaran/stock-pattern

## Architecture Decisions (made by planner)

- **Dividend/capital-increase = same pattern as buybacks.** Empirical first step (observe real subject values over 14 days; likely candidates: "Kâr Payı Dağıtım İşlemlerine İlişkin Bildirim", subjects containing "Sermaye Artırımı"), then subject-based filters in `KapNewsService`. /Haberler tab row becomes: Tüm Bildirimler | Geri Alımlar | Temettü | Sermaye Artırımları (a dropdown/scrollable pill row on mobile). Per-symbol section gets matching badges ("Temettü", "Sermaye Art.").
- **KAP→AI = prompt enrichment, not new AI calls.** `GeminiCommentaryService` prompt gains a "Son şirket bildirimleri" block: up to 5 most recent disclosures for the symbol (date + subject + summary if present, one line each, hard char cap ~600) from the existing per-symbol KAP fetch. Instruct the model: mention disclosures only if relevant to price action; never invent contents beyond the given lines. If KAP returns nothing, omit the block entirely (prompt unchanged from today). Cache/cooldown semantics unchanged.
- **Pattern detection = pivot-point pipeline in pure C#.** New static `Services/PatternDetector.cs` (same style as `IndicatorCalculator`):
  1. Extract swing highs/lows via a pivot/zigzag pass (pivot = local extreme over k=5 bars each side; alternate highs/lows; minimum swing threshold ~2% to drop noise).
  2. Classify pivot sequences into patterns, each with begin/end indices, key price levels (neckline/support/resistance), and a completion state (`forming` = structure present, `confirmed` = breakout past the trigger level):
     - Double top / double bottom (two extremes within 2% of each other + intervening reversal ≥3%).
     - Head & shoulders + inverse (three highs/lows, middle extreme ≥3% beyond shoulders, shoulders within 3% of each other; neckline from the two intervening pivots).
     - Ascending / descending / symmetrical triangle (regression on last ≥4 alternating pivots: one side flat within tolerance, other side converging; both converging = symmetrical).
  3. Return at most the 3 most recent non-overlapping patterns for the requested range.
  Tunable constants live at the top of the file with comments; unit-test the classifier with synthetic OHLC fixtures (a small test project is now justified — `tests/BorsaAnaliz.Tests` with xunit, PatternDetector + IndicatorCalculator coverage).
- **Pattern surfacing**: `GET /api/stocks/{symbol}/patterns?range=` (same range validation as indicators). Detail page: a "Formasyonlar" card listing detected patterns (Turkish names, date range, state chip "oluşum aşamasında"/"teyit edildi", key level) + chart markers/price-lines on the existing lightweight-charts instance (markers at pattern pivots, dashed price line at neckline/trigger). Empty state: "Belirgin bir formasyon tespit edilmedi." Patterns are informational — wording must avoid buy/sell implication; card carries the standard disclaimer line.
- **Patterns→AI**: after Phase 3, append detected pattern names/states (one line) to the AI prompt alongside indicators.
- No DB changes anywhere in this task.

## Proposed Changes

One phase per Codex run; each leaves the app building, locally verified, then pushed (push = production deploy).

### Phase 1 — Temettü ve Sermaye Artırımı sekmeleri

- [x] Empirical step: pull 14 days of disclosures; record distinct subjects related to dividends and capital increases in the Implementation Report (exact strings, counts).
- [x] `KapNewsService`: generalize the buyback filter into a subject-set filter (one code path, three event kinds: buyback/dividend/capital-increase); `GetDividendsAsync`, `GetCapitalIncreasesAsync` (14-day window, 15 min cache each).
- [x] `/Haberler`: tab row Tüm | Geri Alımlar | Temettü | Sermaye Artırımları; mobile-friendly (scrollable pills); counts optional. Rows identical in structure to existing tabs.
- [x] Per-symbol KAP section: "Temettü" and "Sermaye Art." badges via the same subject sets.
- [x] Verify: each tab lists only its event type (cross-check 2 items per tab against kap.org.tr); default tab unchanged; empty states render; mobile pill row scrolls at 375 px.

### Phase 2 — KAP bildirimleri AI yorumuna

- [x] `GeminiCommentaryService`: accept optional recent-disclosure lines (injected by the controller from the existing per-symbol KAP fetch — service composition, no new HTTP in the AI service); add the "Son şirket bildirimleri" prompt block with the ~600-char cap and the relevance/no-invention instruction; omit block when empty.
- [x] Response format: allow an optional "## Şirket Haberleri" section between Göstergeler and Destek/Direnç ONLY when disclosures were provided; sanity check must not require it.
- [x] AI card meta line: append "ve son KAP bildirimlerine" when disclosures were included.
- [x] Verify: symbol WITH recent disclosures (pick from /Haberler) produces commentary referencing at least the existence of news, with disclaimer intact across 3 tries; symbol without disclosures produces the exact current format; token usage stays within limits (no MAX_TOKENS failures in tries).

### Phase 3 — Formasyon tespiti (pattern detection)

- [ ] `Services/PatternDetector.cs` per Architecture Decisions (pivots → classifiers → top-3 recent patterns). Pure functions; tunables documented.
- [ ] `tests/BorsaAnaliz.Tests` (xunit): synthetic-fixture tests — a clean double top, double bottom, H&S, inverse H&S, ascending triangle, plus a flat/noise series asserting NO detection; a couple of `IndicatorCalculator` regression tests while at it. Wire `dotnet test` into verification.
- [ ] `GET /api/stocks/{symbol}/patterns?range=` (catalog + range validation as indicators endpoint; 1 h cache aligned with history cache).
- [ ] Detail page "Formasyonlar" card + chart overlay (pivot markers, dashed trigger/neckline price line, pattern label); toggle to hide overlays; Turkish names: İkili Tepe, İkili Dip, Omuz-Baş-Omuz, Ters Omuz-Baş-Omuz, Yükselen/Alçalan/Simetrik Üçgen. Disclaimer line on the card.
- [ ] Patterns→AI: append detected patterns (names + state) as one prompt line; AI instructed to treat them as observations, not signals.
- [ ] Verify: `dotnet test` green; API returns sane JSON for AKBNK.IS/AAPL at 1Y (patterns or empty, no errors); overlays render and toggle without breaking existing chart controls; at least one real symbol visually shows a plausible detected pattern (screenshot in report); AI mentions a detected pattern when present.

## Acceptance Criteria

- /Haberler: four tabs, each correctly filtered, mobile-friendly.
- AI commentary incorporates recent company disclosures when they exist and never fabricates their contents; unchanged behavior when none exist.
- Pattern detection: unit-tested classifiers; API + detail-page card + chart overlays; Turkish naming; explicitly informational wording.
- No regressions in charts, AI cooldown/cache, KAP tabs, or performance; `dotnet build` and `dotnet test` clean; production healthy after each push.

## Verification

- `dotnet build BorsaAnaliz.sln`; `dotnet test` (new); local run + browser checks per phase (desktop + ~375 px).
- KAP cross-checks on kap.org.tr for the two new tabs.
- Live HTTP checks on https://borsa-analiz-aqr9.onrender.com after each push (tabs, patterns API, AI endpoint behavior).

## Assumptions

- Dividend/capital-increase subjects are as consistent as the buyback subject proved to be (confirmed empirically in Phase 1's first step).
- Pivot-based detection with the stated tolerances yields useful results on daily BIST/US data; thresholds are tunables, perfection is not the bar — clearly-formed patterns detected, noise rejected.
- Adding a test project does not affect the Docker image (test project excluded from publish; Dockerfile builds only the web project — verify this stays true).
- Gemini free-tier limits tolerate the slightly larger prompt (hard char cap keeps growth bounded).

## Open Questions

- None blocking.

## Risks

- KAP subject drift for the two new event types → same mitigation as buybacks: exact observed strings, unknown values excluded, silent degradation.
- Pattern false positives are reputationally worse than misses → conservative thresholds, top-3 cap, "oluşum/teyit" states, informational wording + disclaimer.
- Chart overlay code touches the existing chart JS — keep it additive (separate functions, feature-toggled) so a bug can't break the base chart; verify range switching still works with overlays on.
- Prompt growth could hit token limits → 600-char disclosure cap + one-line patterns; verify no MAX_TOKENS across tries.
- Push = production deploy; verify locally first; check Render Events if a deploy fails (transient 139 seen before).

## Rollback / Recovery

- Per-phase git commits; `git revert` + push. No migrations. Render dashboard can redeploy any earlier build. Pattern feature is fully additive — reverting Phase 3 leaves Phases 1–2 intact.

## Out Of Scope

- Parsing dividend amounts/dates or capital-increase ratios from disclosure bodies (metadata only).
- Pattern backtesting, success-rate stats, alerts/notifications on pattern formation.
- Intraday pattern detection (daily candles only).
- VCP/harmonic/flag patterns (only the seven listed).
- Supabase password rotation (user deferred it — remains on the hygiene list).

## Future Features (backlog — NOT part of this plan; do not implement)

- Pattern/price alerts (e-mail or in-app) for watchlist symbols.
- Dividend calendar view (needs body parsing).
- Pattern backtesting statistics.

## Notes For Codex

- One phase per run; update Status + Implementation Report; list every created/edited file; verify locally before push.
- GPL reference repo: concepts only — write all detection code from scratch; do not port function-by-function.
- Test project: add to solution, ensure `docker build` context/publish still targets only `src/BorsaAnaliz.Web` (check `.dockerignore`/Dockerfile; add `tests/` to `.dockerignore` if needed).
- Keep `PatternDetector` deterministic and culture-independent; document each tunable constant.
- Turkish UI strings; tr-TR display formatting; invariant parsing; no new secrets.
- Do not modify portfolio/watchlist code paths.

## Prior Tasks (archive)

1. **Build task** — done 2026-07-16 (market data, charts+indicators+TradingView, virtual portfolio, Gemini AI `gemini-3.5-flash`).
2. **Deploy task** — done 2026-07-16 (Supabase Postgres, RLS everywhere, Docker, GitHub `Farukckr/borsaAnaliz`, Render; live at https://borsa-analiz-aqr9.onrender.com). Pending user hygiene (deferred by user 2026-07-17): rotate Supabase DB password.
3. **UI redesign task** — done 2026-07-17 (finance theme, dashboard home, enriched portfolio analytics, AI quality fix). Hotfixes: BIST TradingView chart hidden, SVG logo/favicon.
4. **Market/watchlist/KAP task** — done 2026-07-17 (BIST 100/500/ABD tabs, watchlist w/ RLS, /Haberler + home panel, per-symbol KAP section).
5. **Buybacks/sectors/ownership task** — done 2026-07-17 (Geri Alımlar tab via exact KAP subject + 3-day window slicing; 550-symbol sector enrichment, 30 labels, server-side filter; ownership + subsidiaries via `company-detail/get-history` endpoints, `Data/kap-members.json` mapping, 24 h cache; all cross-checked against kap.org.tr).

## Implementation Report

### 2026-07-17 — Phase 1 complete

- Empirical KAP observation used five non-overlapping three-day requests covering 2026-07-04 through 2026-07-17 (2,412 distinct disclosures). Exact event subjects and counts were: `Kar Payı Dağıtım İşlemlerine İlişkin Bildirim` — 44; `Sermaye Artırımı - Azaltımı İşlemlerine İlişkin Bildirim` — 25; `Sermaye Artırımı veya Azaltımı Bildirimi` — 5. Two `Sermaye Artırımından Elde Edilecek - Edilen Fonun Kullanımına İlişkin Rapor` records were observed but intentionally excluded because they are follow-up fund-usage reports, not the capital event itself. All included event subjects were `ODA`; the first two exact subjects used `disclosureType=CA`, while the alternative capital subject used `disclosureType=ODA`.
- `KapDisclosure` now owns the exact subject constants and a shared event classifier for buyback, dividend, and capital-increase kinds. `KapNewsService` exposes `GetDividendsAsync` and `GetCapitalIncreasesAsync`; all three event methods share one windowed 14-day source result, then keep per-kind 15-minute caches. This preserves the 2,000-row-cap mitigation while reducing a full three-tab load to five upstream requests rather than fifteen. Failures still degrade to empty lists.
- `/Haberler` now has four tabs with tab-specific headings, descriptions, and empty states. The pill row is non-wrapping, horizontally scrollable, touch-friendly, and keeps each label on one line at narrow widths. Existing row/search rendering and the default two-day tab are unchanged.
- Per-symbol KAP rows use the same classifier to add `Temettü` and `Sermaye Art.` badges alongside the existing `Geri Alım` badge.
- Files changed: `.agents/PLAN.md`, `Controllers/NewsController.cs`, `Models/KapDisclosure.cs`, `Services/IKapNewsService.cs`, `Services/KapNewsService.cs`, `ViewModels/NewsIndexViewModel.cs`, `Views/News/Index.cshtml`, `Views/Stocks/Details.cshtml`, `wwwroot/css/site.css`.
- Verification: Release build passed with 0 warnings/errors. A real-KAP integration check returned 54 buybacks, 44 dividends, and 30 capital events; every row matched only its declared event set. The three calls shared exactly five upstream requests and repeated calls added none, confirming the cache path. Official KAP disclosure pages 1634548 and 1634534 (dividend) plus 1634576 and 1634471 (capital) all returned HTTP 200. A full local HTTP browser run could not start because the configured Supabase connection stalled during startup migration, but Razor compilation covered all empty-state and tab branches. Render deployment for commit `67f50a1` succeeded; live `/Haberler` returned 200 for all four tabs with 54/44/30 correctly filtered event rows, and PENGD/ARMGD/BAYRK details showed the expected badges. Headless Edge checks at 1440 px and 375 px passed; the mobile capture showed the non-wrapping pill row with horizontal scrolling and no overlap.
- Status remains `in-progress` because Phases 2 and 3 are intentionally deferred to later runs.

### 2026-07-17 — Phase 2 complete

- `StocksController` now reuses the existing per-symbol 14-day KAP fetch for BIST AI requests and formats up to five latest records as invariant date + subject + optional summary lines. US symbols and unavailable/empty KAP results pass no disclosure context. No KAP HTTP dependency was added to the AI service.
- `GeminiCommentaryService` accepts optional prepared disclosure lines, normalizes them, and caps the complete disclosure list at 600 characters. The prompt tells Gemini to mention only price-relevant disclosures, forbids invention beyond supplied lines, and permits an optional `## Şirket Haberleri` section only between `Göstergeler` and `Destek ve Direnç`. With no lines, the prompt's existing fixed format remains unchanged; the sanity check still requires only `## Özet`.
- Successful response cache entries retain whether KAP context was included. The stock-detail AI meta line appends `ve son KAP bildirimlerine` for both fresh and cached enriched responses. Existing 30-second cooldown, five-minute response cache, token limit, and failure behavior are unchanged.
- Files changed: `.agents/PLAN.md`, `Controllers/StocksController.cs`, `Models/AiCommentaryResponse.cs`, `Services/GeminiCommentaryService.cs`, `Services/IAiCommentaryService.cs`, `Views/Stocks/Details.cshtml`.
- Verification: Release build passed with 0 warnings/errors and `git diff --check` passed. A disposable local harness (removed after verification) confirmed the no-disclosure prompt omits both KAP prompt additions, while an eight-line input is reduced to four complete lines / 596 characters. The real per-symbol KAP service returned four current PENGD.IS disclosures, including a dividend disclosure. Real Gemini calls produced multiple successful PENGD.IS commentaries that explicitly mentioned the supplied disclosures, retained the disclaimer, and used the optional company-news heading only when relevant; a successful AAPL/no-disclosure call returned exactly `Özet`, `Trend`, `Göstergeler`, `Destek ve Direnç`, and `Riskler`. No call produced `MAX_TOKENS`. Repeated verification eventually exhausted the provider's short-term quota (general HTTP failure), so three consecutive successful live calls could not be recorded; this was external-rate-limit behavior, not a token or prompt-format failure.
- Status remains `in-progress` because Phase 3 is intentionally deferred to the next run.
