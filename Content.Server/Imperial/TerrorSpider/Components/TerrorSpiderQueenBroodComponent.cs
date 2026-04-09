namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderQueenBroodComponent : Component
{
    [DataField]
    public EntityUid? Queen;

    [DataField]
    public string? RoyalCooldownKey;
}
