using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

#region Удочка (tgui FishingRod)

[Serializable, NetSerializable]
public enum ImperialFishingRodUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ImperialFishingRodSlotData
{
    public string? Name;
    public string? Proto;
}

[Serializable, NetSerializable]
public sealed class ImperialFishingRodBoundUserInterfaceState : BoundUserInterfaceState
{
    public ImperialFishingRodSlotData Bait = new();
    public ImperialFishingRodSlotData Line = new();
    public ImperialFishingRodSlotData Hook = new();
    public string Description = string.Empty;
}

/// <summary>slot_action: пустая рука — вынуть, предмет в руке — вставить или поменять.</summary>
[Serializable, NetSerializable]
public sealed class ImperialFishingRodSlotMessage(string slot) : BoundUserInterfaceMessage
{
    public readonly string Slot = slot;
}

#endregion

#region Анализатор (tgui FishAnalyzer)

[Serializable, NetSerializable]
public sealed class ImperialFishAnalyzerTrait
{
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Inheritability;
}

[Serializable, NetSerializable]
public sealed class ImperialFishAnalyzerEvolution
{
    public string Name = string.Empty;
    public string? Proto;
    public int Probability;
    public string Conditions = string.Empty;
}

[Serializable, NetSerializable]
public sealed class ImperialFishAnalyzerEntry
{
    public string Name = string.Empty;
    public string? Proto;
    public float Health;
    public float Size;
    public float Weight;
    public string Food = string.Empty;
    public Color FoodColor = Color.Gray;
    public float MinTemp;
    public float MaxTemp;
    public float Hunger;
    public bool FluidCompatible;
    public string Fluid = string.Empty;
    public bool SuitableTemp;
    public int BreedTimer;
    public List<ImperialFishAnalyzerTrait> Traits = new();
    public List<ImperialFishAnalyzerEvolution> Evolutions = new();
}

#endregion

#region Энциклопедия (tgui FishCatalog)

[Serializable, NetSerializable]
public sealed class ImperialFishCatalogTips
{
    public string Spots = string.Empty;
    public string FavoriteBait = string.Empty;
    public string DislikedBait = string.Empty;
    public List<string> Traits = new();
    public string Difficulty = string.Empty;
}

#endregion

#region Генная пушка (tgui_input_list)

[Serializable, NetSerializable]
public enum ImperialFishGenegunUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ImperialFishGenegunTrait
{
    public string Id = string.Empty;
    public string Name = string.Empty;
}

[Serializable, NetSerializable]
public sealed class ImperialFishGenegunBoundUserInterfaceState(NetEntity fish, List<ImperialFishGenegunTrait> traits) : BoundUserInterfaceState
{
    public readonly NetEntity Fish = fish;
    public readonly List<ImperialFishGenegunTrait> Traits = traits;
}

[Serializable, NetSerializable]
public sealed class ImperialFishGenegunExtractMessage(NetEntity fish, string trait) : BoundUserInterfaceMessage
{
    public readonly NetEntity Fish = fish;
    public readonly string Trait = trait;
}

#endregion
