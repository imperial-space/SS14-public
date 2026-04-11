namespace Content.Server.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobbernautComponent : Component
{
    [DataField]
    public float HealInterval = 2.5f;

    [DataField]
    public float HealAmount = 3f;

    [ViewVariables]
    public float HealAccumulator;
}