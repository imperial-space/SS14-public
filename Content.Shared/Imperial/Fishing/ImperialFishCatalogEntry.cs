using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialFishCatalogEntry
{
    public string Prototype = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;

    public string Fluid = string.Empty;
    public float TempMin;
    public float TempMax;
    public string Feed = string.Empty;
    public string Source = string.Empty;
    public float Size;
    public float Weight;
    public int Beauty;
    public ImperialFishCatalogTips Tips = new();
}
