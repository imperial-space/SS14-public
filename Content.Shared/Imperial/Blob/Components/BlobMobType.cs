using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob.Components;

[Serializable, NetSerializable]
public enum BlobMobType : byte
{
    Spore,
    WeakSpore,
    IndependentSpore,
    Zombie,
    Blobbernaut,
}
