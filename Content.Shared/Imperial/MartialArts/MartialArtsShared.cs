using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MartialArts;

/// <summary>
/// datum/martial_art (tgstation/BandaStation, code/datums/martial/_martial.dm).
/// В YAML лежат только характеристики, поведение каждого искусства — в MartialArtsSystem по <see cref="Kind"/>.
/// </summary>
[Prototype]
public sealed partial class MartialArtPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>name.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>help_verb: подпись кнопки «Вспомнить основы».</summary>
    [DataField]
    public LocId HelpVerb = "martial-help-verb-basics";

    /// <summary>get_style_help(); пусто — у искусства нет справки.</summary>
    [DataField]
    public LocId? Help;

    [DataField(required: true)]
    public MartialArtKind Kind;

    /// <summary>pacifist_style: пацифист может бить в этом стиле (бокс урона не наносит).</summary>
    [DataField]
    public bool PacifistStyle;

    /// <summary>display_combos: показывать набранное комбо.</summary>
    [DataField]
    public bool DisplayCombos;

    /// <summary>locked_to_use: нельзя переключиться на другой стиль.</summary>
    [DataField]
    public bool LockedToUse;

    /// <summary>max_streak_length.</summary>
    [DataField]
    public int MaxStreakLength = 6;

    /// <summary>combo_timer.</summary>
    [DataField]
    public TimeSpan ComboTimer = TimeSpan.FromSeconds(6);

    /// <summary>grab_state_modifier: захват держится как на ступень сильнее.</summary>
    [DataField]
    public int GrabStateModifier;

    /// <summary>grab_damage_modifier: урон выносливости за неудачный рывок из захвата.</summary>
    [DataField]
    public float GrabDamageModifier;

    /// <summary>grab_escape_chance_modifier.</summary>
    [DataField]
    public float GrabEscapeChanceModifier;

    /// <summary>Способности, которые искусство выдаёт, пока активно.</summary>
    [DataField]
    public List<EntProtoId> Actions = new();

    /// <summary>Сообщения при активации/деактивации стиля.</summary>
    [DataField]
    public LocId? ActivateMessage;

    [DataField]
    public LocId? DeactivateMessage;
}

/// <summary>Какое из искусств TG реализует прототип.</summary>
[Serializable, NetSerializable]
public enum MartialArtKind : byte
{
    Cqc,
    CloseQuartersCooking,
    SleepingCarp,
    Boxing,
    EvilBoxing,
    HunterBoxing,
    Wrestling,
    PlasmaFist,
    PlasmaFistNoBomb,
    PsychoticBrawling,
    MushroomPunch,
    JungleArts,
    SpidersBite,
    KazaRuk,
    CorporateJudo,
}

/// <summary>GRAB_PASSIVE … GRAB_KILL.</summary>
[Serializable, NetSerializable]
public enum MartialGrabState : byte
{
    Passive = 0,
    Aggressive = 1,
    Neck = 2,
    Kill = 3,
}

// ─── Способности ────────────────────────────────────────────────────────

/// <summary>datum/action/swap_arts: ЛКМ — справка, второе нажатие с зажатым Alt недоступно, поэтому смена стиля отдельной кнопкой.</summary>
public sealed partial class MartialArtsHelpActionEvent : InstantActionEvent;

/// <summary>cycle_style(): сменить активное искусство.</summary>
public sealed partial class MartialArtsSwapActionEvent : InstantActionEvent;

/// <summary>throw_mode: в SS14 его нет, поэтому защитная стойка включается отдельно.</summary>
public sealed partial class MartialArtsBlockActionEvent : InstantActionEvent;

/// <summary>Приём, который заряжает следующую атаку: борьба, Каза Рук, дзюдо.</summary>
public sealed partial class MartialArtsMoveActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public string Move = string.Empty;
}

/// <summary>implant/activate(): включить или выключить искусство из импланта.</summary>
public sealed partial class MartialArtsImplantToggleEvent : InstantActionEvent;

/// <summary>component/tackler у Укуса паука: прыжок с захватом.</summary>
public sealed partial class MartialArtsTackleActionEvent : WorldTargetActionEvent;

// ─── Do-after ───────────────────────────────────────────────────────────

/// <summary>grippedby(): усиление захвата до шеи и удушения.</summary>
[Serializable, NetSerializable]
public sealed partial class MartialGrabUpgradeDoAfterEvent : SimpleDoAfterEvent;

/// <summary>mushpunch/charge_up_attack().</summary>
[Serializable, NetSerializable]
public sealed partial class MushroomPunchDoAfterEvent : SimpleDoAfterEvent;

/// <summary>book/granter: страница учебника.</summary>
[Serializable, NetSerializable]
public sealed partial class MartialManualReadDoAfterEvent : SimpleDoAfterEvent;
