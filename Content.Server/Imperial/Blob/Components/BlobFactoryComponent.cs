using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobFactoryComponent : Component
{
    [DataField]
    public EntProtoId SporePrototype = "MobBlobSpore";

    [DataField]
    public float SpawnInterval = 50f;

    [DataField]
    public int MaxActiveSpores = 3;

    [ViewVariables]
    public float SpawnAccumulator;

    [ViewVariables]
    public readonly HashSet<EntityUid> ActiveSpores = new();
}