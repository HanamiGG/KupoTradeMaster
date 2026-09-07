# 01 · Mini-PRD — Kupomart (working codename)

_Phase 1 · Vision Planning. Start with clarity, not code. A narrow product belief is a strength._

## problem_statement
FFXIV gil-makers who play retainer flipping, crafting-for-profit, or intra-DC arbitrage juggle 3–5 disconnected tools: MarketBoardPlugin/PriceInsight for lookup, Universalis.app for cross-DC, Saddlebag Exchange for flip finding, Dagobert for undercutting, a spreadsheet for tracking. There is no single "what should I do to make gil right now" surface. TSM veterans coming from WoW find no true equivalent — the closest thing (Dagobert) only auto-undercuts.

## product_goal
A Dalamud plugin that answers "what should I flip / craft / restock right now" as a live ranked feed — TSM's brain, OSRS-style flipper UX, all shaped to FFXIV's constraints. One glance = one decision.

## intended_user & value
- **user:** FFXIV gil-makers running retainer flipping, cross-DC arbitrage, or crafting-for-market
- **wedge:** a ranked **FlipScore feed** of the user's watchlist that reads live from Universalis and shows post-tax margin, sales velocity, and price freshness in one row — no groups/operations config required
- **value:** replaces the tab-flip between Universalis.app / Saddlebag / spreadsheet with one in-game window that actually knows what your retainers just sold

## what this is NOT trying to be
- **Not** a TSM 1:1 clone. Half of TSM's 14 subsystems (Mail, Sniper, full-market Post/Cancel scan at 50K-post cap, Warehousing across bank tabs, cross-alt Crafting queue rollup, disenchant/mill/prospect) are structurally WoW-only and cannot be built on FFXIV's per-item MB, 20-listing retainer cap, and lack of a mail system.
- **Not** an auto-undercutter for v1. Dagobert already does this; it lives in a third-party repo because auto-actions are ToS-adjacent, and duplicating it is not the wedge.
- **Not** another pretty tooltip plugin. PriceInsight and MarketBoardPlugin cover the casual lookup case; we only enhance tooltips with the *ranking* info they don't show (sparkline + staleness + FlipScore).
- **Not** a Sniper. Universalis has no "newest listings" feed; it's a snapshot API. There is no way to build this feature honestly.
- **Not** a desktop companion app for v1.

## belief pressure-test
- **Is the problem real?** Yes. r/ffxiv economy threads and the existence of Saddlebag (a full external site) prove the tooling is fragmented. Dagobert's popularity in the third-party repo proves users will install market plugins outside the official channel.
- **Is it worth solving?** Yes for the wedge. WoW TSM has millions of users; the FFXIV gil-making audience is smaller but demonstrably underserved. The FlipScore + Journal alone would fill a real gap without needing to be TSM-complete to matter.

## user's original ask, resolved
> "1:1 every rich feature that app has"

The research made this impossible-and-unnecessary: half of TSM depends on WoW-specific game affordances, and the tooltip/undercut half is already covered by existing Dalamud plugins. The **honest 1:1 attempt** is: take TSM's DSL/groups model, marry it to OSRS's ranked-feed + auto-journal UX, and ship the pieces that FFXIV actually supports. That's what this plan builds.
