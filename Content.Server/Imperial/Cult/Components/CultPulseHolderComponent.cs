using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Смещение импульса мастера: схваченная цель.</summary>
[RegisterComponent]
public sealed partial class CultPulseHolderComponent : Component
{
    [ViewVariables] public EntityUid? Seized;
}
