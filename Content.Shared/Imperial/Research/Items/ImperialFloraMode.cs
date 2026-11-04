using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

[Serializable, NetSerializable]
public enum ImperialFloraMode : byte
{
    /// <summary>flora/yield: повышает урожайность.</summary>
    Yield,
    /// <summary>flora/mut: вызывает мутации.</summary>
    Mutate,
}
