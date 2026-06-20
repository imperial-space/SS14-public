using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.DrakePlayerActions;

/// <summary>
/// Marks a player-controlled drake mob as having the flight/swoop ability.
/// The server system will grant the corresponding action on MapInit.
/// </summary>
[RegisterComponent]
public sealed partial class DrakeFlightComponent : Component
{
    /// <summary>Prototype ID of the action to grant.</summary>
    [DataField]
    public EntProtoId ActionId = "ActionDrakeFlight";

    /// <summary>The spawned action entity (server-managed).</summary>
    [DataField]
    public EntityUid? ActionEntity;

    /// <summary>Optional revert action for polymorphed lesser dragon forms.</summary>
    [DataField]
    public EntityUid? RevertActionEntity;

    /// <summary>Maximum teleport range in tiles.</summary>
    [DataField]
    public float MaxRange = 10f;

    /// <summary>How long the swoop lasts before landing.</summary>
    [DataField]
    public float FlightDuration = 1f;

    /// <summary>Visual shadow entity to show at origin during flight.</summary>
    [DataField]
    public string ShadowProto = "ImperialAshDrakeShadow";

    /// <summary>Landing warning entity to show at destination.</summary>
    [DataField]
    public string LandingProto = "ImperialAshDrakeLandingWarning";

    /// <summary>Fire effect prototype spawned on landing.</summary>
    [DataField]
    public string LandingFireProto = "ImperialAshDrakeFireTile";

    /// <summary>Landing fire radius.</summary>
    [DataField]
    public int LandingFireRadius = 2;

    /// <summary>Damage applied by landing fire tiles.</summary>
    [DataField]
    public float LandingFireDamage = 10f;
}
