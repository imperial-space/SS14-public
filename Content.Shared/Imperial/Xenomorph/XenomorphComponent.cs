using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

/// <summary>Ксеноморф: каста, узел улья (hivenode), связь с ульем.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenomorphComponent : Component
{
    [DataField, AutoNetworkedField]
    public XenoCaste Caste;

    /// <summary>Прототип, в который эволюционирует личинка/трутень/преторианец (для однозначных эволюций).</summary>
    [DataField]
    public EntProtoId? EvolvesTo;

    /// <summary>Цена эволюции в плазме.</summary>
    [DataField]
    public float EvolveCost;

    /// <summary>hivenode/recent_queen_death: до этого момента улей ослаблен и эволюция недоступна.</summary>
    [ViewVariables]
    public TimeSpan NoQueenUntil;

    /// <summary>alien_queen_finder: насколько далеко королева (-1 — не чувствуется, 0 рядом, 1/2/3 — near/med/far).</summary>
    [ViewVariables, AutoNetworkedField]
    public sbyte QueenDistance = -1;

    /// <summary>alien_queen_finder: направление на королеву.</summary>
    [ViewVariables, AutoNetworkedField]
    public Robust.Shared.Maths.Direction QueenDirection;
}
