namespace Content.Shared.Imperial.WhitelistClothing.Components;

[RegisterComponent]
public sealed partial class WhitelistClothingComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Whitelist = "GoblinArmor";
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Slot = "outerclothing";
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public string WhitelistState = "humanoid";
}
