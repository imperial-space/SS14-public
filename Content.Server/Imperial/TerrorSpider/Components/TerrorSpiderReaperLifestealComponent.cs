namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderReaperLifestealComponent : Component
{
    [DataField]
    public float HealAmount = 30f;
}
