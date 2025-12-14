using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CanvasComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color[] Texture = new Color[900];
}

[Serializable, NetSerializable]
public sealed partial class CanvasTextureChangedEvent : EntityEventArgs
{
    public NetEntity Canvas;
    public Color[] Texture;

    public CanvasTextureChangedEvent(NetEntity canvas, Color[] texture)
    {
        Canvas = canvas;
        Texture = texture;
    }
}
