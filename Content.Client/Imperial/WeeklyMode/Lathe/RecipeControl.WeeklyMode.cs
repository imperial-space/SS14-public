using Content.Client.Lathe;
using Content.Client.Lathe.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Imperial.WeeklyMode.Lathe;

public sealed partial class WeeklyRecipeControl
{
    public WeeklyRecipeControl(LatheSystem latheSystem, string recipeId, string recipeName, Func<string> tooltipTextSupplier, bool canProduce, Control displayControl)
    {
        RobustXamlLoader.Load(this);

        _latheSystem = latheSystem;
        _recipeId = recipeId;
        TooltipTextSupplier = tooltipTextSupplier;
        SetRecipe(recipeId, recipeName);
        SetCanProduce(canProduce);
        SetDisplayControl(displayControl);

        Button.OnPressed += _ =>
        {
            OnButtonPressed?.Invoke(_recipeId);
        };
        Button.TooltipSupplier = SupplyTooltip;
    }

    public void SetRecipe(string recipeId, string recipeName)
    {
        RecipeName.Text = recipeName;
        _recipeId = recipeId;
    }
}
