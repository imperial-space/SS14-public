using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.ShipDrowning;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipDrowningComponent : Component
{
    [DataField("DrownLevel"), AutoNetworkedField]
    public float DrownLevel;

    [DataField("DrownMaxLevel"), AutoNetworkedField]
    public float DrownMaxLevel;

    [DataField("floodPerDamageStage")]
    public float FloodPerDamageStage = 0.1f;

    [DataField("passiveDrainPerTick")]
    public int PassiveDrainPerTick = 5;

    [DataField("passiveRisePerTick")]
    public int PassiveRisePerTick = 5;

    [DataField("maxFloodPerTile")]
    public int MaxFloodPerTile = 100;

    public float VisualDrownLevel;
    public Vector2 VisualWaterOffset;
    public bool VisualDataInitialized;


    public TimeSpan? DisableWavesTime = null;
}
