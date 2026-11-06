using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

[Serializable, NetSerializable]
public sealed class ImperialPhotoState(string name, byte[] image, string? scribble) : BoundUserInterfaceState
{
    public readonly string Name = name;
    public readonly byte[] Image = image;
    public readonly string? Scribble = scribble;
}
