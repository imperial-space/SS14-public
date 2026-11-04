using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Blood bolt barrage (arcane_barrage/blood).</summary>
[RegisterComponent]
public sealed partial class CultBloodBoltComponent : Component
{
    [ViewVariables] public HashSet<EntityUid> Healed = new();
}
