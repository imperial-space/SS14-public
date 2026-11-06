using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Prototypes;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Server.Imperial.MartialArts;

/// <summary>
/// mob/living/var/martial_arts: известные искусства, активный стиль, комбо и захват.
/// Добавляется к мобу, как только он чему-то научился.
/// </summary>
[RegisterComponent]
public sealed partial class MartialArtsComponent : Component
{
    /// <summary>Известные искусства по порядку: первое — активное.</summary>
    [ViewVariables]
    public List<KnownMartialArt> Arts = new();

    [ViewVariables]
    public ProtoId<MartialArtPrototype>? Active => Arts.Count > 0 ? Arts[0].Art : (ProtoId<MartialArtPrototype>?)null;

    // ─── Комбо ──────────────────────────────────────────────────────────

    /// <summary>streak.</summary>
    [ViewVariables]
    public string Streak = string.Empty;

    /// <summary>current_target.</summary>
    [ViewVariables]
    public EntityUid? StreakTarget;

    /// <summary>Таймер сброса комбо (combo_timer).</summary>
    [ViewVariables]
    public TimeSpan? StreakResetAt;

    // ─── Захват ─────────────────────────────────────────────────────────

    /// <summary>grab_state над тем, кого мы тащим.</summary>
    [ViewVariables]
    public MartialGrabState GrabState = MartialGrabState.Passive;

    [ViewVariables]
    public EntityUid? GrabTarget;

    [ViewVariables]
    public bool UpgradingGrab;

    /// <summary>Следующий тик удушения при GRAB_KILL.</summary>
    [ViewVariables]
    public TimeSpan NextStrangleTick;

    /// <summary>throw_mode: защитная стойка, в которой блокируются удары.</summary>
    [ViewVariables]
    public bool BlockMode;

    /// <summary>Последний, кто бил нас в ближнем бою — чтобы опознать урон как удар.</summary>
    [ViewVariables]
    public EntityUid? LastMeleeAttacker;

    [ViewVariables]
    public TimeSpan LastMeleeAttackAt;

    /// <summary>Нажатый толчок, который ждёт события атаки.</summary>
    [ViewVariables]
    public EntityUid? PendingDisarmTarget;

    // ─── Способности ────────────────────────────────────────────────────

    [ViewVariables]
    public EntityUid? HelpAction;

    [ViewVariables]
    public EntityUid? SwapAction;

    [ViewVariables]
    public EntityUid? BlockAction;

    /// <summary>Способности активного стиля.</summary>
    [ViewVariables]
    public List<EntityUid> StyleActions = new();

    // ─── Состояние отдельных искусств ───────────────────────────────────

    /// <summary>CQC: restraining_mob и до какого момента он «сдержан».</summary>
    [ViewVariables]
    public EntityUid? Restraining;

    [ViewVariables]
    public TimeSpan RestrainingUntil;

    /// <summary>Plasma Fist: plasma_power.</summary>
    [ViewVariables]
    public int PlasmaPower = 1;

    /// <summary>Spider's Bite: last_hit_ref и last_hit_count.</summary>
    [ViewVariables]
    public EntityUid? SpiderLastHit;

    [ViewVariables]
    public int SpiderLastHitCount;

    /// <summary>datum/skill/athletics: опыт бокса (в TG — на разуме).</summary>
    [ViewVariables]
    public float AthleticsExperience;

    /// <summary>Защита от повторного рывка из захвата (client.move_delay = 4 SECONDS).</summary>
    [ViewVariables]
    public TimeSpan NextResist;

    /// <summary>Кулдаун следующего удара после хука/апперкота (changeNext_move).</summary>
    [ViewVariables]
    public TimeSpan NextAttackAllowed;

    [DataField]
    public DamageSpecifier UpdateGrabDamage = new() { DamageDict = { ["Asphyxiation"] = 3 } };

    [DataField]
    public TimeSpan TussleDiscombobulateTime = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan TussleCravenBlowTime = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan CarpGrabTime = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan CqcRestrainMoveTime = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan CqcHarmTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan CqcDisarmTime = TimeSpan.FromSeconds(40);

    [DataField]
    public TimeSpan CqcDisarmTime2 = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan CqcBlockTime = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan DamageModifyDelay = TimeSpan.FromSeconds(0.1);

    [DataField]
    public TimeSpan KazaHarmDelay = TimeSpan.FromSeconds(0.25);

    [DataField]
    public TimeSpan JudoCheckOneClickTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan TackleHitTime = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan TackleLandTime = TimeSpan.FromSeconds(0.2);

    [DataField]
    public TimeSpan PlasmaApotheosisDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan PsychoAttackTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan PsychoAttackTime2 = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan MushHarmDelay = TimeSpan.FromSeconds(2.5);

    [DataField]
    public TimeSpan MushPunchDoAfterTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan JungleAttackTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan WrestlingThrowTime = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan WrestlingSlamDelay = TimeSpan.FromSeconds(0.3);

    [DataField]
    public TimeSpan WrestlingSlamTime = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan WrestlingStrikeDelay = TimeSpan.FromSeconds(0.4);

    [DataField]
    public TimeSpan WrestlingStrikeTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan WrestlingKickTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan WrestlingDropTime = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan WrestlingDropTime2 = TimeSpan.FromSeconds(4);

    [DataField]
    public float BoxingBlockDamage = 10f;

    [DataField]
    public float BoxingBlockAmount = 5f;

    [DataField]
    public float BoxingBlockChance = 0.5f;

    [DataField]
    public float CarpGrabAmount = 20f;

    [DataField]
    public float CarpHarmDamage = 10f;

    [DataField]
    public float CarpDisarmAmount = 20f;

    [DataField]
    public float CqcRestrainMoveAmount = 20f;

    [DataField]
    public float CqcHarmDamage = 10f;

    [DataField]
    public float CqcHarmAmount = 10f;

    [DataField]
    public float CqcDisarmChance = 0.65f;

    [DataField]
    public float CqcDisarmAmount = 5f;

    [DataField]
    public float SnapNeckAmount = 100f;

    [DataField]
    public float KazaHarmDamage = 10f;

    [DataField]
    public float KazaHarmChance = 0.5f;

    [DataField]
    public float KazaDisarmChance = 0.6f;

    [DataField]
    public float JudoCheckOneClickAmount = 10f;

    [DataField]
    public float JudoHarmAmount = 6f;

    [DataField]
    public float PlasmaCheckStreakRange = 200f;

    [DataField]
    public float PlasmaCheckStreakSpeed = 4f;

    [DataField]
    public float PlasmaApotheosisSlope = 4f;

    [DataField]
    public float PsychoAttackRange = 4f;

    [DataField]
    public float PsychoAttackSpeed = 2f;

    [DataField]
    public float MushPunchDoAfterRange = 4f;

    [DataField]
    public float MushPunchDoAfterSpeed = 2f;

    [DataField]
    public float WrestlingCheckStreakDamage = 10f;

    [DataField]
    public float WrestlingSlamChance = 0.5f;

    [DataField]
    public float WrestlingKickRange = 3f;

    [DataField]
    public float WrestlingKickSpeed = 2f;

    [DataField]
    public float WrestlingDropChance = 0.33f;
}
