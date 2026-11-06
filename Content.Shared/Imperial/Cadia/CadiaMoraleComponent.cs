using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Мораль бойца: демоны, обстрел, раны и гибель соседей её роняют, комиссар, священник и штандарт поднимают.
/// Космодесант страха не ведает: компонент у него есть по наследству от моба, но не работает.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaMoraleComponent : Component
{
    public const float ShakenBelow = 40f;
    public const float BrokenBelow = 15f;

    [DataField, AutoNetworkedField]
    public float Morale = 100f;

    /// <summary>Сама поднимается только до этого значения: дальше нужен кто-то, кто поведёт.</summary>
    [DataField]
    public float RecoverTo = 70f;

    [DataField]
    public float RecoverRate = 0.4f;

    [ViewVariables]
    public TimeSpan NextPanic;
}
