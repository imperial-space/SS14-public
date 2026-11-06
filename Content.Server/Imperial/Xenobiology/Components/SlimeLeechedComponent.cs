namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>status_effect/slime_leech: на жертве висит кормящийся слайм.</summary>
[RegisterComponent]
public sealed partial class SlimeLeechedComponent : Component
{
    [ViewVariables]
    public EntityUid Slime;

    [ViewVariables]
    public TimeSpan NextTick;
}
