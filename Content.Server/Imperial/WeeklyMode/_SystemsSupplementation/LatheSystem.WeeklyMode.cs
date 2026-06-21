using Content.Server.Lathe.Components;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.WeeklyMode;
using Robust.Shared.Prototypes;

namespace Content.Server.Lathe;

public sealed partial class LatheSystem
{
    public bool TryAddWeeklyToQueue(EntityUid uid, WeeklyLatheRecipeData recipe, int quantity, LatheComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (quantity <= 0)
            return false;
        quantity = int.Min(quantity, MaxItemsPerRequest);

        if (!CanProduceWeekly(uid, recipe, quantity, component))
            return false;

        foreach (var (mat, amount) in GetAdjustedWeeklyAmount(component, recipe))
            _materialStorage.TryChangeMaterialAmount(uid, mat, -amount * quantity);

        if (component.Queue.Last is { } node && node.ValueRef.IsWeekly && node.ValueRef.Recipe == recipe.RecipeId)
            node.ValueRef.ItemsRequested += quantity;
        else
            component.Queue.AddLast(new LatheRecipeBatch(recipe.RecipeId, true, 0, quantity));

        return true;
    }

    private void OnWeeklyRecipesChanged(WeeklyRecipesChangedEvent args)
    {
        var query = EntityQueryEnumerator<LatheComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            _materialStorage.UpdateMaterialWhitelist(uid);
            UpdateUserInterfaceState(uid, component);
        }
    }

    private static IEnumerable<(ProtoId<MaterialPrototype> mat, int amount)> GetAdjustedWeeklyAmount(LatheComponent lathe, WeeklyLatheRecipeData recipe)
    {
        foreach (var material in recipe.Materials)
        {
            var adjustedAmount = recipe.ApplyMaterialDiscount
                ? (int)(material.Amount * lathe.MaterialUseMultiplier)
                : material.Amount;
            yield return (new ProtoId<MaterialPrototype>(material.MaterialId), adjustedAmount);
        }
    }

    public bool CanProduceWeekly(EntityUid uid, WeeklyLatheRecipeData recipe, int amount = 1, LatheComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!_weeklyMode.TryGetAvailableWeeklyLatheRecipe(uid, component, recipe.RecipeId, out _))
            return false;

        if (amount <= 0)
            return false;

        foreach (var (material, needed) in GetAdjustedWeeklyAmount(component, recipe))
        {
            if (_materialStorage.GetMaterialAmount(uid, material) < needed * amount)
                return false;
        }

        return true;
    }

    private string GetBatchRecipeName(LatheRecipeBatch batch)
    {
        if (!batch.IsWeekly)
            return GetRecipeName(new ProtoId<LatheRecipePrototype>(batch.Recipe));

        return _weeklyMode.TryGetActiveWeeklyLatheRecipe(batch.Recipe, out var recipe)
            ? recipe.Name
            : batch.Recipe;
    }

    private string GetCurrentRecipeName(LatheComponent component)
    {
        if (component.CurrentRecipe == null)
            return string.Empty;

        if (!component.CurrentRecipeIsWeekly)
            return GetRecipeName(new ProtoId<LatheRecipePrototype>(component.CurrentRecipe));

        return _weeklyMode.TryGetActiveWeeklyLatheRecipe(component.CurrentRecipe, out var recipe)
            ? recipe.Name
            : component.CurrentRecipe;
    }
}
