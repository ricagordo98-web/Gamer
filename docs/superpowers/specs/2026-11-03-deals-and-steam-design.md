# Deals comparison and Steam integration — design

Date: 2026-11-03

## Goal

Let the user compare game prices across several stores (best offer highlighted), see their Steam wishlist and Steam library, and configure everything from Settings. Prices are scraped server-side, cached in SQLite and refreshed in the background.

## Scope

- Stores: Steam, Humble Bundle, Fanatical, Gamesplanet, Green Man Gaming, Instant Gaming.
- Tracked games: Steam wishlist (automatic) plus games the user pins after a manual search.
- Currency/region: EUR / PT by default, stored per store.
- Out of scope: price history charts, alerts, importing the Steam library into the backlog, non-Steam libraries.

## Delivery phases (separate commits)

1. Store settings, Steam settings and Steam sync (library and wishlist).
2. Scrapers, matching, price snapshots, background refresh.
3. UI: Deals page, Steam page, Settings sections.

## 1. Store and Steam settings

- Table `Store`: Id, Name, BaseUrl, IsEnabled, Region, Currency. Seeded with the six stores by migration.
- Table `AppSetting` (key/value) for Steam API key, SteamID64 and refresh interval in hours.
- The Steam API key is write-only from the client; the API only returns a "configured" flag.
- Settings page gains "Stores" (enable/disable, edit base URL) and "Steam" (API key, SteamID64, Sync button, refresh interval).

## 2. Steam

- Library: `IPlayerService/GetOwnedGames`.
- Wishlist: `IWishlistService/GetWishlist`, falling back to the public store endpoint.
- Tables `SteamLibraryItem` and `WishlistItem` (AppId, Title, AddedAt), upserted on sync.
- The Steam library is kept separate from the backlog; deal results show an "Already owned" marker when the AppId is in the library.

## 3. Scrapers and comparison

- `IStoreScraper.SearchAsync(title, ct)` returns a result of `Ok(offers)` or `Unavailable(reason)`. `StoreOffer` carries store, title, price, original price, discount %, URL, currency.
- Each scraper runs a strategy chain: JSON/HTTP, then HTML (AngleSharp), then Playwright. The first strategy that succeeds wins; a store is reported unavailable only if all fail.
  - JSON first: Steam, Humble, Fanatical.
  - HTML first: Gamesplanet, Green Man Gaming, Instant Gaming (escalating to Playwright on Cloudflare challenges).
- Playwright is an optional last resort, created lazily only when earlier strategies fail.
- One failing store never fails the comparison.
- Title matching: normalise (case, ™/®, edition suffixes) and compare by similarity; results below a threshold are discarded.
- Table `TrackedGame` and `PriceSnapshot` (tracked game, store, price, original price, discount, captured at). A `BackgroundService` refreshes tracked games every N hours; manual "refresh now" and "search now" are available.
- Best offer = lowest current price among available stores; saving is shown against the second best.
- Politeness: minimum delay between requests per store, identifiable User-Agent, `robots.txt` respected where possible, low volume.

## 4. API (all authorized)

- `GET/PUT /stores`
- `PUT /steam/settings`, `POST /steam/sync`, `GET /steam/library`, `GET /steam/wishlist`
- `GET /deals/search?q=`, `GET /deals`, `POST /deals/tracked`, `DELETE /deals/tracked/{id}`, `POST /deals/refresh`
- DTOs live in `Gamer.Contracts`; features follow the existing `Features/<Name>` layout; UI uses `IGamerApi`.

## 5. UI

- New "Deals" tab: best offers sorted by discount, free-text search, pinned games.
- Each card shows a per-store price table with the best highlighted and unavailable stores greyed out.
- New "Steam" page with library and wishlist.

## 6. Testing (xUnit, FluentAssertions)

- Per-store parsers against recorded HTML/JSON fixtures.
- Title normalisation and matching.
- Best-price selection and graceful failure.
- Endpoint tests with fake `HttpClient` handlers.

## Risks

- Unofficial endpoints and HTML selectors can break when sites change; mitigated by one isolated scraper per store with fixtures.
- Scraping may breach some stores' terms; mitigated by low volume and polite behaviour.
