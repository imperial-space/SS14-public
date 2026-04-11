namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderQueenOrphanDamageComponent : Component
{
    [DataField]
    public float DamagePerTick = 6f;

    [DataField]
    public float TickInterval = 2f;

    [DataField]
    public TimeSpan NextTick = TimeSpan.Zero;
}
