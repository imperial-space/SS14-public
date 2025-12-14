namespace Content.Shared.Imperial.RandomSteal.Components;

[RegisterComponent]
public sealed partial class StealChanceIncreaserComponent : Component
{
    [DataField]
    public int Bonus = 30;
}
