using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

[DataDefinition]
public sealed partial class ImperialTgCraftRecipe
{
    /// <summary>Ключ локализации названия рецепта.</summary>
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public EntProtoId Result;

    [DataField]
    public List<ImperialTgCraftItem> Items = new();

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = new();

    /// <summary>Время сборки в секундах.</summary>
    [DataField]
    public float Time = 1f;
}
