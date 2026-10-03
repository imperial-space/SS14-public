using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Chaplain.Components;

/// <summary>
/// Святая роль (holy_role из SS13). Даёт право пользоваться святыми предметами.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialHolyComponent : Component
{
    [DataField, AutoNetworkedField]
    public HolyRole Role = HolyRole.Priest;

    /// <summary>Действие «Помолиться», выданное святому.</summary>
    [ViewVariables]
    public EntityUid? PrayAction;
}

/// <summary>
/// Уровни святой роли, от младшего к старшему.
/// </summary>
[Serializable, NetSerializable]
public enum HolyRole : byte
{
    /// <summary>Дьякон: пользуется святыми предметами, но не проводит обряды.</summary>
    Deacon = 1,

    /// <summary>Жрец: обычный капеллан.</summary>
    Priest = 2,

    /// <summary>Верховный жрец: задаёт религию станции и выбирает секту.</summary>
    HighPriest = 3,
}
