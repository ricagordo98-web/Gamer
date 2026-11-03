# Game Backlog — Design

## Goal
Cross-platform (Android/Windows/iOS/Mac via MAUI, plus web) app to manage a game backlog, replacing `Jogos Backlog.xlsx` (741 games, 16 platforms). Modern, fast, lightweight, mobile-friendly. Data synced through a central API.

## Solution structure
| Project | Purpose |
|---|---|
| `GameBacklog.Contracts` | Shared DTOs (records) and enums |
| `GameBacklog.Api` | ASP.NET Core minimal API, EF Core, SQLite, JWT auth, FluentValidation |
| `GameBacklog.Web.Ui` | Razor Class Library: all pages/components, `IGameService` abstraction |
| `GameBacklog.Web` | Blazor WebAssembly host using the RCL |
| `GameBacklog.Maui` | MAUI Blazor Hybrid host using the RCL |
| `GameBacklog.Tests` | xUnit, FluentAssertions, NSubstitute |

## Data model
- `Game`: Id, Title, PlatformId, Status, OnDeck, Rating?, Notes?, CreatedAt, UpdatedAt.
- `Platform`: Id, Name, Family, Format (Physical/Digital). Seeded from the "Consolas" sheet; prefix "L -" maps to Physical, "PC -" to Digital.
- `GameStatus`: `Backlog`, `Playing`, `Completed`. Excel mapping: Terminado=1 -> Completed; else A Jogar=1 -> Playing; else Backlog.
- `OnDeck` = Excel "Deck" column (Steam Deck).

## Features
- Game list: instant search; filters by platform, status, Deck; virtualized rendering.
- One-tap status change; add/edit (bottom sheet on mobile).
- Dashboard: totals, completion progress, currently playing, random "suggest a game" from backlog.
- One-shot Excel importer (trims whitespace, reports duplicates).
- Dark-first, card-based, colored platform chips, responsive.

## Data flow and errors
- UI -> `IGameService` -> `HttpClient` -> API. All async methods take `CancellationToken`.
- API validates with FluentValidation and returns ProblemDetails; UI shows toast errors and a retry/offline state.
- Auth: single user, JWT; credentials in API configuration via `IOptions<T>`; no registration.

## Testing
Unit tests for services, validators and Excel mapping; endpoint tests with in-memory SQLite.

## Risks
- Verify the MAUI workload for .NET 10 is installed (installed manifests are .NET 11 RC).
- MAUI Android/iOS builds depend on available SDKs/Mac; Windows is verifiable locally.
