using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.BloodBrother;

/// <summary>
/// datum/team/brother_team: команда братьев по крови. Висит на сущности игрового правила.
/// </summary>
[RegisterComponent]
public sealed partial class BloodBrotherTeamComponent : Component
{
    /// <summary>brothers_left: сколько ещё человек команда может пробудить.</summary>
    [DataField]
    public int BrothersLeft = 2;

    /// <summary>С этим шансом команде достаётся лишний брат (prob(10) в New()).</summary>
    [DataField]
    public float ExtraBrotherChance = 0.1f;

    /// <summary>Разумы братьев в порядке вступления.</summary>
    [ViewVariables]
    public List<EntityUid> Members = new();

    /// <summary>Общие цели команды: один и тот же объект висит у всех братьев.</summary>
    [ViewVariables]
    public List<EntityUid> Objectives = new();

    [ViewVariables]
    public bool ObjectivesForged;

    /// <summary>update_name(): «Братья по крови Иванов и Петров».</summary>
    [ViewVariables]
    public string TeamName = string.Empty;

    // ─── Прототипы целей (forge_single_objective) ───────────────────────

    /// <summary>Цель «пробудить брата».</summary>
    [DataField]
    public EntProtoId ConvertObjective = "BloodBrotherConvertObjective";

    /// <summary>assassinate: цель должна погибнуть.</summary>
    [DataField]
    public EntProtoId AssassinateObjective = "BloodBrotherAssassinateObjective";

    /// <summary>maroon: цель не должна улететь живой.</summary>
    [DataField]
    public EntProtoId MaroonObjective = "BloodBrotherMaroonObjective";

    /// <summary>destroy: уничтожить ИИ станции.</summary>
    [DataField]
    public EntProtoId DestroyAiObjective = "KillStationAiObjective";

    /// <summary>escape: улететь со станции живым и на свободе.</summary>
    [DataField]
    public EntProtoId EscapeObjective = "EscapeShuttleObjective";

    /// <summary>hijack: улететь на шаттле без лояльного экипажа.</summary>
    [DataField]
    public EntProtoId HijackObjective = "BloodBrotherHijackObjective";

    /// <summary>Откуда берутся цели на кражу.</summary>
    [DataField]
    public ProtoId<Content.Shared.Random.WeightedRandomPrototype> StealObjectiveGroup = "TraitorObjectiveGroupSteal";

    /// <summary>CONFIG brother_objectives_amount.</summary>
    [DataField]
    public int BaseObjectiveAmount = 2;

    /// <summary>Шанс, что вместо побега команде выпадет захват шаттла.</summary>
    [DataField]
    public float HijackChance = 0.1f;

    /// <summary>Вспышка, которую получает брат, пока может вербовать.</summary>
    [DataField]
    public EntProtoId FlashProto = "Flash";
}

/// <summary>datum/antagonist/brother: отметка на разуме брата со ссылкой на его команду.</summary>
[RegisterComponent]
public sealed partial class BloodBrotherComponent : Component
{
    /// <summary>Сущность игрового правила, на которой живёт команда.</summary>
    [ViewVariables]
    public EntityUid Team;
}

/// <summary>
/// grant_conversion_skills(): пока команда может пробудить ещё брата, носитель
/// вербует вспышкой. Висит на теле брата.
/// </summary>
[RegisterComponent]
public sealed partial class BloodBrotherConverterComponent : Component
{
    [ViewVariables]
    public EntityUid Team;
}

/// <summary>objective/convert_brother: засчитывается, когда в команде больше одного брата.</summary>
[RegisterComponent]
public sealed partial class BloodBrotherConvertConditionComponent : Component;

/// <summary>Роль брата по крови на сущности роли разума.</summary>
[RegisterComponent]
public sealed partial class BloodBrotherRoleComponent : Component;
