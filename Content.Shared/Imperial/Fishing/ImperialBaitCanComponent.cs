using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Банка наживки (/obj/item/bait_can).</summary>
[RegisterComponent]
public sealed partial class ImperialBaitCanComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Bait;

    [DataField]
    public int Uses = 20;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan NextUse;
}
