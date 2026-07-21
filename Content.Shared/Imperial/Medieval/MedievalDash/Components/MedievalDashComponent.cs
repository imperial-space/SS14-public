using System.Numerics;
using Content.Shared.Imperial.EntityLayer;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Dash;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalDashComponent : Component
{
    /// <summary>
    /// Force of dash
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Force = 770.0f;

    /// <summary>
    /// Stamina damage on dash
    /// </summary>
    [DataField]
    public float StaminaDamage = 15f;

    /// <summary>
    /// Dash reload time
    /// </summary>
    [DataField]
    public TimeSpan DashReloadTime = TimeSpan.FromSeconds(2f);

    /// <summary>
    /// Z-level of dashed entities
    /// </summary>
    [DataField]
    public EntityLayerGroups DashLayer = EntityLayerGroups.Dash;

    /// <summary>
    /// Required body status for dash
    /// </summary>
    [DataField]
    public HashSet<BodyStatus> RequiredBodyStatus = new() { BodyStatus.OnGround };


    [ViewVariables]
    public bool IsDashing = false;

    [ViewVariables]
    public TimeSpan NextDash = TimeSpan.Zero;

    [ViewVariables]
    public TimeSpan DashEndTime = TimeSpan.Zero;

    [ViewVariables]
    public GameTick DashButtonPressedTick;

    [ViewVariables]
    public HashSet<EntityLayerGroups> CachedLayers = new();

    [DataField]
    public Vector2 StartDashPos;

    [DataField]
    public Vector2? LegalEndDashPos;
}
