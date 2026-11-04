using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>
/// Игрок смотрит на соседний этаж (lookup/lookdown SS13). Камера остаётся на игроке, а клиент рисует этаж
/// <see cref="Map"/> поверх своего; сервер держит на том этаже точку взгляда, чтобы игроку приходили его сущности.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZLookingComponent : Component
{
    /// <summary>Карта этажа, на который смотрят.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Map;

    [DataField, AutoNetworkedField]
    public bool Up;

    /// <summary>Точка взгляда на том этаже (только сервер).</summary>
    [ViewVariables]
    public EntityUid Holder;
}
