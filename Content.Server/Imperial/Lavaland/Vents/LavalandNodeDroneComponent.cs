using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Vents;

/// <summary>Дрон NODE: держит жилу во время обороны.</summary>
[RegisterComponent]
public sealed partial class LavalandNodeDroneComponent : Component
{
    [ViewVariables] public EntityUid? Vent;
    [ViewVariables] public bool Escaping;

    [DataField]
    public float DroneStateChangedTotalIntensity = 10f;

    [DataField]
    public float DroneStateChangedSlope = 5f;

    [DataField]
    public float DroneStateChangedMaxTileIntensity = 4f;
}
