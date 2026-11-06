using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob;

[Serializable, NetSerializable]
public sealed class BlobRerollState(List<string> choices) : BoundUserInterfaceState
{
    public readonly List<string> Choices = choices;
}
