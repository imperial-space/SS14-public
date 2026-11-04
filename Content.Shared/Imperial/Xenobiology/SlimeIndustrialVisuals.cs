using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>Промышленный экстракт работает (industrial / industrial_still).</summary>
[Serializable, NetSerializable]
public enum SlimeIndustrialVisuals : byte
{
    Working,
}
