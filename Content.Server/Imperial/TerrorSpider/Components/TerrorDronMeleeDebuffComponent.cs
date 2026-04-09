namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorDronMeleeDebuffComponent : Component
{
    [DataField]
    public float SlowDuration = 4f;

    [DataField]
    public float SlowMultiplier = 0.5f;
}
