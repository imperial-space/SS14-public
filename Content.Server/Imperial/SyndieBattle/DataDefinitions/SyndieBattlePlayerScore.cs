using Content.Shared.Preferences;

namespace Content.Server.Imperial.SyndieBattle;

[DataDefinition]
public sealed partial class SyndieBattlePlayerScore
{
    [ViewVariables(VVAccess.ReadOnly)]
    public string Name = string.Empty;

    [ViewVariables(VVAccess.ReadOnly)]
    public int KillCount;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan SpawnTime;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan SurvivalTime;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool Alive;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? CurrentEntity;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? StoreEntity;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? PersistentStoreEntity;

    [ViewVariables(VVAccess.ReadOnly)]
    public HumanoidCharacterProfile? Profile;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Station;

    [ViewVariables(VVAccess.ReadOnly)]
    public int Deaths;

    [ViewVariables(VVAccess.ReadOnly)]
    public int ArenaAttempts;

    [ViewVariables(VVAccess.ReadOnly)]
    public int ArenaCompletions;

    [ViewVariables(VVAccess.ReadOnly)]
    public int RedeemedPdas;

    [ViewVariables(VVAccess.ReadOnly)]
    public int RedeemedTelecrystals;

    [ViewVariables(VVAccess.ReadOnly)]
    public int SpentTelecrystals;

    [ViewVariables(VVAccess.ReadOnly)]
    public int StolenTelecrystals;

    [ViewVariables(VVAccess.ReadOnly)]
    public int Generation;

    [ViewVariables(VVAccess.ReadOnly)]
    public int CurrentKillStreak;

    [ViewVariables(VVAccess.ReadOnly)]
    public int BestKillStreak;

    [ViewVariables(VVAccess.ReadOnly)]
    public int ComebackTelecrystals;

    [ViewVariables(VVAccess.ReadOnly)]
    public int ParticipationOrder;
}
