using Content.Shared.Dataset;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Края поля Кадии на карте этажа: клетки сетки планировки в мировых координатах. Ставится при постройке поля; по ним
/// сервер не выпускает за скалы, а клиент рисует буран у кромки.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaFieldBoundsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Box2 Bounds;
}
