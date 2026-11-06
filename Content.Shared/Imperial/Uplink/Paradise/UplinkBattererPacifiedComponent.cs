using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Paradise;

/// <summary>Временное умиротворение от Mind Batterer (status_effect/pacifism/batterer).</summary>
[RegisterComponent]
public sealed partial class UplinkBattererPacifiedComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;
}
