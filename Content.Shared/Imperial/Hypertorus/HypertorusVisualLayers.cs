using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Hypertorus;

[Serializable, NetSerializable]
public enum HypertorusVisualLayers : byte
{
    Base,
    Active,
    Crack,
}
