using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Chaplain.Components;

/// <summary>
/// Маяк вооружения капеллана (choice_beacon/holy из SS13): святой выбирает в радиальном меню набор брони.
/// Выбор один на станцию — следующие маяки сразу присылают тот же набор.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HolyArmamentsBeaconComponent : Component;
