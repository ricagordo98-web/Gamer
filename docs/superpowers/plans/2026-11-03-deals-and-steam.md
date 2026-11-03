# Deals and Steam Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Compare game prices across six stores (best offer highlighted), and show the Steam wishlist and library, all configurable from Settings.

**Architecture:** Gamer.Api scrapes stores in the background and caches one `PriceSnapshot` per (title, store) in SQLite. Each store has an `IStoreScraper` built from a URL template, an offer parser and a fetcher chain (HTTP, then Playwright). Steam data comes from the official Web API. The Blazor UI (Gamer.Ui) reads everything through `IGamerApi`.

**Tech Stack:** .NET 10, EF Core SQLite, minimal APIs, FluentValidation, AngleSharp, Microsoft.Playwright (optional last resort), Blazor, xUnit + FluentAssertions + NSubstitute.

Spec: `docs/superpowers/specs/2026-11-03-deals-and-steam-design.md`

---

## File map

Contracts (`src/Gamer.Contracts/Deals`, `src/Gamer.Contracts/Steam`): `StoreDto`, `StoreUpdateRequest`, `SteamSettingsDto`, `SteamSettingsRequest`, `SteamSyncResultDto`, `SteamLibraryItemDto`, `WishlistItemDto`, `OfferDto`, `StoreStatusDto`, `DealDto`, `TrackRequest`.

Domain (`src/Gamer.Api/Domain`): `Store`, `AppSetting`, `SteamLibraryItem`, `WishlistItem`, `TrackedGame`, `PriceSnapshot`.

Deals feature (`src/Gamer.Api/Features/Deals`):
- `TitleMatcher` — normalisation and similarity.
- `PriceParser` — "12,49 €" / "$9.99" to decimal.
- `StoreOffer`, `ScrapeResult`, `IStoreScraper`, `FetchingScraper` — scraper contract and fetcher chain.
- `IPageFetcher`, `HttpPageFetcher`, `BrowserPageFetcher`, `ScrapeExceptions` — fetching.
- `Parsers/` — `SteamSearchParser`, `HumbleSearchParser`, `HtmlOfferParser`.
- `ScraperCatalog` — the six store definitions.
- `DealBuilder` — best offer selection (pure).
- `IDealService`/`DealService`, `PriceRefreshService`, `DealEndpoints`.

Steam feature (`src/Gamer.Api/Features/Steam`): `ISteamSettingsService`/`SteamSettingsService`, `ISteamClient`/`SteamClient`, `SteamSyncService`, `SteamEndpoints`.

Stores feature (`src/Gamer.Api/Features/Stores`): `StoreEndpoints`.

UI (`src/Gamer.Ui`): `IGamerApi` + `GamerApiClient` additions, `Pages/Deals.razor`, `Pages/Steam.razor`, `Pages/Settings.razor` sections, `MainLayout.razor` nav, `Icon.razor`, `gamer.css`.

Tests (`tests/Gamer.Tests/Api`): `TitleMatcherTests`, `PriceParserTests`, `StoreParsersTests`, `DealBuilderTests`, `FetchingScraperTests`, `DealsEndpointsTests`.

## Tasks

### Task 1: Pure logic with tests
- [ ] Write `TitleMatcherTests`, `PriceParserTests`, `DealBuilderTests` (failing).
- [ ] Implement `TitleMatcher`, `PriceParser`, `StoreOffer`, `DealBuilder`.
- [ ] Run `dotnet test --filter "FullyQualifiedName~TitleMatcher|PriceParser|DealBuilder"`; expect PASS.
- [ ] Commit.

### Task 2: Scraping infrastructure and parsers
- [ ] Add AngleSharp and Microsoft.Playwright packages.
- [ ] Write `StoreParsersTests` and `FetchingScraperTests` (failing).
- [ ] Implement fetchers, parsers, `FetchingScraper`, `ScraperCatalog`.
- [ ] Run the filtered tests; expect PASS.
- [ ] Commit.

### Task 3: Domain, migration, Steam and stores API
- [ ] Add entities, DbContext config, seed the six stores; generate the migration with `dotnet ef`.
- [ ] Implement Steam settings/client/sync services and `StoreEndpoints`, `SteamEndpoints`, validators.
- [ ] Add contracts. Write endpoint tests for stores and Steam settings; run; expect PASS.
- [ ] Commit.

### Task 4: Deals service, refresh job and endpoints
- [ ] Implement `DealService`, `PriceRefreshService`, `DealEndpoints`, register everything in `ServiceCollectionExtensions`/`Program`.
- [ ] Write `DealsEndpointsTests` with a fake scraper; run; expect PASS.
- [ ] Commit.

### Task 5: UI
- [ ] Extend `IGamerApi`/`GamerApiClient`; add Deals and Steam pages, Settings sections, navigation, icons, CSS.
- [ ] `dotnet build` the solution (excluding MAUI if its workload is missing) and run all tests.
- [ ] Commit.

## Self-review

Spec coverage: store/Steam settings (T3), Steam sync (T3), scrapers with strategy chain and graceful failure (T2), matching and best price (T1), snapshots, background refresh and manual refresh (T4), UI (T5), tests (all tasks). Known deviation from the spec: `robots.txt` is not fetched; politeness is a per-host throttle plus an identifiable User-Agent.
