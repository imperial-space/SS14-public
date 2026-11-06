using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Гипнотическая вспышка.</summary>
[RegisterComponent]
public sealed partial class UplinkHypnoticFlashComponent : Component
{
    [DataField]
    public TimeSpan HypnoticFlashedDuration = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan HypnoticFlashedDuration2 = TimeSpan.FromSeconds(4);
}
