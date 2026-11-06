namespace Content.Shared.Imperial.Heretic.Paths.Blade;

[RegisterComponent]
public sealed partial class LionhunterBulletComponent : Component
{
    [DataField]
    public TimeSpan BulletHitTime = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan BulletHitTime2 = TimeSpan.FromSeconds(6);
}
