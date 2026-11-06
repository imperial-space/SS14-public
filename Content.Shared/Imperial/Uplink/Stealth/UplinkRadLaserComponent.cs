using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Радиоактивный микролазер.</summary>
[RegisterComponent]
public sealed partial class UplinkRadLaserComponent : Component
{
    [DataField]
    public int Intensity = 10;

    [DataField]
    public int Wavelength = 10;

    [ViewVariables]
    public TimeSpan ReadyAt;
}
