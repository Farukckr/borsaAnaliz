# Plan

## Status

done — 2026-07-17 (all 3 phases implemented and verified)

## Task Type

Feature (3 sub-features: KAP buyback tracking, sector data, company ownership structure).

## User Request

Turkish, summarized (feature suggestions from the user's friend, adopted by the user): (1) a dedicated buyback ("geri alım") section, (2) sector data for stocks, (3) ownership structure ("ortaklık yapısı") and subsidiaries ("bağlı ortaklıklar") per company. Planner ordered them by effort/risk: buybacks → sectors → ownership.

## Goal

Users can see recent share-buyback disclosures in their own tab on /Haberler, filter and read stocks by sector everywhere lists appear, and view a BIST company's ownership structure and subsidiaries on its detail page.

## Current State

- Live production app (ASP.NET Core 8 MVC + Supabase Postgres + Render auto-deploy on push to `main`). All prior tasks `done`.
- Catalog: `Data/symbols.json` — ~500 BIST entries with `indices: ["XU100","XU500"]` tags + 50 US entries; loaded by `JsonStockCatalogService`; stocks page has BIST 100 / BIST 500 / ABD / Takip Listem tabs (BIST 500 paginated 50/page).
- KAP integration EXISTS and works: `IKapNewsService`/`KapNewsService` (byCriteria endpoint, 2-day window, 5 min cache, defensive parsing, silent degradation) powers `/Haberler`, the home panel, AND a per-symbol "Şirket Bildirimleri (KAP)" section on stock detail via `GetForSymbolAsync` (14-day window, 30 min per-symbol cache). Reuse this service and its row styles — do not build a second KAP client.
- Stock detail page: quote header, own charts + indicators, TradingView gauge (BIST) / gauge+chart (US), AI card, per-symbol KAP section.
- All tables have RLS; any new table's migration must embed `ALTER TABLE ... ENABLE ROW LEVEL SECURITY;` (no new tables are expected in this task).
- KAP endpoint knowledge so far: disclosures via `POST/GET https://www.kap.org.tr/tr/api/disclosure/...`; the KAP site is a Next.js SPA — company pages (with ownership tables) are fed by JSON endpoints of the same API family. Company list/oid mapping endpoints exist (e.g. member list used by kap.org.tr company directory). Exact shapes must be observed at implementation time, not assumed.

## Architecture Decisions (made by planner)

- **Buybacks = filtered KAP disclosures.** No new data source. Buyback disclosures carry recognizable subject/template names (e.g. "Pay Geri Alım İşlemleri" / subjects containing "Geri Alım"). First step is empirical: pull a 7–14 day window, log distinct category/subject values, then implement the filter on whatever field reliably identifies buybacks. Extend `IKapNewsService` with `GetBuybacksAsync(days)` (14-day window, 15 min cache).
- **Sector source = static catalog enrichment.** Add `"sector"` (Turkish label, e.g. "Bankacılık", "Havacılık", "GYO") to every entry in `symbols.json`. BIST sectors from KAP's company directory (each company card lists its sector) or Borsa İstanbul's official sector index constituent lists — whichever is cleanly obtainable; US sectors are well-known for our 50 large caps (static). Sector is display/filter metadata — no live fetching, no schema/DB change.
- **Ownership = KAP company-info JSON endpoints.** Per-symbol company data (ortaklık yapısı %, bağlı ortaklıklar) fetched from KAP's company API: requires a one-time symbol→KAP member id (oid) mapping (obtainable from KAP's member list endpoint; persist it as a generated `Data/kap-members.json` or as fields merged into symbols.json — implementer's choice, document it). `IKapCompanyService` with 24 h cache per symbol, JSON endpoints ONLY (no HTML scraping), silent degradation. If usable JSON endpoints cannot be found/verified, STOP Phase 3 and report blocked with findings — do not scrape HTML.
- **UI language**: Turkish throughout; percentages with tr-TR formatting; tables reuse existing theme components.
- No AI/portfolio/watchlist changes in this task.

## Proposed Changes

One phase per Codex run; each leaves the app building, locally verified, then pushed (push = production deploy).

### Phase 1 — Geri Alımlar (buyback tab on /Haberler)

- [x] Empirical step: fetch a 7–14 day disclosure window once; record (in the Implementation Report) the distinct category/subject/template values and which field identifies buyback disclosures.
- [x] `IKapNewsService.GetBuybacksAsync(int days = 14)`: filters buyback disclosures, 15 min cache, defensive as existing methods.
- [x] `/Haberler` gets tabs: "Tüm Bildirimler" (default, current behavior) | "Geri Alımlar". Buyback rows: relative time, company (linked to our detail page when matched), subject, KAP source link. If the disclosure title/subject exposes share count/price cheaply, show it; do NOT build a parser for disclosure bodies.
- [x] Stock detail per-symbol KAP section: buyback items get a small distinguishing badge ("Geri Alım") if trivially detectable with the same filter.
- [x] Verify: buyback tab lists only buyback disclosures over 14 days (cross-check 2–3 against kap.org.tr manually); empty state renders when filter yields nothing; /Haberler default tab unchanged.

### Phase 2 — Sektör verileri

- [x] Enrich `symbols.json`: add `"sector"` to all ~550 entries. BIST from KAP company directory or BIST sector lists; US statically. Validate: no entry without sector; keep a consistent Turkish label set (aim for ~20–30 labels, not 100 variants).
- [x] `StockSymbol` model + catalog service: parse `sector`; add `Sectors()` helper (distinct, sorted).
- [x] Stocks page: sector column (after name) + sector dropdown filter working alongside existing tabs/search/sort (server-side filter param `sector=` so it composes with BIST 500 pagination).
- [x] Stock detail header: sector badge next to market badge, linking to the filtered list.
- [x] Optional if trivial: home page "Sektör görünümü" mini-card — evaluated and intentionally skipped because it would expand this phase into dashboard aggregation/UI work; no extra quote fan-out was introduced.
- [x] Verify: filter by 3 different sectors on each tab incl. a paginated BIST 500 page; detail badge navigates correctly; no sector shows "-".

### Phase 3 — Ortaklık yapısı ve bağlı ortaklıklar

- [x] Discovery step (time-boxed): identify KAP's JSON endpoints for (a) member/company list with ids, (b) per-company general info / ownership ("Ortaklık Yapısı" percentages) and subsidiaries ("Bağlı Ortaklıklar"). Record endpoint paths + response shapes in the report. If not achievable with JSON endpoints → set Status blocked with findings; do NOT scrape HTML.
- [x] Symbol→KAP id mapping generated once into a data file; loading follows the symbols.json pattern.
- [x] `IKapCompanyService`/`KapCompanyService`: `GetCompanyProfileAsync(symbol)` → ownership rows (holder name, share %), subsidiaries list (name, optional activity field); 24 h cache; 10 s timeout; null on any failure.
- [x] Stock detail (BIST only): "Ortaklık Yapısı" section — ownership table (% with tr-TR formatting, sorted desc, "Diğer/Halka açık" rows as delivered by KAP) + "Bağlı Ortaklıklar" list; collapsible if long; section hidden entirely when service returns null. Placed after the KAP disclosures section.
- [x] Verify: 3 large caps (e.g. AKBNK.IS, THYAO.IS, ASELS.IS) show plausible ownership tables cross-checked against kap.org.tr company pages; a symbol with no data hides the section; US stocks never show it; repeated loads hit the 24 h cache.

## Acceptance Criteria

- /Haberler has a working "Geri Alımlar" tab listing only buyback disclosures with KAP source links.
- Every stock shows a sector; lists are filterable by sector on all tabs including paginated BIST 500.
- BIST large-cap detail pages show ownership structure + subsidiaries sourced from KAP, matching kap.org.tr.
- All KAP-derived features degrade silently (hidden/empty), never error pages.
- Existing features untouched; `dotnet build` clean; production healthy after each push.

## Verification

- `dotnet build BorsaAnaliz.sln`; local run + browser checks per phase (desktop + ~375 px).
- Manual cross-checks against kap.org.tr for buybacks (Phase 1) and ownership (Phase 3).
- Live HTTP checks on https://borsa-analiz-aqr9.onrender.com after each push; specifically confirm KAP endpoints respond from Render's IPs (they already do for disclosures).

## Assumptions

- Buyback disclosures are reliably identifiable from category/subject metadata (to be confirmed empirically in Phase 1's first step).
- A stable sector label per company is acceptable as static data; sector changes are rare and can be maintained with the catalog.
- KAP company-info JSON endpoints are reachable and stable enough for a 24 h-cached read; Phase 3 explicitly bails out (blocked) if not.
- No new DB tables needed (all new data is static files or cached external reads).

## Open Questions

- None blocking. (Phase 1 and 3 both start with an observe-then-implement step instead of assumptions.)

## Risks

- KAP metadata drift: category/subject naming may change → filters must tolerate unknown values (fall back to excluding, never crashing).
- Ownership endpoint may differ per company type (banks, holdings) → parse defensively; partial data (ownership without subsidiaries) should still render what exists.
- Sector labeling for ~500 small caps is the largest manual-data risk → prefer an authoritative source dump over hand-typing; validate distinct-label count.
- Push = production deploy; verify locally first (one past deploy died with transient status 139 — if a deploy fails, check Render Events before re-pushing).

## Rollback / Recovery

- Per-phase git commits; `git revert` + push redeploys previous state. No migrations to unwind. Render dashboard can redeploy any earlier successful build.

## Out Of Scope

- Parsing buyback disclosure bodies/attachments for exact amounts (only metadata-level info).
- Financial statements, dividends, capital increases (sermaye artırımı) — future candidates.
- Sector indices as chartable instruments; sector-based AI commentary.
- US ownership data (KAP is BIST-only; US 13F etc. is a different world).
- Chart pattern detection (still in backlog).

## Future Features (backlog — NOT part of this plan; do not implement)

- **Chart pattern detection (formasyon tespiti)**: 3–4 classic patterns in C# alongside `IndicatorCalculator`; reference (ideas only, GPL — no code copying): https://github.com/BennyThadikaran/stock-pattern.
- **KAP → AI**: include recent disclosures (incl. buybacks) in the Gemini commentary prompt.
- **Temettü/sermaye artırımı takibi**: same KAP-filter pattern as buybacks.

## Notes For Codex

- One phase per run; update Status + Implementation Report; list every created/edited file; verify locally before push.
- Reuse `KapNewsService` HTTP client/config for the company service (same base URL, User-Agent, timeout patterns).
- Record observed KAP endpoint shapes in the Implementation Report — they are the documentation for future maintenance.
- Turkish UI strings; tr-TR display formatting; invariant culture parsing; no new secrets (KAP is keyless).
- Do not touch AI, portfolio, watchlist, or chart code paths.

## Prior Tasks (archive)

1. **Build task** — done 2026-07-16 (market data, charts+indicators+TradingView, virtual portfolio, Gemini AI `gemini-3.5-flash`).
2. **Deploy task** — done 2026-07-16 (Supabase Postgres, RLS on all tables, Docker, GitHub `Farukckr/borsaAnaliz`, Render; live at https://borsa-analiz-aqr9.onrender.com). Pending user hygiene: rotate Supabase DB password.
3. **UI redesign task** — done 2026-07-17 (finance theme, home dashboard, enriched portfolio analytics, AI quality fix). Hotfixes: BIST TradingView chart hidden, SVG logo/favicon.
4. **Market/watchlist/KAP task** — done 2026-07-17 (BIST 100/500/ABD tabs from official XU500 list, per-user watchlist w/ RLS, /Haberler + home KAP panel). Direct follow-up fix: per-symbol "Şirket Bildirimleri (KAP)" section on stock detail (`GetForSymbolAsync`, 14-day window).

## Implementation Report

### 2026-07-17 — Phase 1 complete

- Empirical KAP observation: `POST /tr/api/disclosure/members/byCriteria` returned a maximum of 2,000 rows for a broad 14-day request. Buybacks consistently had `disclosureClass = ODA`, `disclosureType = CA`, and `subject = "Payların Geri Alınmasına İlişkin Bildirim"`; `summary` varied between individual transactions and program start/end notices. The response has no template-name field. The exact `subject` field is therefore the filter key.
- To avoid losing older buybacks behind the 2,000-row response cap, the 14-day request is split into non-overlapping three-day windows, merged, de-duplicated by disclosure id, filtered by the observed subject, and cached for 15 minutes. Any request/parse failure returns an empty list.
- `/Haberler` now has "Tüm Bildirimler" and "Geri Alımlar" tabs. Existing rows and search are reused; summaries are shown when KAP exposes them, without parsing disclosure bodies. Per-symbol KAP rows mark matching disclosures with "Geri Alım".
- Files changed: `.agents/PLAN.md`, `Controllers/NewsController.cs`, `Models/KapDisclosure.cs`, `Services/IKapNewsService.cs`, `Services/KapNewsService.cs`, `ViewModels/NewsIndexViewModel.cs`, `Views/News/Index.cshtml`, `Views/Stocks/Details.cshtml`.
- Verification: Release build passed with 0 warnings/errors; local default and buyback tabs returned 200; all 54 current buyback rows had the exact observed subject and official KAP links; JANTS/AVGYO/BIMAS disclosure ids 1634684/1634672/1634671 returned 200 at kap.org.tr; BIMAS detail showed the badge; a forced KAP connection failure returned the buyback empty state with HTTP 200; Edge headless checks were captured at desktop and narrow mobile widths.
- Status remains `in-progress` because Phases 2 and 3 are intentionally deferred to later runs.

### 2026-07-17 — Phase 2 complete

- Source: the official KAP `https://www.kap.org.tr/tr/Sektorler` company directory embeds current company records with `sectorName`, `mkkMemberOid`, `stockCode`, and `title`. Multi-code records were split and all 500 BIST catalog symbols matched. KAP's 48 detailed labels were normalized into user-facing Turkish groups; the 50 fixed US large caps received static labels from the same vocabulary.
- Catalog result: all 550 entries have a non-empty `sector`; there are 30 distinct labels. No runtime sector fetch or database/schema change was added.
- `StockSymbol` and `IStockCatalogService` now expose sector metadata and a culture-aware, distinct sorted sector list. The stocks controller validates `sector=`, filters before quote fan-out and pagination, and supplies list-specific sector options.
- The stocks page has a server-side sector selector, a sector column after company name, sector-aware search text, and pagination links that retain the active sector. Detail pages show a linked sector badge targeting BIST 500 or the US list.
- Files changed: `.agents/PLAN.md`, `Controllers/StocksController.cs`, `Data/symbols.json`, `Models/StockSymbol.cs`, `Services/IStockCatalogService.cs`, `Services/JsonStockCatalogService.cs`, `ViewModels/StocksIndexViewModel.cs`, `Views/Stocks/Details.cshtml`, `Views/Stocks/Index.cshtml`, `wwwroot/css/site.css`.
- Verification: Release build passed with 0 warnings/errors; catalog validation reported 550 entries, 0 missing sectors, and 30 labels; three sectors were checked on each public tab; BIST 500 `page=2` with a sector safely clamped after server-side filtering; AKBNK and AAPL badges linked to the correct filtered lists; anonymous watchlist still challenged with HTTP 302; Edge headless desktop and narrow mobile layouts passed after stacking the mobile filter controls.
- Status remains `in-progress` because Phase 3 is intentionally deferred to the next run.

### 2026-07-17 — Phase 3 complete

- KAP JSON discovery found `GET /tr/api/member/filter/{search}` for member resolution. An exact stock-code search returns an array whose usable fields are `companyCode`, `mkkMemberOid`, `title`, and `permaLink`. The generated `Data/kap-members.json` contains 358 mappings observed before KAP's discovery-session rate limit; the same official endpoint resolves unmapped/new catalog symbols at runtime, and the resulting profile (including null) is cached for 24 hours.
- Company data comes only from `GET /tr/api/company-detail/get-history/{mkkMemberOid}/{itemKey}/N`; no runtime HTML parsing was added. Ownership uses item key `kpy41_acc5_sermayede_dogrudan`, whose history items expose `creationDate` and a `value` array with `shareholder`, `shareInCapital`, `ratioInCapital`, and `votingRightRatio`. Subsidiaries use `kpy41_acc7_bagli_ortakliklar`, whose value rows expose `companyTitle`, `scopeOfActivitiesOfCompany`, `ratioOfCapitalShareOfCompany`, and `relationWithTheCompany` (plus capital/currency fields not needed by the UI). The service selects the newest dated item defensively.
- `IKapCompanyService`/`KapCompanyService` loads the static mapping once, uses the shared KAP client settings and 10-second timeout, fetches ownership and subsidiary items concurrently, tolerates one missing item, caches each symbol for 24 hours, and returns null on total failure or empty data. Ownership excludes aggregate `TOPLAM/TOTAL`, preserves `DİĞER`, parses Turkish/invariant decimals, and sorts descending. The subsidiary list includes only rows identified by KAP as `Bağlı Ortaklık`, excluding ordinary iştirak rows.
- BIST detail pages now render the new section after company disclosures. Percentages use Turkish formatting, long subsidiary lists use a Bootstrap collapse control, missing profiles hide the whole section, and US details never call or render the KAP company feature.
- Files changed: `.agents/PLAN.md`, `Controllers/StocksController.cs`, `Data/kap-members.json`, `Models/KapCompanyProfile.cs`, `Program.cs`, `Services/IKapCompanyService.cs`, `Services/KapCompanyService.cs`, `ViewModels/StockDetailsViewModel.cs`, `Views/Stocks/Details.cshtml`.
- Verification: Release build passed with 0 warnings/errors. A deterministic JSON integration check confirmed two ownership rows sorted 74.2/25.8, one subsidiary with an iştirak excluded, exactly two upstream requests across two AKBNK loads (24-hour cache hit), and no request/profile for AAPL. AKBNK, THYAO, and ASELS mapping ids were verified; their ownership/subsidiary values were cross-checked against the current official KAP pages (for example THYAO: Türkiye Varlık Fonu 49.12%, Diğer 50.88%; ASELS: TSKGV 74.2%, Diğer 25.8%). KOPOL's empty JSON fixture hid the section, and AAPL rendered no KAP company section.
- Final status is `done`; all three planned phases are complete.
