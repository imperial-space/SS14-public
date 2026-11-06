using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

// ═══════════════════════ Падение Кадии ═══════════════════════

/// <summary>
/// Маяк эвакуации: после падения Кадии сюда садятся транспорты и забирают тех, кто рядом.
/// Неуязвим до начала эвакуации; уничтоженный маяк означает конец спасения.
/// </summary>
[RegisterComponent]
public sealed partial class CadiaEvacBeaconComponent : Component
{
    [DataField]
    public float Radius = 7f;

    /// <summary>Сколько бойцов берёт один транспорт.</summary>
    [DataField]
    public int Capacity = 10;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(60);

    /// <summary>За сколько до посадки показывается транспорт.</summary>
    [DataField]
    public TimeSpan Warning = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public TimeSpan? NextLoad;

    [ViewVariables]
    public bool ShipComing;
}
