using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>charged/gold: медленно призывает 10 враждебных существ.</summary>
[RegisterComponent]
public sealed partial class SlimeCrossMobSpawnerComponent : Component
{
    [DataField]
    public int MaxSpawn = 10;

    [ViewVariables]
    public int Spawned;

    [ViewVariables]
    public TimeSpan NextSpawn;

    [DataField]
    public TimeSpan SpawnDelay = TimeSpan.FromSeconds(2);
}
