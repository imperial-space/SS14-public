using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Vents;

/// <summary>
/// Гейзер лаваленда (structure/geyser из SS13): копит реагент до 500 единиц, восполняя по 2 за 2 с.
/// Обнаружение шахтёрским сканером даёт очки и настоящее имя.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandGeyserComponent : Component
{
    /// <summary>reagent_id; null — случайный (geyser/random).</summary>
    [DataField] public ProtoId<ReagentPrototype>? Reagent;

    [DataField] public float Potency = 2f;
    [DataField] public float MaxVolume = 500f;
    [DataField] public float Interval = 2f;
    [DataField] public int PointValue = 100;
    [DataField] public LocId? TrueName;
    [DataField] public LocId? DiscoveryMessage;
    [DataField] public string Solution = "geyser";

    [DataField] public SoundSpecifier DiscoverSound = new SoundPathSpecifier("/Audio/Imperial/Lavaland/twobeep_high.ogg");
    [DataField] public SoundSpecifier ScanSound = new SoundCollectionSpecifier("ImperialIndustrialScan");

    [ViewVariables] public bool Discovered;
    [ViewVariables] public TimeSpan NextRefill;
}

public enum OreVentSize : byte
{
    Small = 5,
    Medium = 10,
    Large = 15,
}

/// <summary>
/// Рудная жила (structure/ore_vent из SS13): сканируется, затем оборона дрона NODE от волн мобов;
/// после победы раз в минуту выдаёт валун с минералами.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandOreVentComponent : Component
{
    /// <summary>ore_vent_options: веса размеров (large 3, medium 5, small 7).</summary>
    [DataField] public Dictionary<OreVentSize, float> SizeWeights = new()
    {
        { OreVentSize.Large, 3 },
        { OreVentSize.Medium, 5 },
        { OreVentSize.Small, 7 },
    };

    /// <summary>Фиксированный размер (босс-жила — большая).</summary>
    [DataField] public OreVentSize? FixedSize;

    /// <summary>mineral_breakdown: руда (стопка из 1) и вес; пусто — 4 случайных из LavalandMinerals.</summary>
    [DataField] public Dictionary<EntProtoId, float> Minerals = new();

    /// <summary>ore_vent_minerals_lavaland.</summary>
    [DataField] public Dictionary<EntProtoId, float> LavalandMinerals = new()
    {
        { "SteelOre1", 14 },
        { "SpaceQuartz1", 13 },
        { "PlasmaOre1", 10 },
        { "SilverOre1", 7 },
        { "GoldOre1", 5 },
        { "UraniumOre1", 4 },
        { "Coal1", 1 },
    };

    /// <summary>defending_mobs.</summary>
    [DataField] public List<EntProtoId> DefendingMobs = new();

    /// <summary>Босс-жила: вместо волн призывает одного из этих боссов.</summary>
    [DataField] public List<EntProtoId> Bosses = new();

    [DataField] public EntProtoId NodeDrone = "ImperialNodeDrone";
    [DataField] public EntProtoId Boulder = "ImperialBoulder";
    [DataField] public EntProtoId ArtifactBoulder = "ImperialBoulderArtifact";
    [DataField] public EntProtoId Platform = "ImperialBoulderPlatform";

    /// <summary>ARTIFACT_ROLL_CHANCE.</summary>
    [DataField] public float ArtifactChance = 0.07f;

    [DataField] public float BoulderInterval = 60f;
    [DataField] public int MaxBoulders = 10;

    [DataField] public SoundSpecifier ScanSound = new SoundPathSpecifier("/Audio/Imperial/Lavaland/timer.ogg");
    [DataField] public SoundSpecifier DroneCrashSound = new SoundPathSpecifier("/Audio/Imperial/Lavaland/explosion3.ogg");

    [ViewVariables] public OreVentSize Size = OreVentSize.Small;
    [ViewVariables] public bool Discovered;
    [ViewVariables] public bool Tapped;
    [ViewVariables] public bool WaveActive;
    [ViewVariables] public TimeSpan WaveEnd;
    [ViewVariables] public TimeSpan NextSpawn;
    [ViewVariables] public TimeSpan ConfirmUntil;
    [ViewVariables] public EntityUid? Node;
    [ViewVariables] public EntityUid? Boss;
    [ViewVariables] public EntProtoId? SummonedBoss;
    [ViewVariables] public List<EntityUid> Spawned = new();
    [ViewVariables] public TimeSpan NextBoulder;
    [ViewVariables] public string BoulderStyle = "boulder";
}

/// <summary>Дрон NODE: держит жилу во время обороны.</summary>
[RegisterComponent]
public sealed partial class LavalandNodeDroneComponent : Component
{
    [ViewVariables] public EntityUid? Vent;
    [ViewVariables] public bool Escaping;
}

/// <summary>
/// Валун (item/boulder): разбивается киркой за durability шагов в руду; на лаве становится плотом.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandBoulderComponent : Component
{
    [DataField] public OreVentSize Size = OreVentSize.Small;
    [DataField] public int Durability = 5;

    /// <summary>Руда и число единиц материала (100 = лист).</summary>
    [DataField] public Dictionary<EntProtoId, int> Materials = new();

    /// <summary>Артефакт внутри (boulder/artifact).</summary>
    [DataField] public EntProtoId? Artifact;

    [DataField] public float PlatformLifespan = 20f;
    [DataField] public string Style = "boulder";
    [DataField] public EntProtoId Platform = "ImperialBoulderPlatform";

    [DataField] public SoundSpecifier HitSound = new SoundCollectionSpecifier("ImperialPickaxe");
    [DataField] public SoundSpecifier BreakSound = new SoundPathSpecifier("/Audio/Imperial/Lavaland/rock_break.ogg");
}
