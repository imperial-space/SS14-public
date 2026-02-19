namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderLurkerWebAreaComponent : Component
{
    [DataField]
    public float StaminaDamage = 100f;

    [DataField]
    public float MuteDuration = 14f;
}
