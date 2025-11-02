using Robust.Shared.Prototypes;
using Content.Shared.Imperial.HalloweenCultist;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.HalloweenCultist.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedHalloweenCultistSystem))]
public sealed partial class PumpkinCultistComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ShopActionId = "ActionPumpkinCultOpenShop";
}
