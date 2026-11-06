using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Демон: пока пилоны стоят, варп слаб — он бьёт слабее и получает больше урона. После смерти возвращается
/// в варп (тело исчезает), розовые ужасы при этом делятся на синих, а гибель принца демонов решает войну.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CadiaDaemonComponent : Component
{
    [DataField]
    public CadiaChaosGod God;

    /// <summary>Во что распадается при смерти.</summary>
    [DataField]
    public EntProtoId? SplitInto;

    [DataField]
    public int SplitCount;

    /// <summary>Босс: его изгнание — победа Империума.</summary>
    [DataField]
    public bool Boss;

    /// <summary>Летает между этажами.</summary>
    [DataField]
    public bool Flyer;
}
