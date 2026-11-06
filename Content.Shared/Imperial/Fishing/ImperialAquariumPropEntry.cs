using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialAquariumPropEntry
{
    public NetEntity Entity;
    public string? Proto;
    public string Name = string.Empty;
}
