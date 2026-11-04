using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Часть стены ДОТа: гранату можно закинуть внутрь через неё.</summary>
[RegisterComponent]
public sealed partial class CadiaPillboxPartComponent : Component
{
    [ViewVariables]
    public EntityUid? Center;
}
