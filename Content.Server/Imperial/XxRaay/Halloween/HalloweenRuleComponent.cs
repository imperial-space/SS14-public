using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Imperial.XxRaay.Halloween;

[RegisterComponent]
public sealed partial class HalloweenRuleComponent : Component
{
    /// <summary>Is the event currently active?</summary>
    [DataField("active")]
    public bool Active { get; set; } = false;

    /// <summary>Ordered list of regular waves.</summary>
    [DataField("waves")]
    public List<HalloweenWave> Waves { get; set; } = new()
    {
        new HalloweenWave
        {
            MobCount = 7,
            WaveLength = TimeSpan.FromMinutes(3),
            MobPrototypes = new List<string> { "MobHalloweenSmallPumpkin" }
        },
        // Wave 2
        new HalloweenWave
        {
            MobCount = 7,
            WaveLength = TimeSpan.FromMinutes(3),
            MobPrototypes = new List<string>
            {
                "MobHalloweenSmallPumpkin",
                "MobHalloweenFlyingPumpkin",
                "MobHalloweenAngryPumpkin"
            }
        },
        // Wave 3
        new HalloweenWave
        {
            MobCount = 7,
            WaveLength = TimeSpan.FromMinutes(3),
            MobPrototypes = new List<string>
            {
                "MobHalloweenSmallPumpkin",
                "MobHalloweenFlyingPumpkin",
                "MobHalloweenAngryPumpkin"
            }
        },
        // Wave 4
        new HalloweenWave
        {
            MobCount = 10,
            WaveLength = TimeSpan.FromMinutes(4),
            MobPrototypes = new List<string>
            {
                "MobHalloweenSmallPumpkin",
                "MobHalloweenFlyingPumpkin",
                "MobHalloweenAngryPumpkin",
            }
        },
        // Wave 5
        new HalloweenWave
        {
            MobCount = 10,
            WaveLength = TimeSpan.FromMinutes(4),
            MobPrototypes = new List<string>
            {
                "MobHalloweenSmallPumpkin",
                "MobHalloweenFlyingPumpkin",
                "MobHalloweenAngryPumpkin"
            }
        },
        // Wave 6
        new HalloweenWave
        {
            MobCount = 10,
            WaveLength = TimeSpan.FromMinutes(5),
            MobPrototypes = new List<string>
            {
                "MobHalloweenSmallPumpkin",
                "MobHalloweenFlyingPumpkin",
                "MobHalloweenAngryPumpkin",
                "MobHalloweenMinionPumpkin"
            }
        },
        // Wave 7
        new HalloweenWave
        {
            MobCount = 10,
            WaveLength = TimeSpan.FromMinutes(5),
            MobPrototypes = new List<string>
            {
                "MobHalloweenSmallPumpkin",
                "MobHalloweenFlyingPumpkin",
                "MobHalloweenAngryPumpkin",
                "MobHalloweenMinionPumpkin",
                "MobHalloweenCrystalPumpkin"
            }
        },
        // Wave 8
        new HalloweenWave
        {
            MobCount = 15,
            WaveLength = TimeSpan.FromMinutes(6),
            MobPrototypes = new List<string>
            {
                "MobHalloweenSmallPumpkin",
                "MobHalloweenFlyingPumpkin",
                "MobHalloweenAngryPumpkin",
                "MobHalloweenMinionPumpkin",
                "MobHalloweenCrystalPumpkin"
            }
        }
    };

    /// <summary>Time between waves.</summary>
    [DataField("timeBetweenWaves")]
    public TimeSpan TimeBetweenWaves { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Prototype for the portal entity.</summary>
    [DataField("portalPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string PortalPrototype = "HalloweenPortal";

    /// <summary>Optional final queen wave.</summary>
    [DataField("queenWave")]
    public QueenWaveDefinition? QueenWave { get; set; } = new QueenWaveDefinition
    {
        QueenPrototype = "MobHalloweenQueen",
        Escorts = new Dictionary<string, int>
        {
            { "MobHalloweenMinionPumpkin", 3 },
            { "MobHalloweenCrystalPumpkin", 2 }
        },
        SurvivalDuration = TimeSpan.FromMinutes(10)
    };

    /// <summary>Delay before queen wave after last regular wave.</summary>
    [DataField("timeBeforeQueen")]
    public TimeSpan TimeBeforeQueen { get; set; } = TimeSpan.FromMinutes(1);
}

[DataDefinition]
public sealed partial class HalloweenWave
{
    /// <summary>Total spawn attempts in this wave.</summary>
    [DataField("mobCount")]
    public int MobCount;

    /// <summary>Wave duration; spawn attempts are spread evenly over this time.</summary>
    [DataField("waveLength")]
    public TimeSpan WaveLength = TimeSpan.FromMinutes(5);

    /// <summary>Mob prototypes to pick from.</summary>
    [DataField("mobPrototypes")]
    public List<string> MobPrototypes = new();
}

[DataDefinition]
public sealed partial class QueenWaveDefinition
{
    [DataField("queenPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string QueenPrototype = "MobHalloweenQueen";

    /// <summary>Escort mobs: prototypeId -> count.</summary>
    [DataField("escorts")]
    public Dictionary<string, int> Escorts = new();

    /// <summary>How long the queen must survive to trigger evac.</summary>
    [DataField("survivalDuration")]
    public TimeSpan SurvivalDuration = TimeSpan.FromMinutes(15);
}
