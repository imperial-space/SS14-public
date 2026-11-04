using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Барьер руны (emergency_shield/cult/barrier).</summary>
[RegisterComponent]
public sealed partial class CultBarrierComponent : Component
{
    [ViewVariables] public EntityUid? Rune;
    [ViewVariables] public bool Active;
}
