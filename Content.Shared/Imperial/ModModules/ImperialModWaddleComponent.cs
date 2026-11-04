using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.ModModules;

/// <summary>
/// waddle: носитель ходит вразвалочку (покачивание спрайта на клиенте).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialModWaddleComponent : Component
{
    /// <summary>Наибольший наклон в градусах.</summary>
    [DataField, AutoNetworkedField]
    public float Angle = 8f;

    /// <summary>Шагов вразвалку в секунду.</summary>
    [DataField, AutoNetworkedField]
    public float Frequency = 2.5f;
}
