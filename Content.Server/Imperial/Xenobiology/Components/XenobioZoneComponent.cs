namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Зона ксенобиологии (area/station/science/xenobiology): консоль действует внутри.</summary>
[RegisterComponent]
public sealed partial class XenobioZoneComponent : Component
{
    [DataField]
    public float Range = 7f;
}
