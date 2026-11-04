using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Леска (/obj/item/fishing_line).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishingLineComponent : Component
{
    [DataField]
    public ImperialFishingLineTraits Traits;

    [DataField]
    public Color LineColor = Color.Gray;

    [DataField]
    public int CastRange = 2;

    /// <summary>Позволяет ловить в лаве и плазме (TRAIT_ROD_LAVA_USABLE).</summary>
    [DataField]
    public bool LavaUsable;

    [DataField]
    public LocId? WikiDescription;
}
