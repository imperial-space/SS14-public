namespace Content.Server.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobLauncherComponent : Component
{
    [DataField]
    public float AttackRange = 4.5f;

    [DataField]
    public float AttackInterval = 4.5f;

    [DataField]
    public int BaseDamage = 7;

    [ViewVariables]
    public float AttackAccumulator;
}