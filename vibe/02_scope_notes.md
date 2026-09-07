# 02 · Scope Notes — Kupomart

_Phase 2 · Scope Planning. Reduce to the smallest useful version. Build the sharpest first version, not the perfect one._

## v1_in_scope — the 4 pillars

1. **Flip Journal (auto-logged)**
   - Hooks Dalamud `IMarketBoard.ItemPurchased` for your buys and retainer-sale packets for your sells
   - Post-MB-tax P/L per flip (5% NPC tax by default; user can override for aetherytes/homeworld exemptions)
   - Per-item stats: buy avg, sell avg, hold time, win rate, GP/hr (session + rolling 7-day)
   - CSV export

2. **Watchlist + FlipScore feed**
   - User adds items or groups; plugin polls Universalis DC/World for those items on a cadence (respects 25 req/s + 8 concurrent + 100-IDs-per-batch)
   - Sortable table: item / your world price / cheapest world in DC / post-tax margin / velocity (sales/day, last 7d) / staleness (minutes since last observed listing change) / **FlipScore**
   - FlipScore v0 formula: `(post_tax_margin_gil × sqrt(sales_per_day)) / max(freshness_minutes, 5)`, normalized 0–100 per session
   - Live WebSocket subscription for watched items (BSON decode; compute deltas ourselves per Universalis bug #1346)

3. **Price DSL (minimal TSM-style)**
   - Named sources: `dbmarket`, `dbminbuyout`, `dbrecent`, `dbregionmarketavg`, `vendor`, `sellrate`
   - Arithmetic + `min/max/avg/ifgte/round` — enough for `max(dbmarket, 1.5*vendor)`
   - Named custom sources reused across groups
   - Groups = list of item IDs + optional attached formula
   - No operations engine in v1 (no post/cancel/restock rules yet)

4. **Tooltip enhancement (opt-in)**
   - 7-day sales sparkline + "last sale 4m ago" staleness stamp on any marketable item
   - FlipScore badge if item is on watchlist
   - Toggle for PriceInsight coexistence (disable overlapping fields if PriceInsight is loaded — don't fight it)

## v1_out_of_scope (postponed — do not build these now)

- Auto-undercut / auto-post to retainers (ToS-adjacent; Dagobert exists)
- Full-market Post Scan / Cancel Scan / Reset Scan (FFXIV has no bulk MB query — every scan is per-item, throttled, requires physically clicking the MB)
- **Sniper** (Universalis has no "newest listings" feed — structurally impossible)
- Crafting profitability engine + craft queue + gathering list (v2 — needs recipe tree work)
- Mail-based alt distribution (FFXIV has no player mail — impossible)
- Warehousing rules between inventory / retainer / saddlebag / FC chest (v2)
- Vendoring / desynth / aetherial-reduction automation (ToS-adjacent)
- Cross-character/retainer inventory rollup (v2 — needs Allagan Tools IPC or reinvention)
- Discord/webhook alerts (v2)
- Group import/export strings (wait until schema stabilizes — v2)
- Desktop companion app / web ledger (v2+)
- Region-wide AuctionDB analogue (Universalis is already this; we just consume)

## caveats / edge cases exposed early

- **Universalis WebSocket delta bug (#1346):** `listings/add` sends full current snapshot, `listings/remove` sends full previous snapshot. We must compute deltas by listing ID ourselves — plan for a `HashSet<long>` per subscribed item and diff on each frame.
- **BSON, not JSON** on the WS channel — pull in a lightweight BSON decoder (`MongoDB.Bson` is heavy; consider `bsonc#` or hand-roll for the ~4 message shapes we need).
- **20-listing retainer cap** kills TSM's "post cap 50000" semantics permanently. Even the eventual v2 Auctioning port can't blindly transplant TSM's algorithm.
- **Rate limits: 25 req/s, 50 burst, 8 concurrent per IP.** Watchlist >100 items must chunk. Add a token-bucket limiter and a per-item minimum poll interval (e.g. 60s).
- **Data scope UI required from day one** — user must pick World / DC / Region for pricing; FlipScore math depends on the choice. Default: home DC.
- **Dalamud v14 / .NET 10 / C# 14 / Dalamud.NET.Sdk 15.0.0** — new toolchain; verify latest SDK when starting Phase 3.
- **ToS surface for the official Dalamud repo bans auto-polling actions.** Read-only + user-clicked buys only for v1. If we ever add v2 posting, live in third-party repo (like Dagobert) and gate everything behind explicit key-modifier + confirmation.
- **PriceInsight is popular.** Detect if loaded and let user disable overlapping tooltip fields on install.
- **Universalis uploader is bundled in Dalamud/XIVLauncher (opt-out)** — we consume, we do not need to write our own uploader.

## scope-creep risks

- **"But TSM has X..."** — Guard: if X requires bulk market queries, a mail system, cross-character bank access, or a live newest-listings feed, X is not portable. Push to `v1_out_of_scope` without debate.
- **"One more feature before shipping"** — Guard: launch after the 4 pillars work end-to-end for one real user (the author, on live retail). Everything else = v1.1, v1.2, v2.
- **Automation temptation** — Guard: v1 does not touch retainer posting/cancelling/restocking. Zero automated actions. Every "buy" is a user click. This keeps us in the official Dalamud repo.
- **Rewriting Dagobert / MarketBoardPlugin / PriceInsight** — Guard: coexist, don't compete. Detect them and defer where they already cover the surface.

## minimum-viable definition

The smallest version that still creates value is:

> **A watchlist window that shows the user's chosen items ranked by post-tax FlipScore with live Universalis data, plus an auto-logged journal of every retainer sale and MB purchase with per-item GP/hr. No config beyond adding items to the watchlist.**

Pillars 3 (Price DSL) and 4 (Tooltip) can slip to v1.1 without killing the value story. Pillars 1 and 2 are the wedge.
