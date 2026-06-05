using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.DrakePlayerActions;

/// <summary>
/// Marks a player-controlled drake mob as having the fire cone ability.
/// The server system will grant the corresponding action on MapInit.
/// </summary>
[RegisterComponent]
public sealed partial class DrakeFireConeComponent : Component
{
    /// <summary>Prototype ID of the action to grant.</summary>
    [DataField]
    public EntProtoId ActionId = "ActionDrakeFireCone";

    /// <summary>The spawned action entity (server-managed).</summary>
    [DataField]
    public EntityUid? ActionEntity;

    /// <summary>How many rays to fire.</summary>
    [DataField]
    public int RayCount = 3;

    /// <summary>Angular spread of the cone in degrees.</summary>
    [DataField]
    public float SpreadDeg = 40f;

    /// <summary>Number of tiles per ray.</summary>
    [DataField]
    public int Range = 12;

    /// <summary>Delay (seconds) between each successive tile in a ray.</summary>
    [DataField]
    public float StepDelay = 0.1f;

    /// <summary>Fire tile entity prototype to spawn per step.</summary>
    [DataField]
    public string FireTileProto = "ImperialAshDrakeSnakeFireTile";

    /// <summary>Heat damage per tick on each fire tile.</summary>
    [DataField]
    public float TileDamage = 10f;
}
