using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>
/// Межэтажная связь коммуникаций (multi-z cable hub, multi-deck adapter, мусоропровод вверх/вниз): сама находит
/// такую же сущность того же вида ровно этажом выше и ниже в той же клетке.
/// </summary>
[RegisterComponent]
public sealed partial class ZLinkComponent : Component
{
    /// <summary>Связываются только одинаковые виды: кабель HV с кабелем HV, труба с трубой и т.д.</summary>
    [DataField(required: true)]
    public string Kind = string.Empty;

    [ViewVariables]
    public EntityUid? Up;

    [ViewVariables]
    public EntityUid? Down;
}
