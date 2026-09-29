namespace Content.Shared.Imperial.Heretic.Paths.Blade;

[RegisterComponent]
public sealed partial class HereticBladePassiveComponent : Component
{
    /// <summary>Когда еретик последний раз контратаковал.</summary>
    [ViewVariables]
    public TimeSpan? LastCounterTime;
}
