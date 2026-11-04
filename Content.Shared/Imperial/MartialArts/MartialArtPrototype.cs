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
