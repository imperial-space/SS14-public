using Content.Shared.Damage;

namespace Content.Server.Imperial.Heretic.Paths.Ash;

/// <summary>
/// Кольцо огня вокруг еретика (Клятва огненного кольца): периодически жжёт живых существ рядом.
/// </summary>
[RegisterComponent, Access(typeof(HereticAshActionsSystem))]
public sealed partial class HereticFireRingOathComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new();

    [DataField]
    public float Radius = 2f;

    [DataField]
    public float FireStacks = 0.5f;

    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(0.5);

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public int TicksLeft;
}
