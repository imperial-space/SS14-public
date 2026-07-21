using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Forged;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ForgedComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, EntityUid> FittedModules = new();

    public TimeSpan LastExplosivePress = TimeSpan.Zero;
}
