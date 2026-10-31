using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Высота этажа стопки Кадии для шаттлов: земля, небо, орбита.</summary>
[Serializable, NetSerializable]
public enum CadiaFlightLevel : byte
{
    Surface,
    Air,
    Orbit,
}

/// <summary>
/// Карта-этаж стопки Кадии с ролью для полётов: на соседний этаж шаттл летит вертикальным перелётом,
/// в космос — только с орбиты, из космоса — только на орбиту. В небе шаттл без двигателей падает на землю.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaFlightLevelComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaFlightLevel Level;

    /// <summary>Поле боя (стопка), к которому относится этаж. Этажи одного поля — соседи по высоте.</summary>
    [DataField, AutoNetworkedField]
    public string Field = string.Empty;
}
