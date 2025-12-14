namespace Content.Shared.Imperial.Medieval.ChemistryRandomization;

/// <summary>
/// Основная информация о реагенте
/// </summary>
public sealed class GeneratedReagentData
{
    public string Name = "";
    public string Description = "";
    public string Flavor = "";
    public Color Color = Color.White;
    public List<ReactionData> Reactions = default!;
    public string Group = "";
}
