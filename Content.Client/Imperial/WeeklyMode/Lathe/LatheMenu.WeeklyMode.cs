using System.Linq;
using System.Text;
using Content.Client.UserInterface.Controls;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Lathe.UI;

public sealed partial class LatheMenu
{
    public List<WeeklyLatheRecipeData> WeeklyRecipes = new();

    private List<WeeklyLatheRecipeData> GetWeeklyRecipesToShow()
    {
        var weeklyRecipesToShow = new List<WeeklyLatheRecipeData>();
        if (CurrentCategory != null)
            return weeklyRecipesToShow;

        foreach (var recipe in WeeklyRecipes)
        {
            if (SearchBar.Text.Trim().Length != 0)
            {
                var normalizedRecipeName = NormalizeRussianText(recipe.Name.ToLowerInvariant());
                var normalizedSearchText = NormalizeRussianText(SearchBar.Text.Trim().ToLowerInvariant());
                if (normalizedRecipeName.Contains(normalizedSearchText))
                    weeklyRecipesToShow.Add(recipe);
            }
            else
            {
                weeklyRecipesToShow.Add(recipe);
            }
        }

        return weeklyRecipesToShow;
    }

    private IOrderedEnumerable<WeeklyLatheRecipeData> SortWeeklyRecipesToShow(IEnumerable<WeeklyLatheRecipeData> recipes)
    {
        return recipes.OrderBy(recipe => recipe.Name, StringComparer.Ordinal);
    }

    private int PopulateWeeklyRecipes(
        IEnumerable<WeeklyLatheRecipeData> recipes,
        int idx,
        int oldChildCount,
        int quantity,
        LatheComponent? lathe)
    {
        foreach (var recipe in recipes)
        {
            var canProduce = CanProduceWeeklyRecipe(recipe, quantity, lathe);
            var tooltipFunction = () => GenerateTooltipText(recipe);

            if (idx >= oldChildCount)
            {
                var control = new RecipeControl(_lathe, recipe.RecipeId, recipe.Name, tooltipFunction, canProduce, GetRecipeDisplayControl(recipe));
                control.OnButtonPressed += s =>
                {
                    if (!int.TryParse(AmountLineEdit.Text, out var amount) || amount <= 0)
                        amount = 1;
                    RecipeQueueAction?.Invoke(s, amount);
                };
                RecipeList.AddChild(control);
            }
            else
            {
                var child = RecipeList.GetChild(idx) as RecipeControl;

                if (child == null)
                {
                    DebugTools.Assert($"Lathe menu recipe control at {idx} is not of type RecipeControl");
                    continue;
                }

                child.SetRecipe(recipe.RecipeId, recipe.Name);
                child.SetTooltipSupplier(tooltipFunction);
                child.SetCanProduce(canProduce);
                child.SetDisplayControl(GetRecipeDisplayControl(recipe));
            }
            idx++;
        }

        return idx;
    }

    private string GenerateTooltipText(WeeklyLatheRecipeData recipe)
    {
        StringBuilder sb = new();
        var multiplier = _entityManager.GetComponent<LatheComponent>(Entity).MaterialUseMultiplier;

        foreach (var material in recipe.Materials)
        {
            if (!_prototypeManager.Resolve<MaterialPrototype>(material.MaterialId, out var proto))
                continue;

            var adjustedAmount = SharedLatheSystem.AdjustMaterial(material.Amount, recipe.ApplyMaterialDiscount, multiplier);
            var sheetVolume = _materialStorage.GetSheetVolume(proto);

            var unit = Loc.GetString(proto.Unit);
            var sheets = adjustedAmount / (float) sheetVolume;

            var availableAmount = _materialStorage.GetMaterialAmount(Entity, material.MaterialId);
            var missingAmount = Math.Max(0, adjustedAmount - availableAmount);
            var missingSheets = missingAmount / (float) sheetVolume;

            var name = Loc.GetString(proto.Name);

            string tooltipText;
            if (missingSheets > 0)
            {
                tooltipText = Loc.GetString("lathe-menu-material-amount-missing", ("amount", sheets), ("missingAmount", missingSheets), ("unit", unit), ("material", name));
            }
            else
            {
                var amountText = Loc.GetString("lathe-menu-material-amount", ("amount", sheets), ("unit", unit));
                tooltipText = Loc.GetString("lathe-menu-tooltip-display", ("material", name), ("amount", amountText));
            }

            sb.AppendLine(tooltipText);
        }

        if (!string.IsNullOrWhiteSpace(recipe.Description))
            sb.AppendLine(Loc.GetString("lathe-menu-description-display", ("description", recipe.Description)));

        if (sb.Length > 0)
            sb.Remove(sb.Length - 1, 1);

        return sb.ToString();
    }

    private bool CanProduceWeeklyRecipe(WeeklyLatheRecipeData recipe, int quantity, LatheComponent? lathe)
    {
        if (lathe == null || quantity <= 0)
            return false;

        foreach (var material in recipe.Materials)
        {
            var adjustedAmount = SharedLatheSystem.AdjustMaterial(material.Amount, recipe.ApplyMaterialDiscount, lathe.MaterialUseMultiplier);
            if (_materialStorage.GetMaterialAmount(Entity, material.MaterialId) < adjustedAmount * quantity)
                return false;
        }

        return true;
    }

    private bool TrySetWeeklyQueueInfo(string recipeId, bool isWeekly)
    {
        if (!isWeekly)
            return false;

        if (TryGetWeeklyRecipe(recipeId, out var recipe))
        {
            FabricatingDisplayContainer.AddChild(GetRecipeDisplayControl(recipe));
            NameLabel.Text = string.IsNullOrWhiteSpace(recipe.Name) ? recipeId : recipe.Name;
        }
        else
        {
            FabricatingDisplayContainer.AddChild(new Control());
            NameLabel.Text = recipeId;
        }

        return true;
    }

    public Control GetRecipeDisplayControl(WeeklyLatheRecipeData recipe)
    {
        if (recipe.Icon != SpriteSpecifier.Invalid)
        {
            var textRect = new TextureRect();
            textRect.Texture = _spriteSystem.Frame0(recipe.Icon);
            return textRect;
        }

        if (!string.IsNullOrWhiteSpace(recipe.ResultPrototype))
        {
            var entProtoView = new EntityPrototypeView();
            entProtoView.SetPrototype(recipe.ResultPrototype);
            return entProtoView;
        }

        return new Control();
    }

    private string GetQueuedRecipeName(LatheRecipeBatch batch)
    {
        if (!batch.IsWeekly)
            return _lathe.GetRecipeName(new ProtoId<LatheRecipePrototype>(batch.Recipe));

        return TryGetWeeklyRecipe(batch.Recipe, out var recipe) && !string.IsNullOrWhiteSpace(recipe.Name)
            ? recipe.Name
            : batch.Recipe;
    }

    private Control GetQueuedRecipeDisplayControl(LatheRecipeBatch batch)
    {
        if (!batch.IsWeekly)
            return GetRecipeDisplayControl(_prototypeManager.Index<LatheRecipePrototype>(batch.Recipe));

        return TryGetWeeklyRecipe(batch.Recipe, out var recipe)
            ? GetRecipeDisplayControl(recipe)
            : new Control();
    }

    private bool TryGetWeeklyRecipe(string recipeId, out WeeklyLatheRecipeData recipe)
    {
        foreach (var candidate in WeeklyRecipes)
        {
            if (!string.Equals(candidate.RecipeId, recipeId, StringComparison.Ordinal))
                continue;

            recipe = candidate;
            return true;
        }

        recipe = default;
        return false;
    }
}
