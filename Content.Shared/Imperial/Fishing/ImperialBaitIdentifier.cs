using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Опознание наживки (favorite_bait / disliked_bait).</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class ImperialBaitIdentifier
{
    /// <summary>Прототип предмета (с наследниками).</summary>
    [DataField]
    public EntProtoId? Proto;

    /// <summary>Типы пищи.</summary>
    [DataField]
    public ImperialFoodTypes FoodTypes;

    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    [DataField]
    public float Amount;

    /// <summary>Особые пометки: "lure" — любая блесна, "fish" — любая рыба.</summary>
    [DataField]
    public string? Special;
}
