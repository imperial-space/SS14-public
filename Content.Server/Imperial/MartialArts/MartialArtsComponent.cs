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
