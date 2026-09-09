<h1>
  <img src="logo.png" alt="KupoTradeMaster" width="48" align="left" style="margin-right: 10px;"/>
  KupoTradeMaster
</h1>

[![Support on Ko-fi](https://img.shields.io/badge/Support-Ko--fi-FF5E5B?logo=ko-fi&logoColor=white)](https://ko-fi.com/hanamivgc) [![Chat on Discord](https://img.shields.io/badge/Chat-Discord-5865F2?logo=discord&logoColor=white)](https://discord.gg/aVgx6dVSre)

Every merchant deserves a moogle at their shoulder, kupo. This one lives in your Dalamud client. It watches the market boards for you, keeps a ledger of everything you buy and sell, remembers what your retainers are supposed to be holding, and never once claims a mispriced Gysahl Green is worth three million gil. It is a market-board sidekick for FFXIV crafters and flippers, built for the person who wants the numbers laid out clearly and then wants to decide for themselves.

No auto-clickers. No bots. No spooky automation of any kind. The moogle does the arithmetic. You still fly to the retainer and click the buy button.

## Table of contents

- [What KupoTradeMaster is](#what-kupotrademaster-is)
- [What it will not do](#what-it-will-not-do)
- [Total beginner's guide](#total-beginners-guide)
- [Install](#install)
- [First flight](#first-flight)
- [The tabs, one by one](#the-tabs-one-by-one)
  - [Best Deals](#best-deals)
  - [Arbitrage](#arbitrage)
  - [Watchlist](#watchlist)
  - [Journal](#journal)
  - [Wealth](#wealth)
  - [Retainers (with Undercut panel)](#retainers)
  - [Crafting (Browse and Buylist)](#crafting)
  - [Restock (with precraft expansion and craft-vs-buy)](#restock)
  - [Sniper](#sniper)
  - [Settings](#settings)
- [Weekly profit report](#weekly-profit-report)
- [How-to guides](#how-to-guides)
- [Custom buy formulas](#custom-buy-formulas)
- [Targeted price alerts](#targeted-price-alerts)
- [Sharing lists and formulas](#sharing-lists-and-formulas)
- [Plays well with others](#plays-well-with-others)
- [Settings worth knowing about](#settings-worth-knowing-about)
- [Troubleshooting](#troubleshooting)
- [Support the moogle](#support-the-moogle)
- [Credits](#credits)

## What KupoTradeMaster is

A single Dalamud window with ten tabs, each one focused on one part of the market-board loop. Pick the item, buy it cheap, list it fair, log the profit, watch the wealth graph tick up. The moogle reads live prices from Universalis, cross-references what you actually own on your character and retainers, and shows you where the deals are.

Everything is scoped to your **home world** by default. That is on purpose. It means the sell prices you see are what other people on your home world are actually charging right now, not a haunted 3M gil sale from Ultros that skews the whole data center. Realistic numbers only.

## What it will not do

- Auto-buy. Auto-sell. Auto-undercut. Auto-anything.
- Ferry gold across characters or claim to make gil while you sleep.
- Send your data anywhere private. It reads from Universalis (public API), writes a local journal, and that is the whole network story.
- Replace [Dagobert](https://github.com/Cufiy/Dagobert). If you want a proper auto-undercutter, that plugin exists and does the job well. KupoTradeMaster is the "decide what to trade" half of the workflow. The two get along.

<a id="total-beginners-guide"></a>
## Total beginner's guide

Never used a Dalamud plugin? Never traded on the market board seriously? Do not know what half of these words mean? Read this section first, kupo. Once you get through it, the rest of the README will make sense.

### The pieces you already need

**FFXIV Online.** You are playing it. Good start.

**XIVLauncher.** A community-made replacement for the official game launcher. It is what lets FFXIV load extra tools. If you have not installed it yet, grab it from [https://goatcorp.github.io/](https://goatcorp.github.io/), point it at your existing FFXIV install, and log in with your Square Enix account like normal. Nothing about your game files changes.

**Dalamud.** A plugin framework that ships with XIVLauncher. When you launch the game via XIVLauncher, Dalamud injects into the game process at login and lets plugins draw windows on top of the game. That is it. You do not install Dalamud separately; it comes with XIVLauncher.

**A moment of ease.** Dalamud plugins are third-party. Square Enix does not officially support them. Plugins are used by hundreds of thousands of players and the community is careful about read-only, well-behaved plugins (which is what KupoTradeMaster is). The safe path is: use XIVLauncher, use well-known plugins, never use anything that automates game actions. This plugin only reads market data and logs your own purchases. It clicks nothing.

### Words that appear a lot

Terms you will see in this README, on the market board, and in every FFXIV economy guide.

- **Gil.** In-game currency. The number in the bottom-right of your screen.
- **Data center (DC).** A cluster of worlds grouped by region. North American examples: Aether, Primal, Crystal, Dynasty. You can freely visit worlds within your own DC.
- **World.** A single game server. You picked one when you made your character. That is your **home world**.
- **Home world.** Your character's home server. This is the world where you actually list items on your retainers. It is the world where buyers most easily find you.
- **Market board (MB).** The auction-house-style building in every capital city. Any player can browse listings across their entire DC. You buy from any world on your DC directly, but only from listings on your home world get to be sold from your retainers to buyers on your home world without them needing to travel.
- **Retainer.** An NPC assistant you hire in-game. Each character can have up to nine (with subscription add-ons). Retainers hold inventory and post market-board listings on your behalf. When someone buys from your retainer, you get the gil next time you speak to them.
- **Listing.** A single row on the market board: item, quantity, unit price, retainer name, world, HQ flag.
- **HQ (High Quality).** An item can be normal or HQ. HQ generally sells for more, especially for crafted gear, food, and potions.
- **NQ (Normal Quality).** Regular quality. Some items only ever exist as NQ (most raw materials).
- **Tax.** When you sell on the market board, the city takes a 5% cut. When calculating "am I making money," the plugin factors this in and calls it **post-tax** profit.
- **Undercut.** When another seller lists the same item cheaper than you. Your listing stops being the first result buyers see. Common workflow: check undercuts, re-list at or just under the new floor.
- **Flip.** Buy an item on the market board for cheap, list it back on the market board for more, keep the difference. The classic market-board play.
- **Arbitrage.** Same idea as a flip but specifically buying from a cheaper world on your DC and selling on your home world.
- **Watchlist.** Your list of "items I want the plugin to track prices for."
- **Restock.** A saved shopping list of "what my retainer should have on it at all times" and the deficit versus current stock.
- **Precraft.** A crafted item that is itself an ingredient in another crafted item. A stitched belt is the final craft; the leather panels used to make it are precrafts.

**Universalis.** A community-run website ([universalis.app](https://universalis.app)) that aggregates market-board data. Whenever any player with a compatible plugin opens the market board in game, prices from that snapshot get uploaded to Universalis. KupoTradeMaster reads from Universalis for every price you see in it. That is why prices update seconds after you (or anyone else) opens the board.

### From zero to your first flip

Follow these in order. You will spend maybe 20 minutes and end the session with a plugin that works and a first profitable trade under your belt.

**Step 1. Install the plugin.** In game, type `/xlsettings` and press Enter. A settings window opens. Click the **Experimental** tab. Find the section labelled **Custom Plugin Repositories**. Paste this into the blank text box:

```
https://raw.githubusercontent.com/hanamigg/kupotrademaster/main/pluginmaster.json
```

Click the plus button next to it. Click **Save and Close** at the bottom. Type `/xlplugins` to open the plugin installer. Search **KupoTradeMaster**. Click **Install**. It downloads in under a second. Done.

**Step 2. Open the plugin.** Type `/kupo` in chat and press Enter. A window opens with tabs along the top.

**Step 3. Confirm your home world and DC.** Click the **Settings** tab. At the top there is a data-center dropdown. It should already show your home DC (the plugin reads it from the game). If it looks wrong, correct it. This one setting controls every price you see for the rest of the session.

**Step 4. Add your first Watchlist items.** Click the **Watchlist** tab. Type an item name in the search box, for example "Rarefied Titanoconch". Click a suggestion to add it. Repeat with three or four other items. If you have no idea what to add, try these starter picks: **Grade 5 Dark Matter**, **Cordial**, **Kingcake**, **Grade 4 Skybuilders' Log**. Common consumables and crafting mats give you fast price feedback.

**Step 5. Wait one minute.** The plugin polls Universalis every 30 seconds. You need at least one poll cycle to have data. The Watchlist tab shows "N of M items have live prices" at the top so you can see it filling in.

**Step 6. Open Best Deals.** Click the **Best Deals** tab. Your watchlist items appear sorted by profit-per-unit. The columns say what the moogle thinks each item can sell for on your home world minus what it costs on your DC right now, minus 5% tax. Green numbers are worth a look. Red or grey numbers mean the item is not currently a good flip.

**Step 7. Visit a retainer, once.** In game, go to any inn or aetheryte-adjacent retainer bell. Talk to one retainer. Close the menu. The plugin now knows what that retainer is holding. Repeat for each retainer if you have more than one. This is a one-time thing per session.

**Step 8. Read the moogle's ranking.** Back in the plugin, the top row of Best Deals is the moogle's current best pick. Say it says **Rarefied Titanoconch, buy at 12,000g, sell for 18,000g, post-tax profit 5,100g, FlipScore 78**. That means: right now on your DC, someone has a listing at 12,000g; the average sale price on your home world is 18,000g; if you buy and re-list, you clear about 5,100g per unit after tax; FlipScore 78 is the plugin's overall confidence (higher is better, factors in how fast this item actually sells).

**Step 9. Do the flip.** Fly to any market board in a capital city. Type the item name into the market board search. Sort by unit price. Look for the world column: the cheapest listing is probably on a different world from yours. Buy it. (You do not need to travel; direct-buy across worlds is free.)

**Step 10. List it on your retainer.** Go back to your retainer bell. Talk to a retainer, choose "Sell items on the market board." Pick the item you just bought from your inventory. Price it slightly below the current cheapest listing on your home world (the plugin's tooltip on the item in-game shows the current home-world floor). List. Close the menu.

**Step 11. Check the Journal.** Back in the plugin, click the **Journal** tab. Your purchase from step 9 is there, auto-logged. When the retainer eventually sells the item (could be minutes, could be days) the plugin logs the sale too. Session profit ticks up.

That is one full cycle. The rest of the plugin is variations on this loop and quality-of-life features around it.

### What each tab does (in beginner terms)

- **Best Deals.** Watchlist items ranked by how much money you would make per unit if you bought the cheapest listing on your DC and re-listed on your home world at the current average price. Post-tax numbers.
- **Arbitrage.** Same idea, but ranked by cross-world price gap and total profit for a full run rather than per-unit margin. Useful for bulk moves.
- **Watchlist.** Where you tell the plugin what to track. Add items, group them, remove them.
- **Journal.** Every buy and sell you do, auto-logged. Session, day, week, month totals. Search, filter, export to spreadsheet.
- **Wealth.** A chart of your total worth (gil + inventory + retainer holdings + listing value) over time.
- **Retainers.** One entry per retainer with current gil, active listings, and total value listed. **Undercut panel** at the top flags any of your listings that a competitor has priced under.
- **Crafting.** Two modes. **Browse recipes** lets you scroll every craft in the game and see which are currently profitable. **Buylist** takes any crafting list you paste and tells you what raw mats you are missing and where to buy them cheapest.
- **Restock.** Long-term shopping lists. "This retainer should always have 20 of each of these items. Show me the deficit."
- **Sniper.** Live-price scanner. Give it an item and a target price (say, 60% of the current floor). It highlights every listing at or under that threshold across your DC with retainer names and worlds so you know exactly where to fly.
- **Settings.** Every knob. Data-center scope, buy formulas, groups, targeted price alerts, chat channel for alerts, hotkey to toggle the window, danger-zone reset buttons.

### What you do NOT need to worry about

- **Setting up Universalis accounts.** No account needed. It just works.
- **Buying anything.** The plugin is free.
- **Losing your data.** Watchlists, journal, and settings are stored in `%AppData%\XIVLauncher\pluginConfigs\KupoTradeMaster\`. Nothing is uploaded anywhere private.
- **Being banned.** This plugin does not automate game actions. Every buy and every listing is a click YOU make. Reading market prices from Universalis is the same thing every market-board plugin does.
- **Formula syntax.** You never need to touch the buy-formula language unless you want the Sniper tab to alert you at custom thresholds. The default formula works for beginners.

### If something looks wrong

Prices showing dashes for the first minute is normal (waiting on the first poll). Prices showing dashes after five minutes means Universalis is slow or your data-center dropdown in Settings is wrong. Every other symptom has an entry in the [Troubleshooting](#troubleshooting) section at the bottom of this README.

If you get truly stuck, drop a message in the [Discord](https://discord.gg/aVgx6dVSre) or open a [GitHub issue](https://github.com/hanamigg/kupotrademaster/issues). The moogle is friendly.

## Install

In game, type `/xlsettings`. Under the **Experimental** tab, paste this into **Custom Plugin Repositories** and click the plus:

```
https://raw.githubusercontent.com/hanamigg/kupotrademaster/main/pluginmaster.json
```

Hit **Save**. Open the plugin installer with `/xlplugins`, search for **KupoTradeMaster**, and hit **Install**. Type `/kupo` in chat to open the window. That is the whole install.

Update the plugin by clicking the update button in the installer when a new version lights up. The moogle will auto-detect that it is behind and pull the new build.

## First flight

Here is what a brand new user should do in the first ten minutes.

1. **Open the plugin.** Type `/kupo` in game. The window appears with the tab bar along the top.
2. **Check your scope.** Go to the **Settings** tab. At the top there is a data-center picker. It should already say your home DC. If it does not, pick it now. Everything downstream reads from this setting.
3. **Add a few items to your Watchlist.** Go to the **Watchlist** tab and either type item names in the search box or paste a TeamCraft list into the bulk-import panel. Ten to twenty items is a fine start. The moogle begins polling their prices every 30 seconds.
4. **Wait one minute.** Prices need a tick or two to populate.
5. **Look at Best Deals.** Your watchlist items sorted by real post-tax margin. Green numbers are worth a look.
6. **Visit a retainer once.** This wakes up the inventory tracker so the plugin knows what you already own.
7. **Buy something you like.** The purchase is auto-logged in the **Journal** tab, tagged with your character name.
8. **List it on your retainer.** Once it sells (or is cancelled), the plugin figures out which one happened and updates the journal accordingly.
9. **Check Retainers.** If anyone has undercut you since the last poll, the top of the tab now shows a highlighted panel listing every affected retainer with the new floor.
10. **Come back tomorrow.** Check the **Wealth** tab to see your gil trend line move. A week from now, the auto-shown profit report will summarise your top items and net gil.

That is the whole loop. Everything else in the plugin is either a variation on this loop or a convenience feature layered on top.

## The tabs, one by one

### Best Deals

Your watchlist, sorted by real post-tax profit-per-unit. The top row is the item the moogle currently thinks is worth your time. Each row shows item name, cheapest listing on your data center, average sale price on your home world, post-tax margin, sale velocity per day, and a FlipScore that combines those factors into a single 0-100 number.

**Filters at the top:** group (if you have watchlist groups set up), and a minimum-velocity slider so you can hide items that sell twice a week even if the margin looks juicy.

### Arbitrage

Watchlist items ranked by cross-world price gap. For each item the moogle finds the cheapest world on your DC, computes what you would clear per unit after tax if you bought there and listed on your home world, then sorts by total profit-per-run.

Hovering the "Buy on" cell shows a full per-world price breakdown so you can decide whether the cheapest listing is a stack of 99 or a single unit someone forgot about.

**Filters at the top:** group, minimum margin percent, minimum velocity.

### Watchlist

Where you tell the moogle what to track. Add items by search, or use the **Bulk import** panel at the top to paste a TeamCraft list, an Artisan crafting list, a plain-text list of names, or one of KupoTradeMaster's own share codes. Items can be grouped (crafting mats, HQ furniture, whatever) and the group filter appears on the other tabs.

Each row has a checkbox on the left. Select several and a **Remove selected** button appears above the table, which is faster than deleting one at a time when a whole group turns out to be worthless. The status line "N of M items have live prices" at the top of the table tells you if the poller is still catching up versus if the item genuinely has no listings.

### Journal

Every gil moves through here.

**Purchases** get logged automatically the moment a market-board buy completes. Item, quantity, unit price, total, character, timestamp.

**Retainer sales** get logged too, and this is where the plugin quietly earns its keep. When a listing disappears from a retainer, the moogle checks whether the item's stack size on that retainer went up or down. If it went up, the listing was cancelled. If it went down, it sold. The journal shows **Sell** for a real sale, not **Sell?**, and only real sales count toward session profit.

At the top of the tab: **GP Earned This Session** (all purchases and sales since you opened the plugin), a **date-range filter** (all time / this session / today / last 7 days / last 30 days), an **item-name search** for finding a specific transaction, a **character filter** for multi-character users, and an **Export CSV** button.

### Wealth

A line chart of your total worth over time. The moogle samples your wealth every few minutes and stores it locally. Total = gil in wallet + inventory value + saddlebag value + retainer inventory value + active listing value.

**Header widgets:** total gil, delta since N days ago (configurable, default 7), and a slider that changes the chart window from 2 hours to 168 hours (a week).

There is also a **CSV export** button in case you want to graph your gil in a spreadsheet or share progress with a discord.

<a id="retainers"></a>
### Retainers

One entry per retainer, listing their gil, current listings, and total listed value (unit price × quantity summed across their board slots). Selecting a retainer opens a detail pane with every active listing, their prices, and how long they have been up.

**Undercut panel (top of the tab).** New in 0.5.0. Every time you open the tab the moogle compares each of your listings against the current home-world floor for that item at the same quality tier, excluding your own retainer's listings. If a competitor has priced below you, they land in a highlighted table with:

- The item and HQ flag
- Your current price
- The new floor
- How much you have been undercut by
- Your retainer name and the competitor's retainer name

The panel is silent when nothing is undercut, so a quiet strip that says "your listings are all at or below the current home-world floor" is the good outcome. When rows are present, you know exactly which retainer to fly to for a re-price without hunting.

If you have [Allagan Tools](https://github.com/Critical-Impact/CriticalCommonlib) installed, the moogle uses its inventory database to keep retainer stock accurate even for retainers you have not visited this session. Without Allagan Tools, retainer info updates when you speak to the retainer.

<a id="crafting"></a>
### Crafting

Two modes. Toggle with the radio buttons at the top.

**Browse recipes.** Pulls every recipe from Lumina and shows a profit calculator: result item, quantity per craft, mat cost, sells for, post-tax profit, mat list. Filter by result name. Options: **Profitable only** (default on), **HQ output pricing** (uses HQ sale reference instead of NQ), and a **Fetch prices for shown rows** button.

The Fetch button is important. The Watchlist poller only touches items you have added, and the bag-valuation cache only touches items in your bag or retainers. Most of the 8000+ recipes are neither. Without the fetch, the Sells for / Profit columns stay as dashes for most rows. Click **Fetch prices for shown rows** and the moogle batch-requests result + mats from Universalis for the currently visible top-300 rows, then rescores. Sells for and Profit fill in.

Right-click any recipe for a context menu: **Add all mats to Watchlist**, **Copy recipe as buylist**, and a link out to the item on Universalis.

**Buylist mode.** Paste any crafting list (Artisan copy-list, TeamCraft JSON, plain text, KTM share code) and the moogle:

1. Expands each item into its raw materials via the recipe tree (toggle **Expand recipes** to see subcrafts explicitly)
2. Subtracts what you already have in inventory, saddlebag, and on retainers
3. Shows you the deficit per row, sorted by need
4. For each deficit, shows the **cheapest world on your DC** with its per-unit price
5. Kicks off an on-demand batch Universalis fetch so the cheapest-world column populates immediately even for items outside your Watchlist

**Save as Restock list** at the bottom snapshots the buylist as a Restock target list. The reverse ("Load as Buylist") lives on the Restock tab.

<a id="restock"></a>
### Restock

A concept borrowed from TradeSkillMaster and adapted to FFXIV retainer play. You define lists like "Hairpin retainer target inventory" with target quantities per item. The moogle reads your retainers' current stock and shows you the deficit: what you need to craft or buy to bring the retainer back up to target.

**On-hand toggles across the top of the tab:**

- **Include bag+crystals in on-hand.** Counts items on your character.
- **Include saddlebag in on-hand.** Counts saddlebag stock too.
- **Expand precrafts.** New in 0.5.0. Walks each deficit's recipe tree and adds subcraft rows for anything you would need to make first, with each subcraft's on-hand computed the same way. Handles multi-level recipes (a → b → c) by rolling deficits down.
- **Craft vs buy.** New in 0.5.0. Adds a "Cheaper path" column showing single-level craft cost vs the finished item's cheapest home-world listing. Highlights the cheaper option per row so you can decide whether to bother crafting at all.

**Deficit table columns:** item, HQ flag, target qty (editable inline), retainer inventory, retainer listed, bag, saddle, deficit, cheaper-path (if enabled).

**Export buttons.** Row of five (plus up to three more when Expand precrafts is on and the list has content in multiple sections):

- **KTM Export.** Copies a share code. Round-trips with **KTM Import** to hand a list to a friend running KupoTradeMaster.
- **Export List (Deficits).** Plain text: `<qty>x <name>` per line. When Expand precrafts is on AND the list has content in more than one role, the text is split into `Precrafts:`, `Final:`, and `Buylist:` sections with headers so you can eyeball the split before pasting into an editor.
- **Copy Precrafts** (only visible when Expand precrafts is on). Just the precraft deficits, no section headers, ready to paste into Artisan.
- **Copy Finals** (only visible when Expand precrafts is on). Just the final-item deficits, ready to paste into Artisan on the second pass.
- **Copy Buylist** (only visible when the deficit list has any no-recipe rows). Items you cannot craft. Route these to the Sniper or a shopping list.
- **Copy TeamCraft JSON.** Bare `[{id, amount, hq}, ...]` array for TeamCraft's crafting-list import.
- **Copy Discord markdown.** Formatted code block with star icons for HQ.
- **Copy CSV.** For spreadsheet users.

The three role-focused buttons (Precrafts / Finals / Buylist) are the workflow shortcut for Artisan users: you paste precrafts first, batch-craft them, then paste finals for the actual list you are trying to build. The Buylist section keeps items with no recipe out of Artisan entirely, so its queue only contains things it can actually work on.

<a id="sniper"></a>
### Sniper

Two modes plus a shared HQ filter.

**HQ filter dropdown at the top.** New in 0.5.0. Any quality / HQ only / NQ only. Applies to both modes. Useful when you want to snipe HQ furniture but do not care about the NQ listings polluting the view.

**Single-item mode.** Pick one item, hit **Scan now**. The moogle pulls a fresh full-listing snapshot of that item across your DC (bypassing the poller so you get up-to-the-second data), applies your buy formula, and highlights every listing at or below your threshold in green. It shows you the exact retainer name and world for each qualifying listing so you know where to go.

**Watchlist sweep mode.** Reads the poller's current snapshot for every Watchlist item, applies your buy formula per item, and lists every specific listing at or below its threshold sorted by discount percent. Different from Best Deals: Best Deals shows aggregate margins per item, Sweep tells you exactly which world and retainer to hit right now.

### Settings

The knobs.

- **Data scope:** DC selector, tax rate override.
- **Buy formula:** the default rule used by Sniper, plus a live **Formula preview** where you pick an item, type a formula, and see the numeric result.
- **Groups:** create watchlist groups, assign items to them.
- **Per-group formulas:** override the global buy formula for specific groups (raw mats vs finished goods often want different rules).
- **Custom sources:** name a formula and reuse it as a variable elsewhere.
- **Buy-signal chat channel:** where "underpriced item detected" notifications go (Notice, Echo, Debug, Urgent, Say, Party).
- **Auto-hide flags:** hide the window in cutscenes / in combat / while bound by duty.
- **Hotkey binding:** a modifier + key combo to toggle the window.
- **Wealth chart compare-to days:** how far back the delta in the Wealth header looks.
- **Price alerts (new in 0.5.0):** targeted alerts. Pick an item, threshold, HQ flag, optional note. Fires a chat message when the item's home-world minimum listing drops to or below the threshold. See [Targeted price alerts](#targeted-price-alerts) below.
- **Profit report (new in 0.5.0):** interval slider (N days between auto-shown reports; 0 = disabled) and a **Show now** button. See [Weekly profit report](#weekly-profit-report) below.
- **Reset buttons:** watchlist / groups+formulas / restock lists. Danger zone, gated behind typing **RESET** to unlock, so no stray clicks nuke your setup.
- **Share codes:** export your groups and formulas as a code, import someone else's.

## Weekly profit report

New in 0.5.0. Automatically opens as a modal window on plugin load when N days have passed since the last time it was shown (default 7, configurable in Settings, set to 0 to disable). Also openable on demand from Settings via **Show now**.

**What it shows:**

- Journal entry count in the window
- Gil earned, gil spent, and net (green if positive, red if negative)
- Top 30 items by net profit in the window, as a scrollable table

**What it does:**

- **Copy report to clipboard** puts a full markdown-formatted version on the clipboard, ready to paste into Discord or a personal log
- **Don't show again this window** snoozes the modal until the next N-day interval elapses (also fires when you close the window normally)
- Header text tells you when it will next auto-open

Turn the auto-open off with the Settings slider if you find modals annoying. You can still open the report manually any time.

## How-to guides

### How to build a watchlist from scratch

Open the **Watchlist** tab. There are two paths.

**The short path:** Type an item name in the search box at the top. Suggestions appear. Click one to add it. Repeat.

**The long path:** Open the **Bulk import** panel by clicking its header. Paste a list of item names or IDs, one per line. Names get looked up automatically. IDs (from Universalis links or Artisan exports) get added directly. TeamCraft's copy-list button gives you a JSON blob that also pastes here fine. Optionally set a group name, click **Import**, done.

### How to set up a restock list

You have a retainer holding twenty of each Grade 4 Gemsap. When any drops below twenty you want to know how many to craft to top it back up.

1. Go to the **Restock** tab.
2. Click **Create**, name it "Gemsap retainer target".
3. Search for each Gemsap, add it, set target quantity 20, tick HQ-only if that matters for you.
4. The Deficits view now shows what you are missing right now, per item.
5. Toggle on **Expand precrafts** to see the subcraft rows appear automatically (Grade 4 Gemsap's precraft mats show up as their own rows with their own on-hand tallies).
6. Toggle on **Craft vs buy** to see whether crafting each row is actually cheaper than buying it outright.
7. **Copy Precrafts** and paste into Artisan for the first batch. Craft. Come back.
8. **Copy Finals** and paste into Artisan for the finished items.
9. If any items showed up in the **Buylist:** section (no recipe available), use **Copy Buylist** and hunt them down via the Sniper tab or the Best Deals tab.

Share the list with a friend via **KTM Export**. They paste it in their own **KTM Import** and instantly have the same target list.

### How to catch when you have been undercut

Open the **Retainers** tab. The panel across the top is the answer.

Every time you open the tab the moogle scans your active retainer listings against the current home-world snapshot. Any listing where a competitor has priced below yours (same quality tier, on your home world, excluding your other retainers) lands in the panel with the item, both prices, the gap, and which retainer needs the trip. Silent when there is nothing to report.

The scan uses the last poll cycle's data, so it is up to 30 seconds stale. Trigger a manual poll from the Watchlist tab (Refresh Now button) if you want fresher numbers.

### How to snipe a specific item

1. Go to the **Sniper** tab.
2. Make sure **Single item** mode is selected.
3. Set the **HQ filter** at the top if you only want one quality.
4. Type the item name in the search bar and pick it.
5. Set your buy formula in the field below (see [Custom buy formulas](#custom-buy-formulas)). Something like `mbminhw * 0.7` means "70% of the cheapest listing on my home world."
6. Click **Scan now**.
7. Any listing at or below your threshold turns green. The row shows the retainer name and world.
8. Open the market board in game, sort by unit price, and buy the flagged listings.

### How to use the crafting buylist for a big order

Your linkshell needs forty of some crafted item for a raid week.

1. Type your intended crafts into Artisan or TeamCraft, then copy the crafting list.
2. Go to the **Crafting** tab in KupoTradeMaster, switch to Buylist mode, paste the list into the import box.
3. The moogle expands the 40 finished items into the raw materials you would need, subtracts what you already have on you and on retainers, and shows the deficit.
4. For each row it also tells you the cheapest world in your DC to buy that raw material from, and the per-unit price there.
5. Fly around and buy the deficits. When you come back with the mats, refresh the tab. Rows go green as they fill.

### How to check whether an item is profitable to flip

Add it to your Watchlist. Wait one poll cycle (about 30 seconds). Then:

- **Best Deals row:** post-tax margin column tells you gil-per-unit if you buy the DC cheapest and sell on your home world at the current undercut.
- **Arbitrage row:** post-tax profit-per-unit and est-profit-all columns tell you what a full run would clear.
- **Freshness (min) column:** if the number is over 60, the data is more than an hour stale and might be lying. Wait for it to update or check the market board in game.

If both tabs agree the flip is real, go.

### How to browse recipes for profit

New workflow enabled by 0.5.0's on-demand price fetch.

1. Go to the **Crafting** tab, **Browse recipes** mode.
2. Filter by result name if you have a category in mind ("Gemsap", "elemental cluster", "final fantasy").
3. Click **Fetch prices for shown rows**. The moogle batch-fetches Universalis prices for the result + all mats across the top-300 rows.
4. **Sells for** and **Profit (post-tax)** columns fill in.
5. Toggle **Profitable only** to hide unprofitable rows.
6. Right-click a row to add all its mats to your Watchlist so future browses have data even without a fetch.

## Custom buy formulas

The moogle uses a small formula language for buy-price rules. It is inspired by TradeSkillMaster's DSL and boils down to arithmetic on live market values.

**Available sources:**

| Token | Meaning |
|-------|---------|
| `mbmin` | Cheapest listing on your data center |
| `mbminhw` | Cheapest listing on your **home world** |
| `mbavg` | Data-center-wide average sale price |
| `mbavghw` | Home-world-only average sale price |
| `dbmarket` | Universalis current average price |
| `velocity` | Sales per day |
| `vendor` | NPC vendor buy price (0 if not vendor-sold) |
| `taxrate` | Your current tax rate as a decimal (0.05 = 5%) |

**Operators:** `+`  `-`  `*`  `/`  parentheses.

**Examples:**

```
mbminhw * 0.7
```
"Alert when a listing is 70% of the cheapest current listing on my home world." Basic underprice-sniper rule.

```
dbmarket * 0.5
```
"Alert at half of Universalis average." Aggressive.

```
mbavghw * (1 - taxrate) - 1000
```
"Alert when I could resell at my home world's average, pay tax, and still profit at least 1000 gil per unit."

```
max(vendor + 100, mbminhw * 0.6)
```
"Alert at either 100 gil above vendor floor or 60% of home-world minimum, whichever is higher." Useful for items with cheap vendor sources.

**Preview it before you commit.** In the Settings tab, under **Formula preview**, pick an item, type your formula, click **Evaluate**, and the moogle tells you exactly what number the formula produces for that item right now. Faster than saving, waiting for a poll, and realising you had a typo.

## Targeted price alerts

New in 0.5.0. The simple version of custom formulas: pick an item, pick a threshold, get pinged. No syntax to learn.

**Setting one up (Settings tab):**

1. Scroll to **Price alerts**.
2. Type an item name in the search box.
3. Pick the match from the dropdown.
4. Set the gil threshold.
5. Tick **HQ only** if you only want to be pinged for HQ listings.
6. Optional: type a note ("crafting mat for skysteel", "hoarding at this price"). It shows up in the chat alert.
7. Click **Add alert**.

The alert fires the moment the item's home-world minimum drops to or below your threshold. Delivered to whichever chat channel you have set in Settings (default: Notice). The alert line looks like:

```
[KupoTradeMaster] Rose Garnet Ore (HQ) is at 720g, under alert threshold 800g (hoarding at this price)
```

Each alert has a toggle in the alerts table for enable / disable. Remove permanently with the row's **remove** button. Alerts share the same per-item mute cooldown as the formula-based buy signals: silencing one silences the other for the same item.

## Sharing lists and formulas

KupoTradeMaster has its own share-code format (**KM1:**) for round-tripping data with other users of the plugin. Two flavours:

- **KTM Export** on the Restock tab: shares a whole Restock list (name, items, targets, HQ flags, on-hand toggles) as one code.
- **Share** button in Settings: shares your groups, group formulas, custom sources, and buy formula as one code.

Both are copied to the clipboard. Recipient pastes into the matching **Import** field and their local state gets a new list / new groups. Nothing else is touched.

For sharing lists with users of other tools:

- **TeamCraft users:** Restock tab → **Copy TeamCraft JSON** copies a bare `[{id, amount, hq}]` array that TeamCraft's crafting-list import understands.
- **Discord chat:** Restock tab → **Copy Discord markdown** wraps a formatted code block ready to drop in linkshell chat.
- **Spreadsheets:** Restock tab → **Copy CSV**.

## Plays well with others

Everything is optional. The moogle uses these plugins if they are present and quietly gets less clever if they are not:

- **[Allagan Tools](https://github.com/Critical-Impact/CriticalCommonlib):** unlocks retainer inventory tracking for retainers you have not visited this session. Highly recommended for anyone running multiple retainers.
- **[AutoRetainer](https://github.com/PunishXIV/AutoRetainer):** the moogle reads AutoRetainer's retainer venture data to time inventory refreshes.
- **[Artisan](https://github.com/PunishXIV/Artisan):** the moogle understands Artisan's copy-list format, so pasting a crafting list into KupoTradeMaster just works. The Restock tab's Copy Precrafts / Copy Finals output is designed to paste directly into Artisan's list import.
- **[MarketBoardPlugin](https://github.com/PhilippeCharriere/MarketBoardPlugin):** the moogle uses its addon hooks to detect market-board opens for freshness detection.
- **[PriceInsight](https://github.com/hpx7/PriceInsight):** happily coexists. Turn off KupoTradeMaster's in-game tooltip in Settings if you prefer PriceInsight's.
- **[Dagobert](https://github.com/Cufiy/Dagobert):** does the auto-undercutting KupoTradeMaster refuses to do. Perfect complement to the Undercut panel: Dagobert re-lists at the new floor, KupoTradeMaster tells you what the new floor is.

## Settings worth knowing about

A few settings are worth surfacing because a lot of people miss them.

**Auto-hide in cutscenes / combat / duty.** All three flags are opt-in. Turn them on if the plugin window keeps covering something important during raids.

**Hotkey to toggle the window.** Default off. Bind a key combo (Ctrl+K, F5, whatever) and stop typing `/kupo` a hundred times a day.

**Buy-signal chat channel.** When an item drops below your buy formula and you have chat notifications on, the alert lands in the channel you pick here. Notice is the default; some people prefer Echo so no one else in party sees it. **Targeted price alerts** use the same channel.

**Wealth chart compare-to days.** The delta shown in the Wealth header ("up 340k since a week ago") uses whatever number of days you put here. 30 is a nice long-view value if you are casual.

**Profit report interval.** Slider from 0 to 30 days. 0 disables the auto-open modal entirely; you can still open it on demand from **Show now**.

**Danger zone reset buttons.** Deep in the Settings tab. Type **RESET** into the text box to unlock them. Then Reset Watchlist / Groups+Formulas / Restock Lists are one click away. Only use these if you are certain, kupo.

## Troubleshooting

**All my Watchlist prices are dashes.** Universalis is either slow or degraded. Check /xllog for lines starting with `KupoTradeMaster: Universalis returned N/M items`. If N is much less than M, Universalis is dropping requests. Data will backfill on subsequent poll ticks. If it stays at 0/M for more than a few minutes, check [Universalis status](https://universalis.app/) directly.

**Crafting → Browse recipes shows dashes for Sells for / Profit.** Expected. Those columns require live prices, and the Watchlist poller only touches items you have watchlisted. Click **Fetch prices for shown rows** to batch-request them for the current filter results.

**Undercut panel is empty when I know I have been undercut.** The scan uses the last poll cycle's data. If you have not polled recently, undercuts are invisible. Trigger a Watchlist refresh (Refresh Now button on the Watchlist tab) and reopen the Retainers tab. Also: the scan needs your retainer prices to be captured, so visit the retainer once so KupoTradeMaster reads its sell-list addon.

**"Sell?" instead of "Sell" in the Journal.** Should not happen after 0.4.x, since retainer inventory delta detection distinguishes cancellations from sales. If it does happen, the retainer's inventory did not refresh in time. Speak to the retainer once and re-check.

**Wealth chart is empty.** The moogle needs at least two wealth samples to draw a line. Wait five minutes and check again.

**Bag / Saddlebag show 0.** Open your bag and saddlebag in game once. Lazy-loading containers do not populate until the UI has opened them at least once per session. This is a game engine thing.

**Bag items with margin look impossibly high.** The moogle uses home-world scoping to fix the classic "one Gysahl Green sold for 3M on Faerie" problem. If you still see fantasy numbers, check the Group filter is set to "All" and the Data Scope in Settings matches your actual data center.

**Retainers page shows old listings.** Speak to the retainer in game to refresh. With Allagan Tools installed the refresh happens automatically.

**Weekly profit report keeps opening.** If you close it with the window X, it snoozes automatically to the next N-day interval. If you want it silent for a long stretch, set the interval slider to 0 in Settings.

**Something is wrong that is not on this list.** Open a GitHub issue at [https://github.com/hanamigg/kupotrademaster/issues](https://github.com/hanamigg/kupotrademaster/issues) with a description of what you were doing and a snippet from /xllog. The moogle appreciates a good bug report.

## Support the moogle

KupoTradeMaster is free and always will be, kupo. If it saved you a couple hours of spreadsheet work this week and you feel like buying the developer a coffee, the [Ko-fi link](https://ko-fi.com/hanamivgc) is there for exactly that. There is also a Ko-fi button on the plugin's tab bar that opens the page in your browser.

**Join the Discord.** Trade tips, feature requests, help each other set up formulas, complain about the market: [https://discord.gg/aVgx6dVSre](https://discord.gg/aVgx6dVSre). Come say kupo.

Contributions of code, bug reports, and feature requests are all welcome. This plugin exists because a few people wanted it to.

## Credits

Live market data from [Universalis](https://universalis.app/). The API is free and community-run, and the whole plugin genre owes them.

Built on [Dalamud](https://github.com/goatcorp/Dalamud), the FFXIV plugin platform.

Inspired by [TradeSkillMaster](https://tradeskillmaster.com/) (the WoW gold-farming toolkit that defined the price-rules-and-groups design) and the OSRS Flipping Utilities family of tools.

Logo, plugin, and moogle vibes by hanamigg.

Kupo.
