using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMoonConvertedComponent : Component
{
    public float DamageAccumulated = 0f;
    public const float DamageBreakThreshold = 75f;
}
