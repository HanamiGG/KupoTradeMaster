using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace KupoTradeMaster.Services;

/// Reads Lumina's Recipe sheet and exposes recipe rows for the Crafting tab. Kept intentionally
/// thin: no pricing, no persistence — CraftingTab combines these with the poller/valuation caches
/// to compute profit. Cached lazily because the sheet has ~50k rows and we want first-open fast.
public sealed class CraftingService
{
    private readonly IDataManager _data;
    private readonly object _sync = new();
    private List<CraftingRecipe>? _recipes;

    public CraftingService(IDataManager data) => _data = data;

    public IReadOnlyList<CraftingRecipe> All()
    {
        if (_recipes != null) return _recipes;
        lock (_sync)
        {
            if (_recipes != null) return _recipes;
            _recipes = Build();
        }
        return _recipes;
    }

    private List<CraftingRecipe> Build()
    {
        var list = new List<CraftingRecipe>(capacity: 8000);
        var sheet = _data.GetExcelSheet<Recipe>();
        if (sheet == null) return list;

        foreach (var row in sheet)
        {
            var resultId = row.ItemResult.RowId;
            if (resultId == 0) continue;

            var ings = new List<CraftingIngredient>(capacity: 4);
            // Recipe sheet has ~10 ingredient slots. Lumina exposes these as parallel collections;
            // guard against mismatched lengths just in case.
            var slotCount = Math.Min(row.Ingredient.Count, row.AmountIngredient.Count);
            for (var i = 0; i < slotCount; i++)
            {
                var ing = row.Ingredient[i];
                var qty = row.AmountIngredient[i];
                if (ing.RowId == 0 || qty == 0) continue;
                ings.Add(new CraftingIngredient(ing.RowId, qty));
            }
            if (ings.Count == 0) continue;

            list.Add(new CraftingRecipe(
                RecipeId: row.RowId,
                ResultItemId: resultId,
                ResultQuantity: row.AmountResult,
                Ingredients: ings));
        }
        return list;
    }
}

public sealed record CraftingRecipe(uint RecipeId, uint ResultItemId, byte ResultQuantity, IReadOnlyList<CraftingIngredient> Ingredients);
public readonly record struct CraftingIngredient(uint ItemId, byte Quantity);
