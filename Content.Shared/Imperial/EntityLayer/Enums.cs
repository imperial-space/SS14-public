using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EntityLayer;


/// <summary>
/// If you want to add a new layer, just add it here. Values ​​should NOT be repeated. Use ONLY the bitwise shift operator.
/// </summary>
[Serializable, NetSerializable]
public enum EntityLayerGroups
{
    None = 0,
    PhaseSpace = 1 << 0,

    Overworld = 1 << 31,

    All = PhaseSpace,
    AllWithOverworld = All | Overworld
}
