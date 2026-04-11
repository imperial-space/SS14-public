namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderWidowMeleeDebuffComponent : Component
{
    [DataField]
    public float MuteDuration = 10f;

    [DataField]
    public float VenomPerHit = 10f;

    [DataField]
    public string VenomReagent = "BlackTerrorVenom";
}
