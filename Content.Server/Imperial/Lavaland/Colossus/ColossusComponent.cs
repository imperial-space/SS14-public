using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.Colossus;

[RegisterComponent]
public sealed partial class ColossusComponent : Component
{
    // ── General ──────────────────────────────────────────────────────────────

    [DataField]
    public float MaxHp = 2500f;

    [DataField]
    public float TargetSearchRange = 20f;

    [DataField]
    public SoundSpecifier EnrageSound =
        new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_narsie_attack.ogg");

    [DataField]
    public SoundSpecifier AttackSound =
        new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_ratvar_attack.ogg");

    // ── Enrage ───────────────────────────────────────────────────────────────

    [DataField]
    public float NormalMeleeDamage = 30f;

    [DataField]
    public float EnragedMeleeDamage = 50f;

    [DataField]
    public float NormalSpeed = 1.5f;

    [DataField]
    public float EnragedSpeed = 2.5f;

    [DataField]
    public float EnrageThreshold = 1250f;

    [ViewVariables]
    public bool Enraged;

    // ── Spike projectile ─────────────────────────────────────────────────────

    [DataField]
    public string SpikePrototype = "BulletColossusHoly";

    [DataField]
    public float SpikeSpeed = 4f;

    // ── Telegraph (pre-fire delay, like SS13's SLEEP_CHECK_DEATH 1.5s) ───────

    [DataField]
    public float TelegraphDelay = 1.5f;

    [ViewVariables]
    public bool IsTelegraphing;

    [ViewVariables]
    public TimeSpan TelegraphUntil;

    [ViewVariables]
    public ColossusPreFireAttack PendingAttack;

    /// <summary>Saved cone target for use after telegraph delay.</summary>
    [ViewVariables]
    public EntityUid TelegraphConeTarget;

    // ── Attack 1: Shotgun blast (6 spikes, tight cone toward player) ──────────

    [DataField]
    public float ConeCooldown = 8f;

    [DataField]
    public int ConeCount = 6;

    /// <summary>Total spread in degrees (±12.5° = 25° total, matching SS13).</summary>
    [DataField]
    public float ConeSpreadDeg = 25f;

    [ViewVariables]
    public TimeSpan NextConeTime;

    // ── Attack 2: Directional alternating (diag→card→diag→card, 1s apart) ────

    [DataField]
    public float CrossCooldown = 12f;

    [DataField]
    public float CrossRepeatDelay = 1.0f;

    [DataField]
    public int CrossRepeatTotal = 4;

    [ViewVariables]
    public TimeSpan NextCrossTime;

    [ViewVariables]
    public bool CrossIsFiring;

    [ViewVariables]
    public int CrossRepeatsDone;

    [ViewVariables]
    public TimeSpan NextCrossRepeatTime;

    /// <summary>false = diagonals first (SS13 order: diag→card→diag→card).</summary>
    [ViewVariables]
    public bool CrossNextCardinal = false;

    // ── Attack 3: Random AoE (32 shots, random 360°) ─────────────────────────

    [DataField]
    public float RandomCooldown = 14f;

    [DataField]
    public int RandomShotCount = 32;

    [ViewVariables]
    public TimeSpan NextRandomTime;

    // ── Attack 4: Spiral ──────────────────────────────────────────────────────

    [DataField]
    public float SpiralCooldown = 28f;

    [DataField]
    public int SpiralSpikeCount = 80;

    [DataField]
    public float SpiralSpikeInterval = 0.1f;

    /// <summary>22.5 degrees per shot, matching SS13.</summary>
    [DataField]
    public float SpiralAngleStepDeg = 22.5f;

    [ViewVariables]
    public TimeSpan NextSpiralTime;

    [ViewVariables]
    public bool IsSpiralActive;

    [ViewVariables]
    public int SpiralSpikeFired;

    [ViewVariables]
    public float SpiralCurrentAngleDeg;

    [ViewVariables]
    public TimeSpan NextSpiralSpikeTime;

    [ViewVariables]
    public bool LootDropped;
}

public enum ColossusPreFireAttack : byte
{
    None,
    Cone,
    Cross,
    Random,
    Spiral,
}
