using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>
/// Предмет, из которого по рецептам TG (code/datums/components/crafting) собирается что-то новое:
/// наборы деталей оружия, технологический патрон. Нужные предметы ищутся в руках и рядом с игроком.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialTgCraftComponent : Component
{
    [DataField(required: true)]
    public List<ImperialTgCraftRecipe> Recipes = new();

    /// <summary>Радиус поиска предметов вокруг игрока.</summary>
    [DataField]
    public float Range = 1.5f;
}
