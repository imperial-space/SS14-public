using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// item/kinetic_crusher: метит крупных существ дестабилизатором (ПКМ), удар по метке её подрывает.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KineticCrusherComponent : Component
{
    /// <summary>charge_time без трофеев, секунды.</summary>
    [DataField]
    public float ChargeTime = 1.5f;

    /// <summary>detonation_damage.</summary>
    [DataField]
    public float DetonationDamage = 50f;

    /// <summary>backstab_bonus.</summary>
    [DataField]
    public float BackstabBonus = 30f;

    /// <summary>crusher_mark duration.</summary>
    [DataField]
    public TimeSpan MarkDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public string ContainerId = "crusher_trophies";

    [DataField]
    public SoundSpecifier? BackstabSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/kinetic_accel.ogg");

    [DataField]
    public SoundSpecifier? InsertSound = new SoundPathSpecifier("/Audio/Items/screwdriver.ogg");

    /// <summary>temp_visual/kinetic_blast.</summary>
    [DataField]
    public EntProtoId DetonationEffect = "BulletImpactEffectKinetic";
}

public enum CrusherTrophyKind : byte
{
    /// <summary>watcher_wing / ice_wing: задерживает дальние атаки цели.</summary>
    WatcherWing,
    /// <summary>blaster_tubes/magma_wing: следующий выстрел наносит урон.</summary>
    MagmaWing,
    /// <summary>legion_skull: ускоряет перезарядку.</summary>
    LegionSkull,
    /// <summary>goliath_tentacle: урон за недостающее здоровье.</summary>
    GoliathTentacle,
    /// <summary>miner_eye: blooddrunk на 1 секунду.</summary>
    MinerEye,
    /// <summary>tail_spike: огонь и отталкивание вокруг.</summary>
    TailSpike,
    /// <summary>demon_claws: урон и лечение.</summary>
    DemonClaws,
    /// <summary>blaster_tubes: следующий выстрел наносит урон, но медленнее.</summary>
    BlasterTubes,
    /// <summary>vortex_talisman: самонаводящийся преследователь иерофанта.</summary>
    VortexTalisman,
    /// <summary>wendigo_horn: удвоенный урон ударами.</summary>
    WendigoHorn,
    /// <summary>lobster_claw: rebuke цели.</summary>
    LobsterClaw,
    /// <summary>brimdemon_fang: звук и надпись при подрыве.</summary>
    BrimdemonFang,
    /// <summary>bileworm_spewlet: кислота в четыре стороны, бурение породы вокруг.</summary>
    BilewormSpewlet,
    /// <summary>ice_demon_cube: два демонических клона.</summary>
    IceDemonCube,
    /// <summary>wolf_ear: краткое ускорение.</summary>
    WolfEar,
    /// <summary>bear_paw: двойной удар при здоровье ниже половины.</summary>
    BearPaw,
    /// <summary>raptor_feather: выстрелы проходят сквозь союзников.</summary>
    RaptorFeather,
    /// <summary>ice_block_talisman: заморозка цели.</summary>
    IceBlockTalisman,
    /// <summary>broodmother_tongue: шанс щупалец под целью; сжать — иммунитет к лаве.</summary>
    BroodmotherTongue,
    /// <summary>legionnaire_spine: шанс черепа-союзника; потрясти — призвать череп.</summary>
    LegionnaireSpine,
    /// <summary>flesh_glob: лечение ударами и подрывом.</summary>
    FleshGlob,
    /// <summary>retool_kit: только внешний вид дробителя.</summary>
    Retool,
}

/// <summary>
/// item/crusher_trophy.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherTrophyComponent : Component
{
    [DataField(required: true)]
    public CrusherTrophyKind Trophy;

    /// <summary>bonus_value.</summary>
    [DataField]
    public float BonusValue = 10f;

    /// <summary>denied_type: два трофея одной группы не ставятся вместе.</summary>
    [DataField(required: true)]
    public string Group = string.Empty;

    /// <summary>effect_desc().</summary>
    [DataField(required: true)]
    public LocId Effect;

    /// <summary>blaster_tubes deadly_shot.</summary>
    [ViewVariables]
    public bool DeadlyShot;

    [ViewVariables]
    public TimeSpan DeadlyShotReset;

    /// <summary>Перезарядка эффекта трофея (bileworm, ice_demon_cube).</summary>
    [ViewVariables]
    public TimeSpan CooldownEnd;

    /// <summary>Перезарядка использования в руке (broodmother_tongue, legionnaire_spine).</summary>
    [ViewVariables]
    public TimeSpan UseCooldownEnd;
}

/// <summary>
/// status_effect/crusher_mark.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CrusherMarkComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField]
    public SpriteSpecifier.Rsi Effect = new(new ResPath("/Textures/Objects/Weapons/Effects"), "shield2");
}

/// <summary>
/// element/crusher_loot: трофей выпадает, если большую часть урона нанёс дробитель.
/// </summary>
[RegisterComponent]
public sealed partial class CrusherLootComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Trophies = new();

    /// <summary>drop_mod, проценты.</summary>
    [DataField]
    public float DropChance = 25f;

    /// <summary>guaranteed_drop: доля урона дробителем, при которой трофей выпадает всегда (мегафауна — 0.6).</summary>
    [DataField]
    public float? GuaranteedDrop;
}

/// <summary>
/// status_effect/crusher_damage: сколько урона нанёс дробитель.
/// </summary>
[RegisterComponent]
public sealed partial class CrusherDamageComponent : Component
{
    [ViewVariables]
    public float TotalDamage;
}

[Serializable, NetSerializable]
public enum CrusherVisuals : byte
{
    /// <summary>retool_kit/ashenskull: облик черепа.</summary>
    Skull,
}

/// <summary>status_effect/ice_block_talisman: заморожен в ледяной глыбе и не может двигаться.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CrusherFrozenComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan Until;
}

/// <summary>status_effect/speed_boost (wolf_ear).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CrusherSpeedBoostComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan Until;

    [DataField]
    public float Modifier = 1.5f;
}
