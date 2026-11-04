using Content.Shared.Dataset;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Фазы войны: подготовка обороны → орбитальный обстрел → штурм → развязка (высадка Чёрного легиона).
/// </summary>
[Serializable, NetSerializable]
public enum CadiaWarPhase : byte
{
    Preparation,
    Bombardment,
    Assault,
    Finale,
    /// <summary>Кадия пала: защитники уходят транспортами, Хаос пытается никого не выпустить.</summary>
    Evacuation,
    Ended,
}
