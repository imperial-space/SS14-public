using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Imperial.XxRaay.Halloween;

[RegisterComponent]
public sealed partial class HalloweenRuleComponent : Component
{
    /// <summary>
    /// Is the event currently active?
    /// </summary>
    [DataField("active")]
    public bool Active { get; set; } = false;

    /// <summary>
    /// A list defining each wave of the event.
    /// </summary>
    [DataField("waves")]
    public List<HalloweenWave> Waves { get; set; } = new();

    /// <summary>
    /// Time between the end of one wave and the start of the next.
    /// </summary>
    [DataField("timeBetweenWaves")]
    public TimeSpan TimeBetweenWaves { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Prototype for the portal entity.
    /// </summary>
    [DataField("portalPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string PortalPrototype = "HalloweenPortal";

    /// <summary>
    // Definition for the final Queen wave.
    /// </summary>
    [DataField("queenWave")]
    public QueenWaveDefinition? QueenWave { get; set; }

    /// <summary>
    /// Time to wait after the last regular wave before spawning the queen.
    /// </summary>
    [DataField("timeBeforeQueen")]
    public TimeSpan TimeBeforeQueen { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Defines a single wave of Halloween mobs.
/// </summary>
[DataDefinition]
public sealed partial class HalloweenWave
{
    /// <summary>
    /// Total number of mobs to spawn in this wave.
    /// </summary>
    [DataField("mobCount")]
    public int MobCount;

    /// <summary>
    /// Total duration of the wave. Mobs will spawn evenly over this time.
    /// </summary>
    [DataField("waveLength")]
    public TimeSpan WaveLength = TimeSpan.FromMinutes(5);

    /// <summary>
    /// A list of mob prototypes that can be spawned in this wave.
    /// The system will pick randomly from this list.
    /// </summary>
    [DataField("mobPrototypes")]
    public List<string> MobPrototypes = new();
}

/// <summary>
/// Defines the final queen wave sequence.
/// </summary>
[DataDefinition]
public sealed partial class QueenWaveDefinition
{
    [DataField("queenPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string QueenPrototype = "MobHalloweenQueen";

    /// <summary>
    /// Mobs that spawn to protect the queen.
    /// Key is the prototype ID, Value is the count.
    /// </summary>
    [DataField("escorts")]
    public Dictionary<string, int> Escorts = new();

    /// <summary>
    /// How long the queen has to survive to win.
    /// </summary>
    [DataField("survivalDuration")]
    public TimeSpan SurvivalDuration = TimeSpan.FromMinutes(15);
}

/// <summary>
/// A marker component for all Halloween event mobs.
/// </summary>
[RegisterComponent]
public sealed partial class HalloweenMobComponent : Component
{
}