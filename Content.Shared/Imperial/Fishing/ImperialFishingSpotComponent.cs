using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Место ловли (/datum/component/fishing_spot).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishingSpotComponent : Component
{
    [DataField(required: true)]
    public ProtoId<ImperialFishSourcePrototype> Source;
}
