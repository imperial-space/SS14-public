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

    /// <summary>Speed in tiles/s. Keep low for "very slow" feel.</summary>
    [DataField]
    public float SpikeSpeed = 4f;

    // ── Attack 1: Cone (6 spikes, narrow cone toward player) ─────────────────

    [DataField]
    public float ConeCooldown = 8f;

    [DataField]
    public int ConeCount = 6;

    [DataField]
    public float ConeSpreadDeg = 20f;

    [ViewVariables]
    public TimeSpan NextConeTime;

    // ── Attack 2: Cross/Diagonal (4 volleys, 1.5 s apart) ────────────────────

    [DataField]
    public float CrossCooldown = 12f;

    [DataField]
    public float CrossRepeatDelay = 1.5f;

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

    /// <summary>true = cardinal axes, false = diagonals; alternates each volley.</summary>
    [ViewVariables]
    public bool CrossNextCardinal = true;

    // ── Attack 3: Random scatter (12x12, 5% per tile) ────────────────────────

    [DataField]
    public float RandomCooldown = 14f;

    [DataField]
    public int RandomAreaHalfSize = 6;

    [DataField]
    public float RandomChance = 0.05f;

    [ViewVariables]
    public TimeSpan NextRandomTime;

    // ── Attack 4: Spiral ──────────────────────────────────────────────────────

    [DataField]
    public float SpiralCooldown = 28f;

    /// <summary>Total spikes per spiral arm (double spiral fires 2x this at < 50% HP).</summary>
    [DataField]
    public int SpiralSpikeCount = 80;

    /// <summary>Seconds between consecutive spike launches.</summary>
    [DataField]
    public float SpiralSpikeInterval = 0.08f;

    /// <summary>Angle increment per spike: 720 / 80 = 9 degrees gives 2 full rotations.</summary>
    [DataField]
    public float SpiralAngleStepDeg = 9f;

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
