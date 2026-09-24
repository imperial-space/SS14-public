using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticWeeepingHallucinationStatusEffectComponent : Component
{
    [DataField]
    public TimeSpan NextHallucinationTime = TimeSpan.Zero;

    [DataField]
    public float MinInterval = 120f;

    [DataField]
    public float MaxInterval = 480f;
}
