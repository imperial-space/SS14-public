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
