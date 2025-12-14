
using Content.Server.Popups;
using Content.Shared.Imperial.Medieval.Chemistry;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Medieval.Chemistry;

public sealed class RecipeBookSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalRecipeBookComponent, InteractUsingEvent>(PutRecipe);
    }
    public void PutRecipe(EntityUid uid, MedievalRecipeBookComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        if (!TryComp<MedievalRandomChemistryRecipeComponent>(args.Used, out var recipe))
            return;
        if (component.Recipes.Contains(recipe.Reagent.ID))
        {
            _popup.PopupEntity(Loc.GetString("imperial-medieval-recipe-already"), uid, args.User);
            return;
        }
        component.Recipes.Add(recipe.Reagent.ID);
        QueueDel(args.Used);
        _popup.PopupEntity(Loc.GetString("imperial-medieval-recipe-inserted"), uid, args.User);
        if (_ui.HasUi(uid, RecipeBookUi.Key))
        {
            _ui.SetUiState(uid, RecipeBookUi.Key,
                new PotionBookUserInterfaceState() { Ids = component.Recipes });
        }
    }
}
