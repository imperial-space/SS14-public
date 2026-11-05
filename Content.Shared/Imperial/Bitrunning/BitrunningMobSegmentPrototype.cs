using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>/datum/modular_mob_segment: набор мобов, из которого выбираются существа для метки.</summary>
[Prototype]
public sealed partial class BitrunningMobSegmentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<string> Mobs = new();

    /// <summary>exact: всех мобов из списка; иначе случайные.</summary>
    [DataField]
    public bool Exact;

    /// <summary>max: сколько мобов выбрать, если не exact.</summary>
    [DataField]
    public int Max = 4;

    /// <summary>probability: шанс появления каждого моба.</summary>
    [DataField]
    public float Probability = 1f;
}
