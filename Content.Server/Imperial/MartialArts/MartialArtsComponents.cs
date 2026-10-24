using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Prototypes;

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
    public ProtoId<MartialArtPrototype>? Active => Arts.Count > 0 ? Arts[0].Art : (ProtoId<MartialArtPrototype>?) null;

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
}

[DataDefinition]
public sealed partial class KnownMartialArt
{
    [DataField]
    public ProtoId<MartialArtPrototype> Art;

    /// <summary>origin: если источник удалён или снят — искусство забывается.</summary>
    [DataField]
    public EntityUid? Source;
}

/// <summary>component/martial_art_giver: одежда, которая учит искусству, пока надета.</summary>
[RegisterComponent]
public sealed partial class MartialArtGiverComponent : Component
{
    [DataField(required: true)]
    public ProtoId<MartialArtPrototype> Art;

    [ViewVariables]
    public EntityUid? Wearer;
}

/// <summary>implant/kaza_ruk, implant/cqc: обучает при вживлении, кнопкой — вкл/выкл.</summary>
[RegisterComponent]
public sealed partial class MartialArtImplantComponent : Component
{
    [DataField(required: true)]
    public ProtoId<MartialArtPrototype> Art;
}

/// <summary>book/granter/martial: учебник или свиток.</summary>
[RegisterComponent]
public sealed partial class MartialArtManualComponent : Component
{
    [DataField(required: true)]
    public ProtoId<MartialArtPrototype> Art;

    /// <summary>martial_name.</summary>
    [DataField(required: true)]
    public LocId MartialName;

    /// <summary>greet.</summary>
    [DataField(required: true)]
    public LocId Greet;

    /// <summary>remarks: что бормочет читатель над страницами.</summary>
    [DataField]
    public List<LocId> Remarks = new();

    /// <summary>uses.</summary>
    [DataField]
    public int Uses = 1;

    /// <summary>pages_to_mastery.</summary>
    [DataField]
    public int Pages = 3;

    /// <summary>reading_time на страницу.</summary>
    [DataField]
    public TimeSpan PageTime = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public int PagesRead;

    /// <summary>recoil(): что будет, если читать исписанный учебник.</summary>
    [DataField]
    public MartialManualRecoil Recoil = MartialManualRecoil.None;

    /// <summary>Во что превращается свиток после прочтения (blankscroll).</summary>
    [DataField]
    public EntProtoId? SpentProto;
}

public enum MartialManualRecoil : byte
{
    None,

    /// <summary>granter/martial/cqc/recoil(): учебник взрывается в руках.</summary>
    Explode,
}

/// <summary>obj/item/mushpunch: странный гриб, съешь — научишься.</summary>
[RegisterComponent]
public sealed partial class MartialArtFoodComponent : Component
{
    [DataField(required: true)]
    public ProtoId<MartialArtPrototype> Art;

    [DataField]
    public LocId Message = "martial-mushroom-devour";
}

/// <summary>status_effect/judo_armbar: «рычаг» дзюдо.</summary>
[RegisterComponent]
public sealed partial class JudoArmbarComponent : Component
{
    [ViewVariables]
    public TimeSpan ExpiresAt;
}

/// <summary>losebreath: сбитое дыхание после удара под дых.</summary>
[RegisterComponent]
public sealed partial class MartialLoseBreathComponent : Component
{
    /// <summary>Сколько жизненных тиков ещё не дышать.</summary>
    [ViewVariables]
    public int Ticks;

    [ViewVariables]
    public TimeSpan NextTick;
}

/// <summary>Метка того, что моба держит в захвате боец — чтобы рвать захват через сопротивление.</summary>
[RegisterComponent]
public sealed partial class MartialGrabbedComponent : Component
{
    [ViewVariables]
    public EntityUid Grabber;
}

/// <summary>
/// CARP_STYLE_FACTOR: одежда, которая добавляет «карповости» стилю Спящего карпа.
/// </summary>
[RegisterComponent]
public sealed partial class CarpStyleFactorComponent : Component;

/// <summary>obj/item/staff/bostaff: правый клик двумя руками бьёт по выносливости.</summary>
[RegisterComponent]
public sealed partial class BoStaffComponent : Component;
