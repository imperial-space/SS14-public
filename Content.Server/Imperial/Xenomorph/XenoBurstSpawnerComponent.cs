using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>Спавнер личинки, вырывающейся из носителя (опрос призраков).</summary>
[RegisterComponent]
public sealed partial class XenoBurstSpawnerComponent : Component
{
    [ViewVariables] public EntityUid? Host;
}
