using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.LavalandPlanet;

[RegisterComponent]
public sealed partial class LavalandPlanetRuleComponent : Component
{
    // Grid-category map files (category: Grid)
    [DataField]
    public List<string> StructureGridFiles = new();

    // Map-category map files (category: Map) — loaded via TryLoadMap, grids reparented
    [DataField]
    public List<string> StructureMapFiles = new();

    [DataField]
    public string? RecyclingOutpostMap;

    [DataField]
    public string? PrisonMap;

    [DataField]
    public string? ShuttleMap;

    [DataField]
    public List<string> OreLayers = new();

    [DataField]
    public List<string> MobLayers = new();

    [DataField]
    public List<EntProtoId> MegafaunaPrototypes = new();

    [DataField]
    public EntProtoId NecroposisSpikePrototype = "NecroposisSpikeImperial";

    [DataField]
    public int NecroposisSpikeCount = 20;

    [DataField]
    public float MinStructureDistance = 30f;

    [DataField]
    public float MaxStructureDistance = 240f;

    [DataField]
    public float BorderRadius = 255f;

    [DataField]
    public string BiomeTemplate = "Lava";

    [DataField]
    public Color MapLight = Color.FromHex("#A34931");

    // Structure grids waiting for atmosphere rebuild (deferred one tick so atmos Tiles are populated).
    public List<EntityUid> PendingAtmosRebuild = new();
}
