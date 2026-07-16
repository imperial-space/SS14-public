using Content.Shared.Polymorph;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.BloodVial;

/// <summary>
/// A dragon blood vial that polymorphs the user on use (50/50 chance between two forms).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodVialComponent : Component
{
    /// <summary>First polymorph option (50% chance).</summary>
    [DataField]
    public ProtoId<PolymorphPrototype> PolymorphA = "DragonBloodSkeleton";

    /// <summary>Second polymorph option (50% chance).</summary>
    [DataField]
    public ProtoId<PolymorphPrototype> PolymorphB = "DragonBloodLesserDragon";

    /// <summary>Whether this vial has been consumed.</summary>
    [ViewVariables]
    public bool Used = false;
}
