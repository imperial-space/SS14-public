using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Core;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Moon;

/// <summary>
/// status_effect/moon_slept: цель с защитой разума, которую лунный амулет усыпил вместо обращения.
/// Пока метка висит, повторное проведение амулета только ломает мозг.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMoonSleptComponent : Component
{
    [ViewVariables]
    public TimeSpan ExpiresAt;
}
