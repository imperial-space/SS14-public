using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Core;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Effects;

/// <summary>
/// sound_damage(): пока метка висит, носитель не слышит чужую речь.
/// Накладывают Врата Разума.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HereticDeafComponent : Component
{
    [ViewVariables]
    public TimeSpan ExpiresAt;
}
