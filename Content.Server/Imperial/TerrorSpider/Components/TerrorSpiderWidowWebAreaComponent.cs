namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderWidowWebAreaComponent : Component
{
    [DataField]
    public float VenomOnTouch = 20f;

    [DataField]
    public string VenomReagent = "BlackTerrorVenom";
}
