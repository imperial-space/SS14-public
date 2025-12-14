using Content.Shared.Imperial.Minigames;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class TrapPlayersOnMapComponent : Component
{
    [DataField(required: true)]
    public ResPath MapPath;

    [DataField(required: true)]
    public ProtoId<TagPrototype> SpawnTag;

    [DataField(required: true)]
    public ProtoId<MinigamePrototype> Minigame;

    [DataField]
    public EntProtoId? SpawnOnDespawn;

    [DataField]
    public int TrapCount = 4;

    public EntityUid Map;

    public List<EntityUid> Trapped = new();

    public EntityUid? ReleaseUser;
}
