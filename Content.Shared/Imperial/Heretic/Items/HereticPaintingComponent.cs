namespace Content.Shared.Imperial.Heretic.Items;

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

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> PlayerCooldowns = new();
}
