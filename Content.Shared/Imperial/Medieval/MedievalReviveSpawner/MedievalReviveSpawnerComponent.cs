using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Revive;


[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalReviveSpawnerComponent : Component
{
    public EntProtoId? Prototype = "MedievalMobPlayerRespawn";
}
