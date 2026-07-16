using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Lavaland.LavalandShuttle;

[RegisterComponent]
public sealed partial class LavalandShuttleConsoleComponent : Component
{
    /// <summary>
    /// Grid UID of the Lavaland recycling outpost.
    /// Set by LavalandPlanetRuleSystem after the outpost is loaded.
    /// Used as the FTL dock target for "Fly to Lavaland".
    /// </summary>
    [DataField]
    public EntityUid? RecyclingOutpostGrid;
}
