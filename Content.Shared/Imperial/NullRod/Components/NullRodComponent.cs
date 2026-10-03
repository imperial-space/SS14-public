using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Нулевой стержень капеллана. Использование в руке открывает радиальное меню со всеми формами
/// святого оружия (<see cref="NullRodVariantComponent"/>); выбранная форма заменяет стержень в руке.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NullRodComponent : Component
{
    /// <summary>
    /// Выбор этого стержня становится святым оружием станции: после него другие такие стержни
    /// в этом раунде перевыбрать нельзя.
    /// </summary>
    [DataField]
    public bool StationHolyItem = true;
}
