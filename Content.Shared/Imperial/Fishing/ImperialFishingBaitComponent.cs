using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Свойства наживки. Любая еда, рыба, наличные, монеты и батареи тоже годятся в наживку,
/// этот компонент задаёт качество и особые свойства.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishingBaitComponent : Component
{
    [DataField]
    public ImperialBaitQuality Quality = ImperialBaitQuality.None;

    /// <summary>TRAIT_BAIT_ALLOW_FISHING_DUD.</summary>
    [DataField]
    public bool AllowDud;

    /// <summary>TRAIT_OMNI_BAIT: нравится всем рыбам.</summary>
    [DataField]
    public bool Omni;

    /// <summary>TRAIT_BAIT_UNCONSUMABLE.</summary>
    [DataField]
    public bool Unconsumable;

    /// <summary>TRAIT_POISONOUS_BAIT.</summary>
    [DataField]
    public bool Poisonous;

    /// <summary>spin_frequency блесны в секундах (от, до).</summary>
    [DataField]
    public Vector2? SpinFrequency;

    [DataField]
    public ImperialLureKind Lure;

    /// <summary>Типы пищи TG для этого предмета (если не выводятся из компонентов).</summary>
    [DataField]
    public ImperialFoodTypes FoodTypes;

    /// <summary>Состояние наложения на удочку (rod_overlay_icon_state).</summary>
    [DataField]
    public string? RodOverlay;
}
