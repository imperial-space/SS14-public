using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Детектор ИИ (multitool/ai_detect).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UplinkAiDetectorComponent : Component
{
    [DataField]
    public float RangeAlert = 8f;

    [DataField]
    public float RangeWarning = 20f;

    [DataField, AutoNetworkedField]
    public UplinkAiProximity State = UplinkAiProximity.None;

    [ViewVariables]
    public TimeSpan NextCheck;
}
