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

    [DataField]
    public TimeSpan ApplyHereticEffectWeepingDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan ApplyNonHereticEffectWeepingDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public float ApplyNonHereticEffectDesireAmount = -100f;

    [DataField]
    public float ApplyNonHereticEffectRustRange = 0.4f;
}
