using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>
/// Трап (/obj/structure/ladder): сам связывается с трапом ровно этажом выше и ниже. Клик — вверх, Alt-клик — вниз.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZLadderComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Up;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Down;

    /// <summary>travel_time = 1 SECONDS.</summary>
    [DataField]
    public TimeSpan TravelTime = TimeSpan.FromSeconds(1);

    /// <summary>Связываются только проходы одного вида: трап с трапом, вентиляция с вентиляцией.</summary>
    [DataField]
    public string Kind = "ladder";

    /// <summary>Начало ключей локализации для подсказок и сообщений (zlevel-ladder-climb-up и т.д.).</summary>
    [DataField]
    public string LocPrefix = "zlevel-ladder";

    /// <summary>Тянуть ли за собой тех, кого тащишь (ZMOVE_INCLUDE_PULLED). В вентиляцию никого не протащишь.</summary>
    [DataField]
    public bool IncludePulled = true;

    /// <summary>Делит ли газ своей клетки с клеткой прохода этажом ниже.</summary>
    [DataField]
    public bool PassGas;
}
