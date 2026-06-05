namespace Content.Server.Imperial.Lavaland.OrePoints;

[RegisterComponent]
public sealed partial class OrePointsVendingComponent : Component
{
    [DataField]
    public Dictionary<string, int> ItemCosts = new();
}
