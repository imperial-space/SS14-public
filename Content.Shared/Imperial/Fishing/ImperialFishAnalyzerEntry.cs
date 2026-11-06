using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

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
