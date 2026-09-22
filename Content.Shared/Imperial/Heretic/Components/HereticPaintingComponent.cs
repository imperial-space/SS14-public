namespace Content.Shared.Imperial.Heretic.Components;

public enum HereticPaintingType { Weeping, Beauty, Vines, Rust, Desire }

[RegisterComponent]
public sealed partial class HereticPaintingComponent : Component
{
    [DataField]
    public HereticPaintingType PaintingType;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1f);

    [DataField]
    public float VisibilityRange = 7f;

    public TimeSpan NextUpdate;

    public Dictionary<EntityUid, TimeSpan> PlayerCooldowns = new();
}
