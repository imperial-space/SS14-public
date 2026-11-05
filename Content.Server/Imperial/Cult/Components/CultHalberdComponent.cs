using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Кровавая алебарда.</summary>
[RegisterComponent]
public sealed partial class CultHalberdComponent : Component
{
    [ViewVariables] public EntityUid? OwnerEntity;
    [ViewVariables] public EntityUid? BondAction;
}
