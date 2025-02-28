namespace Content.Shared.Imperial.IdentityFaction.Components;
[RegisterComponent]
public sealed partial class IdentityFactionComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Faction = "NanoTrasen";
}
