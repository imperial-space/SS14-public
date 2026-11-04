using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Святой готов контратаковать выбранную цель клинком из ножен.
/// </summary>
[RegisterComponent]
public sealed partial class NullRodCounterStanceComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public EntityUid Sheath;

    [ViewVariables]
    public TimeSpan Expires;

    /// <summary>До какого момента святой стоит на месте (Immobilize в SS13, без оглушения: руки свободны).</summary>
    [ViewVariables]
    public TimeSpan ImmobileUntil;

    [ViewVariables]
    public bool Immobile;

    /// <summary>Удар, который отбит контратакой в этот тик.</summary>
    [ViewVariables]
    public bool Countered;
}
