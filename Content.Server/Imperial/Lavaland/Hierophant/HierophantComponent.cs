using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Lavaland.Hierophant;

[RegisterComponent]
public sealed partial class HierophantComponent : Component
{
    [DataField]
    public float TargetSearchRange = 16f;

    // Cross Attack
    [DataField]
    public float CrossBlastRadius = 1.0f;

    [DataField]
    public float CrossBlastDamage = 20f;

    [DataField]
    public float CrossOffset = 1.5f;

    [DataField]
    public int LineAttackLength = 6;

    // Snake Attack
    [DataField]
    public float SnakeTileSpeed = 3f;

    [DataField]
    public EntProtoId SnakeTilePrototype = "ImperialHierophantSnakeTile";

    // Area Burst Attack
    [DataField]
    public float BurstRadius = 0.9f;

    [DataField]
    public float BurstDamage = 18f;

    [DataField]
    public float BurstGridSize = 4f;

    [DataField]
    public int SquareRingCount = 3;

    [DataField]
    public float SquareRingStepDelay = 0.25f;

    [DataField]
    public float SquareRingBaseDamage = 10f;

    [DataField]
    public float SquareRingDamageStep = 8f;

    [DataField]
    public float MaxHp = 5000f;

    [DataField]
    public float TileDamageDelay = 0.5f;

    [DataField]
    public int SnakeMinTiles = 5;

    [DataField]
    public int SnakeMaxTiles = 6;

    [DataField]
    public float SnakeStepDelay = 0.2f;

    [DataField]
    public float SnakeTileDamage = 12f;

    // Effects
    [DataField]
    public EntProtoId BlastEffectPrototype = "ImperialHierophantBlast";

    [DataField]
    public EntProtoId SquareEffectPrototype = "ImperialHierophantSquare";

    // Sounds
    [DataField]
    public SoundSpecifier AttackSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_lavaland_hiero_boss.ogg");

    [DataField]
    public SoundSpecifier LineAttackSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_weapons_sonic_jackhammer.ogg");

    [DataField]
    public SoundSpecifier SnakeAttackSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_weapons_sear.ogg");

    [DataField]
    public SoundSpecifier RingAttackSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_repulse.ogg");

    [DataField]
    public SoundSpecifier ChaserAttackSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_blind.ogg");

    [DataField]
    public SoundSpecifier ArenaAttackSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_machines_airlock_open.ogg");

    [DataField]
    public SoundSpecifier LeapAttackSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_wand_teleport.ogg");

    [DataField]
    public SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_effects_bin_close.ogg");

    // Leap attack
    [DataField]
    public float LeapDistance = 4f;

    [DataField]
    public int LeapTileRadius = 1;

    [DataField]
    public int LeapMaxRepeats = 2;

    [DataField]
    public float LeapRepeatDelay = 0.8f;

    // Teleport to player when too far
    [DataField]
    public float TeleportRange = 8f;

    [DataField]
    public float TeleportCooldown = 5f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextTeleportTime;

    // HP-scaled attack speed
    [DataField]
    public float MinAttackCooldown = 0.8f;

    // Timing
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextAttackTime;

    [DataField]
    public float AttackCooldown = 2.5f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextRingStepTime;

    [DataField]
    public int CurrentRingStep;

    [DataField]
    public bool RingAttackActive;

    [DataField]
    public int AttackIndex;

    [DataField]
    public int LineAttackModeIndex;

    // Leap runtime state
    [DataField]
    public int LeapRemainingRepeats;

    [DataField]
    public EntityUid LeapTarget = EntityUid.Invalid;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextLeapTime;

    [DataField]
    public bool LeapActive;

    // ── Chaser attack ───────────────────────────────────────────────────────
    /// <summary>Seconds between each chaser step.</summary>
    [DataField]
    public float ChaserStepDelay = 0.22f;

    /// <summary>Max lifetime of a chaser in seconds.</summary>
    [DataField]
    public float ChaserDuration = 9f;

    [DataField]
    public float ChaserDamage = 10f;

    [DataField]
    public float ChaserCooldown = 12f;

    [DataField]
    public int MaxChasers = 2;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextChaserTime;

    // ── Arena trap ─────────────────────────────────────────────────────────
    [DataField]
    public int ArenaRadius = 7;

    [DataField]
    public float ArenaCooldown = 20f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextArenaTime;
}
