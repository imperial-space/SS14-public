using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Кристаллизатор TG: превращает газ из окружающей среды в кристаллы по рецептам.</summary>
[RegisterComponent]
public sealed partial class ImperialCrystallizerComponent : Component
{
    [DataField]
    public List<ImperialCrystallizerRecipe> Recipes = new();

    [ViewVariables]
    public int Selected;

    [ViewVariables]
    public float Progress;

    [DataField]
    public bool Enabled;
}
