using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

[Serializable, NetSerializable]
public sealed class FissionRodDesign
{
    public string Id = string.Empty;
    public FissionRodCategory Category;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public float PowerAmount;
    public float PowerAmpMod = 1;
    public float HeatAmount;
    public float HeatAmpMod = 1;
    /// <summary>Отрицательное — бесконечная прочность.</summary>
    public float MaxDurability;
    public string? HeatEnrichment;
    public float HeatEnrichmentRequirement;
    public string? PowerEnrichment;
    public float PowerEnrichmentRequirement;
    public List<string> NeighborRequirements = new();
    public List<FissionMaterialEntry> Materials = new();
}
