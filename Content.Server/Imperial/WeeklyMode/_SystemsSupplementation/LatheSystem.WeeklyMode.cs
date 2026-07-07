using System.Linq;
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

        var materialSnapshot = GetAdjustedWeeklyMaterialSnapshot(component, recipe).ToList();
        foreach (var material in materialSnapshot)
            _materialStorage.TryChangeMaterialAmount(uid, new ProtoId<MaterialPrototype>(material.MaterialId), -material.Amount * quantity);

        if (component.Queue.Last is { } node && node.ValueRef.IsWeekly && node.ValueRef.Recipe == recipe.RecipeId)
            node.ValueRef.ItemsRequested += quantity;
        else
        {
            component.Queue.AddLast(new LatheRecipeBatch(
                recipe.RecipeId,
                true,
                0,
                quantity,
                materialSnapshot,
                recipe.ResultPrototype,
                recipe.ResultAmount,
                recipe.Name,
                recipe.ProductionTimeSeconds));
        }

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

    private static IEnumerable<WeeklyLatheRecipeMaterialData> GetAdjustedWeeklyMaterialSnapshot(LatheComponent lathe, WeeklyLatheRecipeData recipe)
    {
        foreach (var material in recipe.Materials)
        {
            var adjustedAmount = recipe.ApplyMaterialDiscount
                ? (int)(material.Amount * lathe.MaterialUseMultiplier)
                : material.Amount;
            yield return new WeeklyLatheRecipeMaterialData
            {
                MaterialId = material.MaterialId,
                Amount = adjustedAmount,
            };
        }
    }

    private static IEnumerable<(ProtoId<MaterialPrototype> mat, int amount)> GetAdjustedWeeklyAmount(LatheComponent lathe, WeeklyLatheRecipeData recipe)
    {
        return GetAdjustedWeeklyMaterialSnapshot(lathe, recipe)
            .Select(material => (new ProtoId<MaterialPrototype>(material.MaterialId), material.Amount));
    }

    private static IEnumerable<(ProtoId<MaterialPrototype> mat, int amount)> GetWeeklyMaterialSnapshot(LatheRecipeBatch batch, LatheComponent lathe, WeeklyLatheRecipeData? fallbackRecipe)
    {
        if (batch.WeeklyMaterialSnapshot.Count > 0)
        {
            foreach (var material in batch.WeeklyMaterialSnapshot)
                yield return (new ProtoId<MaterialPrototype>(material.MaterialId), material.Amount);
            yield break;
        }

        if (fallbackRecipe is not { } recipe)
            yield break;

        foreach (var material in GetAdjustedWeeklyAmount(lathe, recipe))
            yield return material;
    }

    private static IEnumerable<(ProtoId<MaterialPrototype> mat, int amount)> GetCurrentWeeklyMaterialSnapshot(LatheComponent component, WeeklyLatheRecipeData? fallbackRecipe)
    {
        if (component.CurrentWeeklyMaterialSnapshot.Count > 0)
        {
            foreach (var material in component.CurrentWeeklyMaterialSnapshot)
                yield return (new ProtoId<MaterialPrototype>(material.MaterialId), material.Amount);
            yield break;
        }

        if (fallbackRecipe is not { } recipe)
            yield break;

        foreach (var material in GetAdjustedWeeklyAmount(component, recipe))
            yield return material;
    }

    private bool TryGetWeeklyBatchData(LatheRecipeBatch batch, out WeeklyLatheRecipeData recipe)
    {
        if (_weeklyMode.TryGetActiveWeeklyLatheRecipe(batch.Recipe, out recipe))
            return true;

        if (string.IsNullOrWhiteSpace(batch.WeeklyResultPrototype))
            return false;

        recipe = new WeeklyLatheRecipeData
        {
            RecipeId = batch.Recipe,
            Name = string.IsNullOrWhiteSpace(batch.WeeklyRecipeName) ? batch.Recipe : batch.WeeklyRecipeName,
            ResultPrototype = batch.WeeklyResultPrototype,
            ResultAmount = batch.WeeklyResultAmount,
            ProductionTimeSeconds = batch.WeeklyProductionTimeSeconds,
            Materials = batch.WeeklyMaterialSnapshot,
        };
        return true;
    }

    private static void SetCurrentWeeklyRecipeSnapshot(LatheComponent component, LatheRecipeBatch batch, WeeklyLatheRecipeData recipe)
    {
        component.CurrentWeeklyMaterialSnapshot = GetWeeklyMaterialSnapshot(batch, component, recipe).Select(material => new WeeklyLatheRecipeMaterialData
        {
            MaterialId = material.mat.Id,
            Amount = material.amount,
        }).ToList();
        component.CurrentWeeklyResultPrototype = string.IsNullOrWhiteSpace(batch.WeeklyResultPrototype)
            ? recipe.ResultPrototype
            : batch.WeeklyResultPrototype;
        component.CurrentWeeklyResultAmount = batch.WeeklyResultAmount > 0 ? batch.WeeklyResultAmount : recipe.ResultAmount;
        component.CurrentWeeklyRecipeName = string.IsNullOrWhiteSpace(batch.WeeklyRecipeName)
            ? recipe.Name
            : batch.WeeklyRecipeName;
    }

    private static void ClearCurrentWeeklyRecipeSnapshot(LatheComponent component)
    {
        component.CurrentWeeklyMaterialSnapshot.Clear();
        component.CurrentWeeklyResultPrototype = string.Empty;
        component.CurrentWeeklyResultAmount = 1;
        component.CurrentWeeklyRecipeName = string.Empty;
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
            : string.IsNullOrWhiteSpace(batch.WeeklyRecipeName) ? batch.Recipe : batch.WeeklyRecipeName;
    }

    private string GetCurrentRecipeName(LatheComponent component)
    {
        if (component.CurrentRecipe == null)
            return string.Empty;

        if (!component.CurrentRecipeIsWeekly)
            return GetRecipeName(new ProtoId<LatheRecipePrototype>(component.CurrentRecipe));

        if (!string.IsNullOrWhiteSpace(component.CurrentWeeklyRecipeName))
            return component.CurrentWeeklyRecipeName;

        return _weeklyMode.TryGetActiveWeeklyLatheRecipe(component.CurrentRecipe, out var recipe)
            ? recipe.Name
            : component.CurrentRecipe;
    }
}
