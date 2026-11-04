using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>heretic_hunt: тело, которое можно принести в жертву на руне (очки по рангу).</summary>
[RegisterComponent]
public sealed partial class BitrunningSacrificeComponent : Component
{
    [DataField]
    public float Points = 1;
}
