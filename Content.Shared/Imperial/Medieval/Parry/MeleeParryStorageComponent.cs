using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.MeleeParry.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MeleeParryStorageComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan NextParryTime = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public float CooldownParry = 0.5f;

    [DataField, AutoNetworkedField]
    public bool ParryQueued = false;
}
