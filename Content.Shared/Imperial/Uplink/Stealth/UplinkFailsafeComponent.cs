using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Аварийный код самоуничтожения аплинка (failsafe).</summary>
[RegisterComponent]
public sealed partial class UplinkFailsafeComponent : Component
{
    [ViewVariables]
    public Content.Shared.PDA.Note[]? Code;

    [DataField]
    public float SetRingtoneTotalIntensity = 30f;

    [DataField]
    public float SetRingtoneSlope = 6f;

    [DataField]
    public float SetRingtoneMaxTileIntensity = 6f;
}
