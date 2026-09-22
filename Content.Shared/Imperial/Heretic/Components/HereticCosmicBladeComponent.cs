namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticCosmicBladeComponent : Component
{
    [DataField]
    public List<EntityUid> RecentVictims = new();

    [DataField]
    public int ComboCount;

    [DataField]
    public TimeSpan LastHitTime;

    [DataField]
    public TimeSpan ComboWindow = TimeSpan.FromSeconds(2);

    [DataField]
    public bool TrailActive;
}
