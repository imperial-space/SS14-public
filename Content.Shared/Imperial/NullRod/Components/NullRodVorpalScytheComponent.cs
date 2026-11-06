using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Порочная коса: насыщается ударами и «похоронным звоном», насыщенную можно убрать без вреда,
/// усиленная бьёт вдвое сильнее.
/// </summary>
[RegisterComponent]
public sealed partial class NullRodVorpalScytheComponent : Component
{
    [ViewVariables]
    public ScytheEmpowerment Empowerment = ScytheEmpowerment.Weak;

    [ViewVariables]
    public TimeSpan EmpowermentEnd;

    /// <summary>4 минуты делятся на уровень насыщения: насыщена 4 минуты, усилена 2.</summary>
    [DataField]
    public TimeSpan EmpowermentDuration = TimeSpan.FromMinutes(4);

    [DataField]
    public float EmpoweredMultiplier = 2f;

    /// <summary>Длительность похоронного звона до модификаторов.</summary>
    [DataField]
    public TimeSpan DeathKnellTime = TimeSpan.FromSeconds(15);
}
