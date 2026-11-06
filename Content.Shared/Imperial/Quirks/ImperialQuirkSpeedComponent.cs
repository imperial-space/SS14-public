using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Quirks;

/// <summary>
/// Изменение скорости от причуд: хромота, паралич половины тела, поселенец, космический житель.
/// Сервер меняет множители, клиент предсказывает движение.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialQuirkSpeedComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Walk = 1f;

    [DataField, AutoNetworkedField]
    public float Sprint = 1f;
}
