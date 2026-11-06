namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent]
public sealed partial class HereticEldritchCoinComponent : Component
{
    [DataField]
    public float FlipRange = 5f;

    [DataField]
    public TimeSpan UseInHandDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public float UseInHandChance = 0.5f;

    [DataField]
    public float AfterInteractChance = 0.5f;
}
