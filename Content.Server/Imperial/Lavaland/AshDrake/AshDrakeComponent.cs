using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.AshDrake;

/// <summary>
/// Пепельный дракон (megafauna/dragon из SS13): конус огня, метеоры, пике с лавой,
/// при здоровье ниже половины — огненная арена и круговое пламя.
/// </summary>
[RegisterComponent]
public sealed partial class AshDrakeComponent : Component
{
    // fire_breath
    [DataField] public float FireRange = 15f;
    [DataField] public float FireDelay = 0.15f;
    [DataField] public float FireDamage = 20f;
    [DataField] public float FireConeCooldown = 3f;
    [DataField] public float[] FireConeAngles = { -40f, 0f, 40f };

    // mass_fire
    [DataField] public float MassFireCooldown = 10.5f;
    [DataField] public int MassFireSectors = 12;
    [DataField] public float MassFireBreathDelay = 2.5f;
    [DataField] public int MassFireSpins = 3;

    // meteors
    [DataField] public float MeteorsCooldown = 3f;
    [DataField] public int MeteorsRange = 9;
    [DataField] public float MeteorsChance = 11f;
    [DataField] public float MeteorFallTime = 0.9f;
    [DataField] public float MeteorDamage = 10f;

    // lava_swoop
    [DataField] public float SwoopCooldown = 4f;
    [DataField] public float SwoopDamage = 75f;
    [DataField] public float SwoopStepDelay = 0.05f;
    [DataField] public float SwoopDescentTime = 1f;
    [DataField] public int LavaPoolsAmount = 30;
    [DataField] public float LavaPoolsDelay = 0.08f;
    [DataField] public float LavaWarningTime = 1.3f;
    [DataField] public float LavaWarningDamage = 10f;

    // arena_escape_enrage
    [DataField] public float ArenaEscapeHeal = 250f;

    [DataField] public SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_fireball.ogg");
    [DataField] public SoundSpecifier WarnSound = new SoundPathSpecifier("/Audio/Imperial/Lavaland/sound_effects_magic_fleshtostone.ogg");
    [DataField] public SoundSpecifier ImpactSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_effects_meteorimpact.ogg");
    [DataField] public SoundSpecifier ExplosionSound = new SoundCollectionSpecifier("Explosion");

    [DataField] public EntProtoId FireEffect = "ImperialMegafaunaHotspot";
    [DataField] public EntProtoId LavaWarning = "ImperialAshDrakeLavaWarning";
    [DataField] public EntProtoId LavaSafe = "ImperialAshDrakeLavaSafe";
    [DataField] public EntProtoId Lava = "ImperialAshDrakeSwoopLava";
    [DataField] public EntProtoId ArenaWall = "ImperialAshDrakeArenaWall";
    [DataField] public EntProtoId MeteorTarget = "ImperialAshDrakeMeteorWarning";
    [DataField] public EntProtoId MeteorFireball = "ImperialAshDrakeMeteorFireball";
    [DataField] public EntProtoId Landing = "ImperialAshDrakeLandingWarning";
    [DataField] public EntProtoId Flight = "ImperialAshDrakeFlight";

    /// <summary>Идёт пике: нельзя бить, ходить и применять другие атаки.</summary>
    [ViewVariables] public bool Swooping;
}
