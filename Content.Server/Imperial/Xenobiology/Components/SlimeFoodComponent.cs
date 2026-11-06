namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>status_effect/slime_food: феромоны — слайм, высосавший носителя, подружится с кормильцем.</summary>
[RegisterComponent]
public sealed partial class SlimeFoodComponent : Component
{
    [ViewVariables]
    public EntityUid? Feeder;

    [DataField]
    public float BefriendChance = 100f;
}
