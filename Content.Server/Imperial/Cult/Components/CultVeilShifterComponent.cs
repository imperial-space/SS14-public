using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Veil shifter.</summary>
[RegisterComponent]
public sealed partial class CultVeilShifterComponent : Component
{
    [DataField] public int Uses = 4;
}
