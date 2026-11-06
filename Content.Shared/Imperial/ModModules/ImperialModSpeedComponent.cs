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
/// slowdown модулей: множитель скорости носителя.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialModSpeedComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Walk = 1f;

    [DataField, AutoNetworkedField]
    public float Sprint = 1f;
}
