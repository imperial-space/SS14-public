using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Moon.Parade;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class HereticMoonIllusionComponent : Component
{
    [AutoNetworkedField]
    public List<PrototypeLayerData> ClothingLayers = new();

    /// <summary>
    /// Клиентский флаг: слои одежды уже добавлены в спрайт иллюзии.
    /// </summary>
    [ViewVariables]
    public bool ClothingLayersApplied;
}
