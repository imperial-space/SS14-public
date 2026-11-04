using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialAquariumFishEntry
{
    public NetEntity Entity;
    public string? Proto;
    public string Name = string.Empty;
    public bool Alive;

    /// <summary>get_happiness_value(): 0..5 сердечек.</summary>
    public int Happiness;
}
