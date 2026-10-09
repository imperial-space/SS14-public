using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Legion;

/// <summary>
/// Легион-мегафауна (megafauna/legion из SS13): черепа, раскрутка с броском, стражи;
/// при смерти делится на три меньших. Последний оставляет посох бурь.
/// </summary>
[RegisterComponent]
public sealed partial class MegaLegionComponent : Component
{
    /// <summary>LEGION_LARGE = 3, LEGION_MEDIUM = 2, LEGION_SMALL = 1.</summary>
    [DataField] public int Size = 3;

    [DataField] public float SkullCooldown = 2f;
    [DataField] public float ChaseCooldown = 6f;
    [DataField] public float TurretCooldown = 2f;
    [DataField] public int MinTurrets = 2;
    [DataField] public float TurretInitialDelay = 1.8f;
    [DataField] public float TurretShotDelay = 0.8f;
    [DataField] public float TurretDamage = 19f;
    [DataField] public int TurretBeamRange = 6;

    /// <summary>move_to_delay = 3.</summary>
    [DataField] public float NormalSpeed = 10f / 3f;

    [DataField] public EntProtoId Brood = "ImperialMegaLegionBrood";
    [DataField] public EntProtoId Turret = "ImperialLegionTurret";
    [DataField] public EntProtoId TracerEffect = "ImperialLegionTracer";
    [DataField] public EntProtoId BeamEffect = "ImperialLegionBeam";
    [DataField] public List<EntProtoId> SplitInto = new();
    [DataField] public EntProtoId LastLoot = "ImperialStormStaff";
    [DataField] public EntProtoId ExtraLoot = "NecropolisCrate";
    [DataField] public EntProtoId DefaultLoot = "MaterialBones1";
    [DataField] public int DefaultLootCount = 3;

    [DataField] public SoundSpecifier AttackSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_misc_demon_attack1.ogg");
    [DataField] public SoundSpecifier ChargeSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_weapons_sonic_jackhammer.ogg");
    [DataField] public SoundSpecifier ImpactSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_effects_meteorimpact.ogg");
    [DataField] public SoundSpecifier TurretSummonSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_rattlemebones.ogg");
    [DataField] public SoundSpecifier TracerSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_machines_airlock_open.ogg");
    [DataField] public SoundSpecifier BeamSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_effects_bin_close.ogg");

    [ViewVariables] public bool Charging;
    [ViewVariables] public EntityUid? ThrowTarget;
}

/// <summary>Страж легиона: через 1.8 с стреляет кровавым импульсом и исчезает.</summary>
[RegisterComponent]
public sealed partial class LegionTurretComponent : Component
{
    [ViewVariables] public EntityUid? Owner;
}
