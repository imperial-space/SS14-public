using Content.Shared.Imperial.CrewSkills;
using Content.Shared.Roles;
using Content.Shared.Store;
using Content.Shared.Random;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Content.Server.Imperial.SyndieBattle.Types;
using Content.Shared.Imperial.SyndieBattle.StatusIcons;

namespace Content.Server.Imperial.SyndieBattle;

/// <summary>
/// Компонент для правила игры SyndieBattle
/// </summary>
[RegisterComponent]
public sealed partial class SyndieBattleRuleComponent : Component
{
    /// <summary>
    /// Активно ли правило в данный момент
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Active;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool ActivationPending;

    /// <summary>
    /// Количество машин искупления
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int RedemptionMachineCount = 30;

    [DataField]
    public EntProtoId RedemptionMachine = "SyndieBattleRedemptionMachine";

    /// <summary>
    /// Что выдается в аплинк
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<CurrencyPrototype> Currency = "Telecrystal";

    /// <summary>
    /// Стартовая экипировка
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<StartingGearPrototype> StartingGear = "SyndieBattleGear";

    /// <summary>
    /// Mind role assigned to every combatant.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId MindRole = "MindRoleSyndieBattle";

    /// <summary>
    /// Skill group granted to every combatant.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<SkillGroupPrototype> SkillGroup = "All";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public LocId GoalAnnouncement = "syndiebattle-mode-goal";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public LocId RulesAnnouncement = "syndiebattle-mode-rules";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public LocId GuideAnnouncement = "syndiebattle-mode-guide";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId PrimaryObjective = "SyndieBattleWinObjective";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<WeightedRandomPrototype> AdditionalObjectiveGroup = "TraitorObjectiveGroups";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxObjectiveDifficulty = 7f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int AdditionalObjectiveCount = 5;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan GoalMessageDelay = TimeSpan.FromSeconds(5);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan GoalMessageJitter = TimeSpan.FromSeconds(1);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan SpawnProtectionDuration = TimeSpan.FromSeconds(45);

    [DataField]
    public int SpawnPositionAttempts = 32;

    /// <summary>
    /// Preferred distance from living mobs when choosing a station spawn.
    /// </summary>
    [DataField]
    public float SpawnSeparation = 8f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public LocId SpawnProtectionStartMessage = "syndiebattle-spawn-protection-start";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public LocId SpawnProtectionEndMessage = "syndiebattle-spawn-protection-end";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int ManifestEntryLimit = 10;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int InitialTelecrystals = 24;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int ArenaComebackTelecrystals = 12;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float KillStealFraction = 0.25f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxKillSteal = 8;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int LeaderKillThreshold = 3;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int LeaderKillReward = 4;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int VictoryKillLimit = 20;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<SyndieBattleLeaderIconPrototype> LeaderStatusIcon = "SyndieBattleLeader";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan RepeatKillCooldown = TimeSpan.FromMinutes(3);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<int> KillStreakAnnouncements = new() { 3, 5, 10, 15 };

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan RoundDuration = TimeSpan.FromMinutes(40);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan RoundEndDelay = TimeSpan.FromSeconds(12);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<TimeSpan> RoundTimeAnnouncements = new()
    {
        TimeSpan.FromMinutes(20),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(1),
    };

    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<NetUserId, SyndieBattlePlayerScore> PlayerScores = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public string? FirstDeathName;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? FirstDeathTime;

    [ViewVariables(VVAccess.ReadOnly)]
    public NetUserId? Leader;

    [ViewVariables(VVAccess.ReadOnly)]
    public NetUserId? Winner;

    [ViewVariables(VVAccess.ReadOnly)]
    public string? WinnerName;

    [ViewVariables(VVAccess.ReadOnly)]
    public int WinnerKills;

    [ViewVariables(VVAccess.ReadOnly)]
    public SyndieBattleRoundEndReason? RoundEndReason;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? RoundEndsAt;

    [ViewVariables(VVAccess.ReadOnly)]
    public Queue<TimeSpan> PendingRoundTimeAnnouncements = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<(NetUserId Killer, NetUserId Victim), TimeSpan> RecentKills = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public int NextParticipationOrder;

    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<EntityUid> RedemptionMachines = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public List<(NetUserId UserId, TimeSpan At)> PendingGoalAnnouncements = new();
}
