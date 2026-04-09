namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderMotherJellyComponent : Component
{
    [DataField]
    public float BuffRange = 2f;

    [DataField]
    public float BuffHealPerTick = 6f;

    [DataField]
    public float BuffInterval = 1f;

    [DataField]
    public float BuffDuration = 25f;
}
