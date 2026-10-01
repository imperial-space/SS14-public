using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Аритмичный нож: пока он в руке, скорость владельца каждые несколько секунд случайно меняется.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NullRodArrhythmicComponent : Component
{
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(2);

    [DataField]
    public float MinSpeedModifier = 0.6f;

    [DataField]
    public float MaxSpeedModifier = 1.5f;

    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1f;

    [ViewVariables]
    public TimeSpan NextChange;
}
