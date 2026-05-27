using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Lavaland.AshDrake;

[RegisterComponent]
public sealed partial class AshDrakeComponent : Component
{
    // ── Existing melee-counter shot system ───────────────────────────────────

    [DataField]
    public int FirstShotMeleeHits = 3;

    [DataField]
    public int MinMeleeHitsBeforeShot = 4;

    [DataField]
    public int MaxMeleeHitsBeforeShot = 10;

    [ViewVariables]
    public int MeleeHitsSinceLastShot;

    [ViewVariables]
    public int NextShotAtMeleeHits;

    public void RollNextThreshold(IRobustRandom random)
    {
        NextShotAtMeleeHits = random.Next(MinMeleeHitsBeforeShot, MaxMeleeHitsBeforeShot + 1);
    }

    // ── Tile-based attack shared ──────────────────────────────────────────────

    [DataField]
    public string FireEffectPrototype = "ImperialAshDrakeFireTile";

    [DataField]
    public string SnakeFirePrototype = "ImperialAshDrakeSnakeFireTile";

    [DataField]
    public float TileDamageDelay = 0.6f;

    [DataField]
    public float TargetSearchRange = 18f;

    // Sounds
    [DataField]
    public SoundSpecifier MeleeAttackSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_misc_demon_attack1.ogg");

    [DataField]
    public SoundSpecifier FireConeSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_fireball.ogg");

    [DataField]
    public SoundSpecifier MeteorRainSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_effects_meteorimpact.ogg");

    [DataField]
    public SoundSpecifier DeathSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_misc_demon_dies.ogg");

    // ── Fire cone (tile-based, separate cooldown) ─────────────────────────────

    [DataField]
    public float FireConeCooldown = 2f;

    [DataField]
    public float FireConeRange = 15f;

    [DataField]
    public float FireConeDamage = 5f;

    [DataField]
    public int FireConeRayCount = 3;

    [DataField]
    public float FireConeSpreadDeg = 40f;

    [DataField]
    public float FireConeStepDelay = 0.08f;

    [ViewVariables]
    public TimeSpan NextFireConeTime;

    // ── Meteor rain (random tiles around target) ──────────────────────────────

    [DataField]
    public float MeteorCooldown = 6f;

    [DataField]
    public float MeteorRadius = 6.5f;

    [DataField]
    public int MeteorCount = 95;

    [DataField]
    public float MeteorDamage = 40f;

    [DataField]
    public float MeteorExtraDelay = 0.6f;

    [DataField]
    public float MeteorSpawnChance = 0.11f;

    [DataField]
    public float MeteorCameraShakeRadius = 7f;

    [DataField]
    public float MeteorTravelSpeed = 8f;

    [DataField]
    public EntProtoId MeteorFireballPrototype = "ImperialAshDrakeMeteorFireball";

    [DataField]
    public EntProtoId MeteorWarningPrototype = "ImperialAshDrakeMeteorWarning";

    [ViewVariables]
    public TimeSpan NextMeteorTime;

    [ViewVariables]
    public List<InFlightMeteor> ActiveMeteors = new();

    // ── Swoop attack (unlocked below 50% HP) ─────────────────────────────────

    [DataField]
    public float SwoopCooldown = 8f;

    [DataField]
    public float SwoopAoeRadius = 2f;

    [DataField]
    public float SwoopDamage = 75f;

    [DataField]
    public float SwoopWindup = 1.0f;

    [DataField]
    public float SwoopFlightDuration = 0.2f;

    [DataField]
    public float SwoopLandingDelay = 0.3f;

    [DataField]
    public float SwoopTrailInterval = 0.08f;

    [DataField]
    public float SwoopKnockback = 7f;

    [DataField]
    public float SwoopCameraShakeIntensity = 0.8f;

    [DataField]
    public EntProtoId SwoopShadowPrototype = "ImperialAshDrakeShadow";

    [DataField]
    public EntProtoId SwoopLandingWarningPrototype = "ImperialAshDrakeLandingWarning";

    // ── Circular Fire Breath (unlocked below 50% HP) ──────────────────────────

    [DataField]
    public float CircularFireBreathCooldown = 4f;

    [DataField]
    public float CircularFireBreathRange = 8f;

    [DataField]
    public float CircularFireBreathDamage = 5f;

    [DataField]
    public int CircularFireBreathRepeats = 3;

    [DataField]
    public float CircularFireBreathRepeatDelay = 0.4f;

    [ViewVariables]
    public TimeSpan NextCircularFireBreathTime;

    // ── Fire Arena (unlocked below 50% HP, replaces swoop) ────────────────────

    [DataField]
    public float FireArenaCooldown = 10f;

    [DataField]
    public int FireArenaRadius = 3;

    [DataField]
    public float FireArenaDamage = 35f;

    [DataField]
    public string FireArenaEffectPrototype = "ImperialAshDrakeArenaFireTile";

    [DataField]
    public EntProtoId FireArenaMarkerPrototype = "ImperialAshDrakeArenaMarker";

    [DataField]
    public EntProtoId FireArenaWallPrototype = "ImperialAshDrakeArenaWall";

    [DataField]
    public float FireArenaMarkerDuration = 1.5f;

    [DataField]
    public float FireArenaFlameDuration = 2f;

    [DataField]
    public int FireArenaRounds = 3;

    [DataField]
    public int FireArenaMarkerStepDistance = 3;

    [DataField]
    public float FireArenaWaveDelay = 0.8f;

    [DataField]
    public int FireArenaWaves = 3;

    [ViewVariables]
    public TimeSpan NextFireArenaTime;

    [ViewVariables]
    public bool IsFireArenaActive;

    [ViewVariables]
    public FireArenaPhase FireArenaPhase;

    [ViewVariables]
    public EntityCoordinates FireArenaCenterCoordinates;

    [ViewVariables]
    public EntityCoordinates FireArenaMarkerTile;

    [ViewVariables]
    public int FireArenaCompletedRounds;

    [ViewVariables]
    public TimeSpan FireArenaMarkerEndTime;

    [ViewVariables]
    public TimeSpan FireArenaFlameEndTime;

    [ViewVariables]
    public EntityUid FireArenaMarkerUid = EntityUid.Invalid;

    [ViewVariables]
    public List<EntityUid> FireArenaWallUids = new();

    [ViewVariables]
    public int LowHpSwoopsSinceLastArena;

    [DataField]
    public float MaxHp = 2500f;

    [ViewVariables]
    public TimeSpan NextSwoopTime;

    [ViewVariables]
    public bool IsSwooping;

    [ViewVariables]
    public EntityUid SwoopTarget = EntityUid.Invalid;

    [ViewVariables]
    public TimeSpan SwoopLandTime;

    [ViewVariables]
    public TimeSpan SwoopStartTime;

    [ViewVariables]
    public TimeSpan SwoopWarningTime;

    [ViewVariables]
    public TimeSpan NextSwoopTrailTime;

    [ViewVariables]
    public bool SwoopWarningSpawned;

    [ViewVariables]
    public EntityCoordinates SwoopStartCoordinates;

    [ViewVariables]
    public EntityCoordinates SwoopDestinationCoordinates;

    [ViewVariables]
    public EntityUid SwoopShadowUid = EntityUid.Invalid;

    [ViewVariables]
    public EntityUid SwoopWarningUid = EntityUid.Invalid;
}

public enum FireArenaPhase
{
    None,
    WaitingForMarker,
    FlamesActive,
}

/// <summary>
/// Tracks a meteor projectile in flight from drake to target tile.
/// </summary>
public struct InFlightMeteor
{
    public EntityUid FireballUid;
    public EntityUid WarningUid;
    public EntityCoordinates StartPos;
    public EntityCoordinates EndPos;
    public TimeSpan StartTime;
    public TimeSpan TravelDuration;
    public float Damage;
}
