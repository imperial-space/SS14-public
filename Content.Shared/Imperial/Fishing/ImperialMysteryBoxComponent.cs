using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Пиратский сундук с сокровищами (/obj/structure/mystery_box/fishing): при открытии перебирает призы,
/// показывает выпавший и отдаёт его тому, кто успеет взять. Ограниченное число открытий.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialMysteryBoxComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Prizes = new();

    /// <summary>uses_left.</summary>
    [DataField]
    public int UsesLeft = 18;

    /// <summary>Сколько раз один разум может открыть сундук.</summary>
    [DataField]
    public int MaxOpensPerMind = 3;

    [ViewVariables]
    public ImperialMysteryBoxState State = ImperialMysteryBoxState.Standby;

    [ViewVariables]
    public EntProtoId? Selected;

    [ViewVariables]
    public bool Claimable;

    [ViewVariables]
    public TimeSpan NextStep;

    /// <summary>Расписание смены картинки приза во время выбора.</summary>
    [ViewVariables]
    public Queue<TimeSpan> Changes = new();

    [ViewVariables]
    public Dictionary<EntityUid, int> MindOpens = new();
}
