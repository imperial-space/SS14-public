namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderMotherRegenBuffComponent : Component
{
    [DataField]
    public float HealPerTick = 6f;

    [DataField]
    public float TickInterval = 1f;

    [ViewVariables]
    public TimeSpan NextTick = TimeSpan.Zero;

    [ViewVariables]
    public TimeSpan ExpiresAt = TimeSpan.Zero;
}
