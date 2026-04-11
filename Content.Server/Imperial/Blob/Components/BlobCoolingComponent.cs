namespace Content.Server.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobCoolingComponent : Component
{
    [DataField]
    public float EffectRange = 3f;

    [DataField]
    public float EffectInterval = 2f;

    [DataField]
    public int BurnHeal = 6;

    [ViewVariables]
    public float EffectAccumulator;
}