using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology;

[Serializable, NetSerializable]
public enum SlimeVisualLayers : byte
{
    Base,
    Mood,
}
