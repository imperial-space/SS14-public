using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Алтарь: понемногу питает силу варпа, пока стоит.</summary>
[RegisterComponent]
public sealed partial class CadiaWarpSourceComponent : Component
{
    [DataField]
    public float PerMinute = 2f;

    [ViewVariables]
    public float Accumulated;
}
