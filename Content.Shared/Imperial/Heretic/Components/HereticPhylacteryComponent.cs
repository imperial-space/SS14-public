namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Филактерия проклятья. При клике по существу с кровью впитывает 5u крови.
/// Визуальное состояние меняется по мере наполнения.
/// </summary>
[RegisterComponent]
public sealed partial class HereticPhylacteryComponent : Component
{
    [DataField]
    public float BloodPerClick = 5f;
}
