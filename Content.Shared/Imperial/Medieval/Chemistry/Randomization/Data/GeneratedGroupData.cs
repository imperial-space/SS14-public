using Content.Shared.Chemistry.Reaction;

namespace Content.Shared.Imperial.Medieval.ChemistryRandomization;

/// <summary>
/// Основная информация о реагенте
/// </summary>
public sealed class GeneratedGroupData
{
    public string ID = "";
    public Dictionary<string, GeneratedReagentData> GeneratedPotions = new();
    public List<Dictionary<string, ReactantPrototype>> RecipesLeft = new();
}
