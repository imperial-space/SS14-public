using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob;

[Serializable, NetSerializable]
public sealed class BlobSelectStrainMessage(string strain) : BoundUserInterfaceMessage
{
    public readonly string Strain = strain;
}
