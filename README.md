# KupoTradeMaster

A Dalamud plugin that shows FFXIV gil-makers **what to flip right now** — TSM's brain, OSRS-style ranked feed, all shaped to FFXIV's constraints.

> See `vibe/01_mini_prd.md` for the product vision, `vibe/02_scope_notes.md` for scope, and `vibe/STATUS.md` for the current session state.

## What it does

Seven tabs in-window (via `/kupo`):

**Best Deals** — one-glance ranked feed of every watchlist item with positive post-tax margin *right now*, sorted by FlipScore, with a cheapest-world column, per-world hover breakdown, and an optional "Buy under X" column driven by your custom formula.

**Arbitrage** — dedicated cross-world lens: for each watchlist item, find the cheapest world in your data scope and compute expected profit if you buy there and re-list at the DC average. Filter by margin threshold.

**Watchlist** — the full list with FlipScore, min listing, avg price, post-tax margin, velocity, freshness. Group filter, item search + add UI, right-click context menu, X to remove.

**Journal** — auto-logged market-board **buys** (correlated via `IMarketBoard.PurchaseRequested` + `ItemPurchased`) and estimated **sells** (via diff-detection on your retainer's `RetainerMarket` container). Per-item aggregate P/L, session GP/hr, recent activity log. Right-click estimated entries to set the actual price or delete. **CSV export** to plugin config dir.

**Wealth** — cash + bag + saddlebag + retainer inventory + active listings, valued at Universalis min. Multi-series net-worth trend chart (Total / Cash / Bag+SB / Retainers) over the last ~8 hours of 5-minute snapshots. History persists to disk.

**Retainers** — per-retainer inventory, active listings with your unit price + vs-market delta, gil, last-seen. Seeded from Allagan Tools if installed; enriched with AutoRetainer venture status if installed.

**Settings** — Data scope (DC / world), MB tax rate, poll interval, prerequisite plugin detection, watchlist groups (create/rename/delete + per-group formula overrides), global buy formula, chat-notification toggle.

## Highlights

- **TSM-style Price DSL** — sources `mbmin` / `dbminbuyout`, `mbavg` / `dbmarket`, `velocity` / `sellrate`, `vendor`, `taxrate`; functions `max` / `min` / `avg` / `round` / `floor` / `ceil` / `if(cond, a, b)`; arithmetic + parens. Example: `max(dbmarket * 0.7, vendor * 1.5)`. Optional per-group overrides.
- **Buy-signal chat notifications** — one-shot ping when an item first crosses the threshold this session; re-arms when it goes above.
- **Universalis-powered** — DC-scope batched HTTP with token-bucket rate limiter (25 req/s, 8 concurrent per docs); no API key required.
- **Live tax rates** per home city via Universalis `/tax-rates`, auto-picks the cheapest for margin math.
- **Plugin integrations** — Allagan Tools (retainer roster), AutoRetainer (offline retainer gil + venture status), Artisan (detected, reserved for a future Crafting pillar). All optional; graceful degradation.
- **`/kupo <item name>`** — searches Lumina, opens Universalis for the top match in your browser.
- **Right-click item context menu everywhere** — Copy name / ID, Open on Universalis, Add/Remove Watchlist, Add to Group ▶.

## Build & sideload

Requirements:
- .NET 10 SDK
- XIVLauncher + Dalamud (API level 15)

```
dotnet build src/KupoTradeMaster/KupoTradeMaster.csproj -c Release
```

Output lands at `src/KupoTradeMaster/bin/Release/`. Copy `KupoTradeMaster.dll` + `KupoTradeMaster.json` to:

```
%AppData%\XIVLauncher\devPlugins\KupoTradeMaster\
```

In game: `/xldev` → **Dev Plugins** tab → enable **KupoTradeMaster** → `/kupo`.

## Not doing (by design)

- **Auto-undercutting / auto-posting** — Dagobert covers it; ToS-adjacent for the official Dalamud repo. KupoTradeMaster stays read-only + user-clicks only.
- **Sniper (real-time newest listings)** — Universalis has no such feed; structurally impossible.
- **Mail-based alt distribution** — FFXIV has no player mail.
- **Full-market Post/Cancel Scans** — FFXIV's per-item MB + 20-listing retainer cap kill TSM's algorithm.

See `vibe/02_scope_notes.md` for the full anti-scope.

## Credits

- Market data: [Universalis](https://universalis.app/) — keyless public API, please respect their rate limits.
- Runtime: [Dalamud](https://github.com/goatcorp/Dalamud) + [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher).
- Item sheets: [Lumina](https://github.com/NotAdam/Lumina), inventory access via [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs).
- Inspiration: WoW's [TradeSkillMaster](https://tradeskillmaster.com/) + OSRS [Flipping Utilities](https://github.com/Flipping-Utilities/rl-plugin).
- Coexists with [MarketBoardPlugin](https://github.com/fmauNeko/MarketBoardPlugin) / [PriceInsight](https://github.com/Kouzukii/ffxiv-priceinsight) / [Dagobert](https://github.com/SHOEGAZEssb/Dagobert) / [Allagan Tools](https://github.com/Critical-Impact/InventoryTools) / [AutoRetainer](https://github.com/PunishXIV/AutoRetainer) / [Artisan](https://github.com/PunishXIV/Artisan).
"# KupoTradeMaster" 
