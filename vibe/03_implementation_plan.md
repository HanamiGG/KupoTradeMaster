# 03 · Implementation Plan — Kupomart v0 → v1

_Phase 3 · Agent Preparation. Concrete structure for the coding pass._

## v0 (Phase 5 Prototype Feel Check) — this session

**Goal:** a compiled, sideload-able Dalamud plugin that opens an ImGui window showing a hardcoded watchlist of ~10 popular items, live-polled from Universalis home DC, sorted by FlipScore. Prove the core loop feels right before investing in config/journal/tooltip.

**Cut from v0 (deferred to v1):** watchlist add/remove UI, DC picker UI, config persistence detail, journal / SQLite, tooltip enhancement, Price DSL, WebSocket. All hardcoded for prototype.

## Solution layout

```
C:\HeadroomAI\ffxivtsm\
├── Kupomart.sln
├── README.md
├── .gitignore
├── src\Kupomart\
│   ├── Kupomart.csproj            # Dalamud.NET.Sdk
│   ├── Kupomart.json              # plugin manifest
│   ├── Plugin.cs                  # IDalamudPlugin entry, DI, /kupo command
│   ├── Configuration.cs           # IPluginConfiguration (watchlist, DC, tax %)
│   ├── Services\
│   │   ├── UniversalisModels.cs   # POCOs for /v2/{dc}/{ids} response
│   │   ├── UniversalisClient.cs   # HttpClient, batched fetch, rate-limit budget
│   │   ├── FlipScorer.cs          # pure v0 formula
│   │   └── WatchlistPoller.cs     # timer-driven refresh loop
│   └── UI\
│       └── MainWindow.cs          # ImGui.NET table with sortable columns
```

## Universalis client design

- `HttpClient` with `User-Agent: Kupomart/{version} (github.com/foglerk/kupomart)`
- Endpoint: `GET https://universalis.app/api/v2/{scope}/{itemId1,itemId2,...}?listings=5&entries=5`
- Batches capped at 100 IDs per request (API max)
- **Rate budget:** token bucket at 20 req/s (safe margin under Universalis' 25 r/s), max 4 concurrent (well under their 8 concurrent limit)
- Per-item minimum poll interval: 60s (avoid pointless refreshes)
- Poll loop tick 30s; each tick pulls only stale items

## FlipScore v0 formula

```
minPrice        = min listing price on scope
sellReference   = currentAveragePrice  (Universalis rolling)
margin_gil      = (sellReference * (1 - taxRate)) - minPrice   ; taxRate=0.05 default
velocity_perday = regularSaleVelocity                          ; Universalis units/day
freshness_min   = (now - lastUploadTime).TotalMinutes

if margin_gil <= 0:  score = 0
else:                score = (margin_gil * sqrt(velocity_perday)) / max(freshness_min, 5.0)
```

Normalize to 0–100 per session. Formula lives in `FlipScorer.cs`, pure static, unit-testable.

## Bootstrap watchlist (v0 hardcoded)

Popular retail flip candidates so the user sees the score do something out of the box. IDs verified against Lumina at build time — a few tentative:
- Fire Crystal (8), Ice Crystal (9), Wind Crystal (11) — universal craft
- Dark Matter Cluster (10386) — endgame repair
- Chocobo Feather (4870) — bulk vendor arbitrage
- Thavnairian Onion (4868) — chocobo food
- Grade 8 Tincture of Strength (36109 or current) — combat pot
- HQ high-tier crafted materia (current expansion)

## Dalamud services used (v0)

- `IDalamudPluginInterface` — entry
- `IPluginLog` — logging
- `ICommandManager` — `/kupo` toggle
- `IChatGui` — one-time hello message
- `IFramework` — timer tick

## Coexistence (later)

- No shared surface in v0. When we add tooltips, detect PriceInsight via `IDalamudPluginInterface.InstalledPlugins` and let user disable overlapping fields.

## Testing notes for the prototype

1. `dotnet build` cleanly against Dalamud.NET.Sdk
2. Copy `Kupomart.dll` + `Kupomart.json` to `%AppData%\XIVLauncher\devPlugins\Kupomart\`
3. In game: `/xldev` → Kupomart shows in Dev Plugins → enable
4. `/kupo` opens window → hardcoded watchlist populates in <60s → FlipScores visible → clicking column header sorts
5. Feel check: does the ranked list produce items that pass the "5-second buy decision" test?

## Rules for the coding agent (rules.md — inline)

- C# 14 / .NET 10, nullable enabled, `TreatWarningsAsErrors` on
- Pure ImGui via ImGui.NET (no XAML/Blazor/WPF)
- `System.Text.Json` for parsing (no Newtonsoft)
- No reflection, no `dynamic`
- No auto-actions on retainers or MB — read-only + user-clicks only (ToS)
- All I/O behind `HttpClient` singleton with User-Agent set
- One command: `/kupo` toggles main window (`/kupomart` alias)
