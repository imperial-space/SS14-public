using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Lavaland.Legion;

[RegisterComponent]
public sealed partial class LegionBossComponent : Component
{
    // ── Decision cycle ──────────────────────────────────────────────────────
    [DataField]
    public float DecisionCooldown = 2f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextDecisionTime;

    // ── Ability 1: Skull summon (75%) ────────────────────────────────────────
    [DataField]
    public SoundSpecifier? SummonSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_narsie_attack.ogg");

    [DataField]
    public EntProtoId SummonedPrototype = "MobLegionSummonSkullLavaland";

    [DataField]
    public int MaxConcurrentSkulls = 10;

    [DataField]
    public float SummonChance = 0.75f;

    [DataField]
    public float SummonRadius = 1.5f;

    // ── Ability 2: Charge mode (25%) ─────────────────────────────────────────
    [DataField]
    public float ChargeDuration = 10f;

    [DataField]
    public float ChargeAccelerationDuration = 3f;

    [DataField]
    public float ChargeSpeed = 4.5f;

    [DataField]
    public float NormalSpeed = 1.5f;

    /// <summary>HTN MeleeRange при обычном режиме (держать дистанцию).</summary>
    [DataField]
    public float NormalMeleeRange = 4f;

    public bool IsCharging;
    public TimeSpan ChargeEndTime;
    public TimeSpan ChargeStartTime;

    // ── Split on death ────────────────────────────────────────────────────────
    [DataField]
    public EntProtoId? SplitPrototype;

    [DataField]
    public int SplitCount = 2;
}
