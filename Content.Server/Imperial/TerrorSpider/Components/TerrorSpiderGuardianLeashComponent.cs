namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderGuardianLeashComponent : Component
{
    [DataField]
    public float RequiredRoyalRange = 8f;

    [DataField]
    public float DamageIfFar = 6f;

    [DataField]
    public float TickInterval = 1f;

    [DataField]
    public float WarningCooldown = 4f;

    [ViewVariables]
    public TimeSpan NextTickTime = TimeSpan.Zero;

    [ViewVariables]
    public TimeSpan NextWarningTime = TimeSpan.Zero;
}
