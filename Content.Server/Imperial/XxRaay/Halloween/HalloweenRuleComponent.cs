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
    public List<HalloweenWave> Waves { get; set; } = new();

    /// <summary>Time between waves.</summary>
    [DataField("timeBetweenWaves")]
    public TimeSpan TimeBetweenWaves { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>Prototype for the portal entity.</summary>
    [DataField("portalPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string PortalPrototype = "HalloweenPortal";

    /// <summary>Optional final queen wave.</summary>
    [DataField("queenWave")]
    public QueenWaveDefinition? QueenWave { get; set; }

    /// <summary>Delay before queen wave after last regular wave.</summary>
    [DataField("timeBeforeQueen")]
    public TimeSpan TimeBeforeQueen { get; set; } = TimeSpan.FromMinutes(10);
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
