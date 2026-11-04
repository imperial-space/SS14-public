using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Только что появившийся боец неуязвим несколько секунд или до первого выстрела.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaSpawnProtectionComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Until;
}
