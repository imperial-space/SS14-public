using Robust.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.SyndieBattle.Components;

[RegisterComponent]
public sealed partial class SyndieBattleRespawnComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ResPath RespawnMap = new("/Maps/Imperial/SyndieBattle/RespawnMap.yml");

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId ArenaMob = "MobSyndieBattleCatSyndy";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public SoundSpecifier? Music;

    [ViewVariables(VVAccess.ReadOnly)]
    public List<EntityUid> ArenaSpawns = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public int NextArenaSpawnIndex;

    [ViewVariables(VVAccess.ReadOnly)]
    public MapId? ArenaMap;

    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<NetUserId, TimeSpan> LastRespawn = new();
}
