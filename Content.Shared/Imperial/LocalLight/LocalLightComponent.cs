using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.LocalLight;

//copypaste from SharedPointLightComponent (can't inherit it directly cause light parameters can only be modified by the light system)
//a better solution would be to move all of the point light parameters to a separate class but that requires changes to the RT
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LocalLightComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color { get; set; } = Color.White;

    [DataField, AutoNetworkedField]
    public Vector2 Offset = Vector2.Zero;

    [DataField, AutoNetworkedField]
    public float Energy { get; set; } = 1f;

    [DataField, AutoNetworkedField]
    public float Softness { get; set; } = 1f;

    [DataField, AutoNetworkedField]
    public bool CastShadows = true;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public float Radius = 5f;

    [DataField, AutoNetworkedField]
    public string? MaskPath;
}
