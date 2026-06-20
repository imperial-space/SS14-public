using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Lavaland.WheelOfFate;

[RegisterComponent, NetworkedComponent]
public sealed partial class WheelOfFateComponent : Component
{
    [DataField] public float DiceChance = 0.05f;
    [DataField] public float FailureDamage = 20f;
    [DataField] public TimeSpan SpinCooldown = TimeSpan.FromSeconds(30);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextSpinTime;
}
