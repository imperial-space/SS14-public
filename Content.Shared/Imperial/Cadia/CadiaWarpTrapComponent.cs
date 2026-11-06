using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Варп-ловушка Хаоса: срабатывает под врагом.</summary>
[RegisterComponent]
public sealed partial class CadiaWarpTrapComponent : Component
{
    [DataField]
    public TimeSpan WarpTrapDuration = TimeSpan.FromSeconds(2.5);
}
