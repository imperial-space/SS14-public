using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.ItemShow;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ItemDisplayComponent : Component
{
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid ItemUid { get; set; }

    public TimeSpan DespawnAt { get; set; }

    public TimeSpan DespawnDelay { get; set; } = TimeSpan.FromSeconds(3.5f);
}
