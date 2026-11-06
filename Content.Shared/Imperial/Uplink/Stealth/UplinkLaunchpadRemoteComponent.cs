using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Пульт телепада, замаскированный под папку.</summary>
[RegisterComponent]
public sealed partial class UplinkLaunchpadRemoteComponent : Component
{
    [ViewVariables]
    public EntityUid? Pad;

    [ViewVariables]
    public EntityUid? Briefcase;

    [DataField]
    public int X;

    [DataField]
    public int Y = 1;

    [ViewVariables]
    public TimeSpan ReadyAt;

    [DataField]
    public TimeSpan ReadyDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public float FireLaunchpadRange = 0.45f;
}
