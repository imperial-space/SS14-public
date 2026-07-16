using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Applied to an entity when it's hit by a <see cref="KineticCrusherComponent"/> weapon.
/// While present, the entity takes bonus damage from the crusher.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MarkedByKineticCrusherComponent : Component
{
    /// <summary>
    /// When this mark expires.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan ExpiresAt;
}
