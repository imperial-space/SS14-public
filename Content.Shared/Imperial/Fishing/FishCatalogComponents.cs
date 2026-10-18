using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Энциклопедия рыб (/obj/item/book/manual/fish_catalog).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishCatalogComponent : Component;

[Serializable, NetSerializable]
public enum ImperialFishCatalogUiKey : byte
{
    Key,
}

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

[Serializable, NetSerializable]
public sealed class ImperialFishCatalogBoundUserInterfaceState(List<ImperialFishCatalogEntry> entries) : BoundUserInterfaceState
{
    public readonly List<ImperialFishCatalogEntry> Entries = entries;
}
