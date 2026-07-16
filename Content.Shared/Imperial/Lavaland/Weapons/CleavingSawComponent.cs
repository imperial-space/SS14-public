using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.Weapons;

[RegisterComponent, NetworkedComponent]
public sealed partial class CleavingSawComponent : Component
{
    [DataField]
    public float ActivatedAttackRate = 1.5f;

    [DataField]
    public float DeactivatedAttackRate = 0.8f;
}
