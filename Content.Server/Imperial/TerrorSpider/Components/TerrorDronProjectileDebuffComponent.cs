namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorDronProjectileDebuffComponent : Component
{
    [DataField]
    public float SlowDuration = 2f;

    [DataField]
    public float SlowMultiplier = 0.5f;

    [DataField]
    public float StaminaDamage = 15f;
}
