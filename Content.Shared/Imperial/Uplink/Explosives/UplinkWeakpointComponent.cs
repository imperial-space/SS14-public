using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Explosives;

/// <summary>Слабое место в полу (effect/weakpoint): взрыв рядом раскалывает пол цепочкой трещин.</summary>
[RegisterComponent]
public sealed partial class UplinkWeakpointComponent : Component
{
    [DataField]
    public int CrackLength = 8;

    [DataField]
    public int NewWeakpoints = 2;

    [DataField]
    public bool SpawnsChildren = true;

    [DataField]
    public float WeakpointDamagedChance = 0.5f;
}
