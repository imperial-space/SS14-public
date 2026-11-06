using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>Королева, а также принцесса и императрица (подтипы queen).</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderQueenComponent : Component
{
    [DataField]
    public bool Princess;

    [DataField]
    public bool Empress;

    /// <summary>spider_spawnfrequency (1200 = 120 секунд).</summary>
    [DataField]
    public TimeSpan SpawnFrequency = TimeSpan.FromSeconds(120);

    /// <summary>nestfrequency (300 = 30 секунд).</summary>
    [DataField]
    public TimeSpan NestFrequency = TimeSpan.FromSeconds(30);

    [DataField]
    public int MaxPerNest = 35;

    /// <summary>spider_max_children принцессы.</summary>
    [DataField]
    public int MaxChildren = 8;

    [DataField]
    public int CanLay = 5;

    [DataField]
    public bool HasNested;

    [ViewVariables]
    public int EggsLaid;

    [ViewVariables]
    public TimeSpan LastSpawn;

    [ViewVariables]
    public TimeSpan LastNestSetup;

    [ViewVariables]
    public int NestStep;

    [ViewVariables]
    public EntityUid? NestVent;
}
