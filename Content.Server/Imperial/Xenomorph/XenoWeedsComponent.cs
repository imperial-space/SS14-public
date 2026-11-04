using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>Сорняк улья (alien/weeds).</summary>
[RegisterComponent]
public sealed partial class XenoWeedsComponent : Component
{
    [ViewVariables] public EntityUid? Node;
    [ViewVariables] public TimeSpan? DieAt;
}
