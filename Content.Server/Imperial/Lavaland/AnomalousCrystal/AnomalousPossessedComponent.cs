using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.AnomalousCrystal;

/// <summary>Животное, в которое вселился игрок через кристалл (obj/structure/closet/stasis).</summary>
[RegisterComponent]
public sealed partial class AnomalousPossessedComponent : Component
{
    [ViewVariables] public EntityUid Body;
    [ViewVariables] public EntityUid? Action;
}
