<h1>
  <img src="logo.png" alt="KupoTradeMaster" width="48" align="left" style="margin-right: 10px;"/>
  KupoTradeMaster
</h1>

[![Support on Ko-fi](https://img.shields.io/badge/Support-Ko--fi-FF5E5B?logo=ko-fi&logoColor=white)](https://ko-fi.com/hanamivgc) ![Discord](https://img.shields.io/discord/1517370260976828436?style=flat&logo=%235865F2&logoColor=violet&logoSize=auto&link=https%3A%2F%2Fdiscord.gg%2FaVgx6dVSre)



A market-board sidekick for FFXIV crafters and flippers. Find good buys, know what your retainers need, and track every gil you spend and earn, all in one plugin. No auto-clickers, no bots. It shows you the numbers. You decide.

## What you get

- **A ranked feed of what to flip.** Your watchlist, sorted by real post-tax profit, updated as prices move.
- **Cross-world arbitrage.** Buy where it's cheap, sell where you play. The math is done for you.
- **A precision sniper.** Point it at the watchlist and it tells you which specific retainer on which world to hit right now, sorted by how far below your buy rule the listing sits.
- **An automatic trade journal.** Purchases log themselves. Retainer sales log themselves too, distinguished from cancellations so you see "Sell", not "Sell?". Session profit is right there at the top.
- **A wealth graph.** Watch your gil grow. Cash, bags, saddlebag, retainers, and active listings all counted, plotted over any window from two hours to a full week. Shows "up X since a week ago" right in the header.
- **Restock lists.** Say what each retainer should hold. Get a to-craft list of the gap. Export the whole list as a Discord message, a spreadsheet CSV, a TeamCraft JSON, or a share code your friends can import in one click.
- **A crafting buylist.** Paste any Artisan or TeamCraft list. See only the raw materials you still need, with the cheapest world to buy each from. Save any buylist as a restock target and vice versa.
- **Realistic pricing.** Sell values are scoped to your own home world. No more fantasy margins from a mispriced foreign listing that pretends Gysahl Greens are worth three million gil.
- **Custom buy rules.** Set your own definition of a good price. Something like `dbmarket * 0.7` gets flagged the moment an item drops below 70% of average. A preview evaluator lets you test the rule against any item before you commit to it.
- **Multi-character.** Every journal entry gets tagged by which character earned or spent the gil, so alt rollups stay clean.
- **In-game tooltip.** Hover any item and see the current market floor. Toggle it off if you already use another price tooltip.
- **Auto-hide.** Optionally have the window disappear during cutscenes, combat, or duties, so it never covers your fight UI.
- **Hotkey to open.** Bind a key combination to toggle the window without typing a command.

## Install

In game, type `/xlsettings`. Under the Experimental tab, paste this into Custom Plugin Repositories and click plus:

```
https://raw.githubusercontent.com/hanamigg/kupotrademaster/main/pluginmaster.json
```

Save. Open the plugin installer with `/xlplugins`, search for KupoTradeMaster, and hit Install. Then `/ktm` opens the window.

## Plays with

Allagan Tools, AutoRetainer, Artisan, MarketBoardPlugin, PriceInsight, and Dagobert. All optional. Each one you have installed makes KupoTradeMaster a little smarter. Allagan Tools in particular lets it track retainer inventory even for retainers you haven't visited this session.

## Will not do

No auto-posting, no auto-cancelling, no auto-buying. If you want a real undercutter, Dagobert is the community standard and it does that job well. KupoTradeMaster is the "help me decide what to trade" tool.


## Credits

Market data from Universalis. Built on Dalamud. Inspired by TradeSkillMaster and OSRS Flipping Utilities.

