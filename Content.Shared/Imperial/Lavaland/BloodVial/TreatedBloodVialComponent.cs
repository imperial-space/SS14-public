using Content.Shared.Polymorph;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.BloodVial;

/// <summary>
/// Treated dragon blood vial - permanently polymorphs user into a dragonid.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TreatedBloodVialComponent : Component
{
    [DataField]
    public ProtoId<PolymorphPrototype> PolymorphTarget = "DragonBloodDragonid";

    [ViewVariables]
    public bool Used = false;
}