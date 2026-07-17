# Borsa Analiz — Stock Analysis & Virtual Portfolio Platform

Full-stack stock analysis web application for **Borsa Istanbul (BIST)** and **US markets**: live quotes, technical indicators, KAP corporate disclosures, per-user watchlists, virtual portfolio simulation, and Gemini-powered AI chart commentary — built with ASP.NET Core.

> **Live demo:** [borsa-analiz-aqr9.onrender.com](https://borsa-analiz-aqr9.onrender.com) · 🇹🇷 [Türkçe README](README.tr.md)

The application is for educational and analytical purposes. Displayed data may be delayed; nothing in this project constitutes investment advice.

## Features

### Market & stock analysis

- ~550-symbol catalog: full **BIST 500** (official XU500 constituents) and 50 US large caps, browsable as **BIST 100 / BIST 500 / US** tabs with server-side pagination
- Sector data for every stock (30 Turkish sector groups sourced from KAP's company directory) with sector filtering across all lists
- Symbol search, market filters, price/change sorting, and a market-overview home page (XU100, USD/TRY, S&P 500, NASDAQ snapshots + top movers)
- Prices, daily change, and OHLCV history via Yahoo Finance with layered caching and graceful degradation
- Candlestick, volume, RSI, MACD, SMA, EMA, and Bollinger charts (TradingView Lightweight Charts, indicators computed in C#)
- TradingView technical-analysis gauge integration

### KAP corporate disclosures

- Live disclosure feed from KAP (Turkey's Public Disclosure Platform) on a dedicated news page with a prominent home-page panel
- Dedicated **share buyback** tracking tab (exact-subject filtering, cross-checked against official KAP records)
- Per-company disclosure history on every BIST stock page
- **Ownership structure & subsidiaries** on BIST detail pages, fetched from KAP company endpoints with 24 h caching

### Watchlist & virtual portfolio

- Per-user **watchlist** with one-click star toggles across lists and detail pages
- Multiple virtual portfolios per user with ₺100,000 starting cash, backed by ASP.NET Core Identity
- Buy/sell at live prices with trade preview (estimated cost, post-trade cash, owned quantity) and balance/position validation
- Average-cost accounting: cost basis, realized and unrealized P/L, daily change, position weights, and an allocation donut chart
- Transaction ledger with per-symbol expandable history
- Portfolio value-over-time chart reconstructed from the ledger and daily closes (up to 1 year)

### AI chart commentary

- Turkish technical commentary via `gemini-3.5-flash`, grounded in the last 60 days of OHLC data and computed indicators
- Structured output (Summary / Trend / Indicators / Support-Resistance / Risks) with truncation detection — partial responses are never shown as finished analysis
- Per-user 30 s cooldown, 5 min response cache, authenticated access only, and a mandatory investment-advice disclaimer on every response

## Tech Stack

| Layer | Technology |
| --- | --- |
| Application | ASP.NET Core 8 MVC, C# |
| Authentication | ASP.NET Core Identity |
| Data access | Entity Framework Core 8, Npgsql |
| Database | PostgreSQL / Supabase (Row Level Security enabled on all tables) |
| Market data | Yahoo Finance Chart API |
| Disclosures | KAP (kap.org.tr) JSON endpoints |
| AI | Google Gemini API (`gemini-3.5-flash`) |
| UI | Razor Views, Bootstrap 5, custom theme |
| Charts | Lightweight Charts, Chart.js, TradingView Widgets |
| Deployment | Docker, Render Blueprint (auto-deploy on `main`) |

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PostgreSQL 14+ or a Supabase project
- A Gemini API key for AI commentary (all other features work without it)
- No API key needed for Yahoo Finance or KAP

## Local Setup

1. Clone and enter the repository:

   ```bash
   git clone https://github.com/Farukckr/borsaAnaliz.git
   cd borsaAnaliz
   ```

2. Restore dependencies:

   ```bash
   dotnet restore BorsaAnaliz.sln
   ```

3. Configure secrets with .NET user-secrets:

   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=<host>;Port=5432;Database=<database>;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true" --project src/BorsaAnaliz.Web
   dotnet user-secrets set "Ai:ApiKey" "<gemini-api-key>" --project src/BorsaAnaliz.Web
   ```

   Never put real connection strings or API keys into `appsettings.json`, commits, or docs. The root `.env` file is gitignored.

4. Build and run:

   ```bash
   dotnet build BorsaAnaliz.sln --configuration Release
   dotnet run --project src/BorsaAnaliz.Web
   ```

5. Open `http://localhost:5122`.

Pending EF Core migrations are applied automatically at startup; the database account needs schema-modification rights.

## Configuration

| .NET key | Environment variable | Description |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Required PostgreSQL connection string |
| `Ai:ApiKey` | `Ai__ApiKey` | Gemini API key for AI commentary |
| `Ai:Provider` | `Ai__Provider` | Default: `Gemini` |
| `Ai:Model` | `Ai__Model` | Default: `gemini-3.5-flash` |
| `ASPNETCORE_ENVIRONMENT` | `ASPNETCORE_ENVIRONMENT` | `Development` locally, `Production` in prod |

Without an AI key, market/chart/portfolio features keep working; only the AI card shows a configuration notice.

## Running with Docker

```bash
docker build -t borsa-analiz .
docker run --rm --env-file .env -p 8080:8080 borsa-analiz
```

with an untracked `.env` file:

```dotenv
PORT=8080
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true
Ai__ApiKey=<gemini-api-key>
```

## Deployment (Render)

[`render.yaml`](render.yaml) defines a Docker-based Render web service (Frankfurt region, `/` health check, auto-deploy on `main`, secrets entered in the dashboard). See [`docs/DEPLOY.md`](docs/DEPLOY.md) for the step-by-step guide.

## Key Routes & APIs

| Route | Access | Description |
| --- | --- | --- |
| `/` | Public | Market overview, KAP panel, top movers |
| `/Stocks` | Public | Stock catalog (BIST 100 / BIST 500 / US / Watchlist tabs) |
| `/Stocks/Details/{symbol}` | Public | Charts, indicators, disclosures, ownership, AI card |
| `/Haberler` | Public | KAP disclosure feed with buyback tab |
| `/api/stocks/{symbol}/history` | Public | OHLCV history |
| `/api/stocks/{symbol}/indicators` | Public | Technical indicator series |
| `/api/stocks/{symbol}/ai-comment` | Authenticated | Gemini technical commentary |
| `/api/watchlist/toggle` | Authenticated | Star/unstar a symbol |
| `/Portfolio` | Authenticated | Virtual portfolios |
| `/api/portfolios/{id}/trade-preview` | Owner | Buy/sell preview |
| `/api/portfolios/{id}/value-history` | Owner | Daily portfolio value series |

## Project Structure

```text
borsaAnaliz/
├── src/BorsaAnaliz.Web/
│   ├── Controllers/       MVC and JSON API endpoints
│   ├── Data/              DbContext, migrations, symbol catalog, KAP member map
│   ├── Models/            Market, portfolio, KAP, and API models
│   ├── Services/          Yahoo, Gemini, KAP, indicators, portfolio logic
│   ├── ViewModels/        Razor page models
│   ├── Views/             Turkish MVC UI
│   └── wwwroot/           CSS, JavaScript, client libraries
├── docs/DEPLOY.md         Render deployment guide
├── Dockerfile
├── render.yaml
└── BorsaAnaliz.sln
```

## Data & Security Notes

- Portfolio, watchlist, and transaction queries are scoped to the authenticated user; Supabase Row Level Security is enabled on every table.
- Anti-forgery validation on AI, watchlist, and trade POST endpoints; Data Protection keys persist in PostgreSQL so sessions survive redeploys.
- Yahoo quotes cache for 60 s; history and portfolio series for 1 h; KAP feeds for 5–30 min; company profiles for 24 h.
- All external data (Yahoo, KAP) degrades gracefully — outages render empty states, never errors.
- AI output is grounded only in the provided OHLC/indicator data; it is not a news or fundamentals source.

## Disclaimer

This project does not provide investment advisory services. Prices, indicators, AI commentary, and virtual portfolio results must not be used as the sole basis for real investment decisions.

## License

[MIT](LICENSE)
