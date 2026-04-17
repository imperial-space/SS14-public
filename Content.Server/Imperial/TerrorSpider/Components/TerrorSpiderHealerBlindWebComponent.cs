namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderHealerBlindWebComponent : Component
{
    [DataField]
    public float BlindDuration = 30f;
}
