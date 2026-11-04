using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Дверь, заблокированная пневматическим затвором.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialSealedDoorComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Seal = "ImperialPneumaticSeal";
}
