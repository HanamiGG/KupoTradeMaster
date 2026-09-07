<h1>
  <img src="logo.png" alt="KupoTradeMaster" width="48" align="left" style="margin-right: 10px;"/>
  KupoTradeMaster
</h1>

**KupoTradeMaster** is a Dalamud plugin for FFXIV market-board flippers, crafters, and retainer merchants. It fuses TSM-style buy formulas with an OSRS-style ranked "opportunities" feed, an auto-logged trade journal, cross-world arbitrage detection, restock lists with retainer-inventory awareness, and a paste-in crafting buylist that resolves to raw materials with the cheapest world to buy each from — all shaped around FFXIV's per-item market board, retainer cap, and Universalis data model.

The plugin is **read-only and user-clicks only**. It never posts, cancels, buys, or lists on your behalf. Everything it shows is a decision aid — you press the buttons.

---

## Features

### Ten in-game tabs

| Tab | What it does |
|-----|--------------|
| **Best Deals** | Ranked "what to flip right now" feed over your watchlist. Post-tax margin, FlipScore, cheapest cross-DC world, per-world hover breakdown, optional "Buy under X" column from your custom formula. |
| **Arbitrage** | Cross-world lens: for each watchlist item, find the cheapest world in your data scope, sell at your home-world undercut. Filter by margin threshold. |
| **Watchlist** | Your item universe with min listing, avg price, post-tax margin, velocity, freshness, and FlipScore. Group filter, one-click add, right-click context menu, bulk import from TeamCraft / Artisan / plain text. |
| **Journal** | Auto-logged market-board buys (correlated via `PurchaseRequested` + `ItemPurchased`) and retainer sales (via inventory delta detection — cancellations are distinguished from sales, so sold entries are confirmed, not estimated). Per-item aggregate P/L, session net P/L, multi-character rollups, CSV export. |
| **Wealth** | Cash + bag + saddlebag + retainer inventory + active listings, valued at min listing. Multi-series net-worth trend chart over the last ~8 hours. Buckets whose containers weren't loaded this capture fall back to the last-known value rather than reporting zero. |
| **Retainers** | Per-retainer inventory, listings with your unit price and vs-market delta, gil, venture status. Home-world-scoped "MB min" comparison so a foreign 2M outlier doesn't paint every listing red. |
| **Crafting** | Two modes: **Browse** all ~8k Lumina recipes by profit; **Buylist** — paste an Artisan or TeamCraft list, recursively expand to raw materials, subtract retainer/bag/saddlebag on-hand, and surface the cheapest DC-wide world to buy each remaining material from. |
| **Restock** | Named restock lists with target quantities per item. Computes deficits from retainer inventory + listings + optionally bag/saddlebag. Exports as Artisan-compatible JSON, plain text, or a shareable `KM1:` code. Imports TeamCraft, Artisan JSON, or plain text (all merge into the target list). |
| **Sniper** | Single-item on-demand fetch that ranks every current DC listing against your buy formula — the underpriced ones bubble to the top. |
| **Settings** | Data scope, tax rate, poll interval, prerequisite plugin detection, groups, custom price sources, operations, one-click backup/restore. |

### TSM-style Price DSL

Formulas driving Best Deals, Sniper, and the buy-signal watcher use a tiny expression language:

- **Sources**: `mbmin` / `dbminbuyout` (DC min listing), `mbminhw` (home-world min listing), `mbavg` / `dbmarket` (DC avg price), `mbavghw` (home-world listing average), `velocity` / `sellrate`, `vendor`, `taxrate`
- **Functions**: `max`, `min`, `avg`, `round`, `floor`, `ceil`, `if(cond, a, b)`
- **Named custom sources**: define `cheap = mbmin * 0.6` in Settings, reuse anywhere
- **Per-group overrides**: attach a formula (or a named Operation) to any group so different item classes get different buy rules

Example: `max(mbavghw * 0.7, vendor * 1.5)` — the higher of "70% of home-world listing avg" or "1.5× vendor floor".

### Home-world–scoped pricing

Every "what will you sell at?" number is scoped to your home world (Midgardsormr, Cactuar, …) rather than the DC-wide sales average — TSM's `firstlist − 1` rule. That prevents a single mispriced foreign listing from inflating your margin math into millions-of-percent fantasy territory. DC-wide sources remain available in the DSL if you want them explicitly.

### Bulk import (TeamCraft / Artisan / plain text)

Restock, Watchlist bulk-import, and Crafting Buylist all accept the same paste formats:

- **KupoTradeMaster share code** (`KM1:...`) — round-trips whole lists between installs
- **TeamCraft JSON** — `{items: [{id, amount, hq}]}` or a bare array
- **Artisan-style JSON** — same envelope, case-insensitive field names
- **Plain text** — `5x Item Name`, `Item Name × 5`, `12345,10`, with optional `(HQ)` / `(NQ)` suffix. Section headers ("Ingredients", "Materials", "Total"), `#` comments, and blank lines are skipped so TeamCraft's "Copy as text" output can be pasted verbatim.

### Prerequisite plugins (all optional, graceful degradation)

- **Allagan Tools** — seeds the retainer roster before you visit a Summoning Bell, and refreshes item quantities per retainer via `ItemCount` IPC so retainers you haven't summoned this session still contribute accurate inventory to your net-worth calc.
- **AutoRetainer** — offline retainer gil + venture ETA per retainer.
- **Artisan** — presence detected; buylist exports produce Artisan-compatible payloads.

The bridge is auto-rescanned every 30 seconds — a peer plugin that loads mid-session is detected without a UI trip.

### Data safety

- Journal, valuation history, and retainer state each persist to disk under a per-service file lock — the 5-minute snapshot loop can't race a restore, and the "Snapshot now" button is re-entry guarded.
- One-click backup and restore (zip) round-trips journal / history / retainers. Restore reloads in-memory state so subsequent appends don't corrupt the restored file.

---

## Installation

KupoTradeMaster is distributed as a third-party Dalamud plugin repository.

1. In-game: `/xlsettings` → **Experimental** tab
2. Under **Custom Plugin Repositories**, paste:
   ```
   https://raw.githubusercontent.com/hanamigg/kupotrademaster/main/pluginmaster.json
   ```
3. Click the `+` to add, then **Save**
4. `/xlplugins` → **All Plugins** → search "KupoTradeMaster" → Install
5. Open with `/ktm` (or `/kupo` as a short alias)

---

## Commands

| Command | Effect |
|---------|--------|
| `/ktm` | Toggle the main window |
| `/kupo` | Alias for `/ktm` |
| `/kupo <item name>` | Search Lumina and open the top match on Universalis in your browser |

---

## Building from source

Requirements:
- .NET 10 SDK
- XIVLauncher + Dalamud (API level 15)

```
dotnet build src/KupoTradeMaster/KupoTradeMaster.csproj -c Release
```

Output lands at `src/KupoTradeMaster/bin/Release/KupoTradeMaster/latest.zip`. For local dev-plugin sideload, drop `KupoTradeMaster.dll` + `KupoTradeMaster.json` into `%AppData%\XIVLauncher\devPlugins\KupoTradeMaster\`, then enable via `/xldev`.

---

## Not doing (by design)

- **Auto-undercutting / auto-posting** — [Dagobert](https://github.com/SHOEGAZEssb/Dagobert) already covers that; it's ToS-adjacent territory the official Dalamud repo doesn't accept. KupoTradeMaster stays read-only.
- **Real-time newest-listings sniper feed** — Universalis has no such feed; structurally impossible without game-server access.
- **Mail-based alt distribution** — FFXIV has no player mail.
- **Full-market post/cancel scans** — FFXIV's per-item market board plus the 20-listing retainer cap kill TSM's whole-market-scan algorithm.

---

## Credits

- **Market data**: [Universalis](https://universalis.app/) — keyless public API. Please respect their rate limits.
- **Runtime**: [Dalamud](https://github.com/goatcorp/Dalamud) + [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher).
- **Game data**: [Lumina](https://github.com/NotAdam/Lumina) (Excel sheets), [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) (inventory + retainer state).
- **Inspiration**: WoW's [TradeSkillMaster](https://tradeskillmaster.com/) and OSRS [Flipping Utilities](https://github.com/Flipping-Utilities/rl-plugin).
- **Interop**: designed to coexist with [MarketBoardPlugin](https://github.com/fmauNeko/MarketBoardPlugin), [PriceInsight](https://github.com/Kouzukii/ffxiv-priceinsight), [Dagobert](https://github.com/SHOEGAZEssb/Dagobert), [Allagan Tools](https://github.com/Critical-Impact/InventoryTools), [AutoRetainer](https://github.com/PunishXIV/AutoRetainer), and [Artisan](https://github.com/PunishXIV/Artisan).

---

## License

MIT. See `LICENSE` for the full text.
