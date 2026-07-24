using Content.Shared.Damage;
using Robust.Shared.Map;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

[RegisterComponent]
public sealed partial class DeathNoteBluespaceVictimComponent : Component
{
    [ViewVariables]
    public EntityCoordinates ReturnCoordinates;

    [ViewVariables]
    public MapCoordinates ReturnMapCoordinates;

    [ViewVariables]
    public EntityUid Anomaly;

    [ViewVariables]
    public TimeSpan TeleportAt;

    [ViewVariables]
    public TimeSpan CollapseAt;

    [ViewVariables]
    public TimeSpan ReturnAt;

    [ViewVariables]
    public EntityUid? PocketMap;

    [ViewVariables]
    public bool Teleported;

    [ViewVariables]
    public bool Collapsed;

    [ViewVariables]
    public bool OwnsPortalTimeout;

    [ViewVariables]
    public DamageSpecifier Damage = new();
}
