namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>blueprints/slime: делает зону вокруг пригодной для консоли ксенобиологии.</summary>
[RegisterComponent]
public sealed partial class SlimeBlueprintComponent : Component
{
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId Zone = "XenobioZoneMarker";
}
