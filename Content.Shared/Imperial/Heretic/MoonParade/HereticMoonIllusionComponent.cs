using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.MoonParade;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticMoonIllusionComponent : Component
{
    [AutoNetworkedField]
    public List<PrototypeLayerData> ClothingLayers = new();
}
