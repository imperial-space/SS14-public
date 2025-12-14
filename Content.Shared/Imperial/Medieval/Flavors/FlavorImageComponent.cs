using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Flavors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlavorImageComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string? ImagePath;
};
