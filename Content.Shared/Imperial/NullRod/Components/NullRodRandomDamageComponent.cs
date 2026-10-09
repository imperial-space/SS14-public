using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Урон каждого удара умножается на случайное число (иномерный клинок: от почти нуля до двойного).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NullRodRandomDamageComponent : Component
{
    [DataField]
    public float MinMultiplier = 1f / 15f;

    [DataField]
    public float MaxMultiplier = 2f;
}
