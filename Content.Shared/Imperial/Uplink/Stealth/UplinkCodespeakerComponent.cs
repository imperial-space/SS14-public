using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Знает кодовую речь Синдиката.</summary>
[RegisterComponent]
public sealed partial class UplinkCodespeakerComponent : Component
{
    [DataField]
    public float CodespeakSpeechRange = 10f;
}
