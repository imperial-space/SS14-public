using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Blob.Components;

[Serializable, NetSerializable]
public enum BlobStructureType : byte
{
    Normal,
    Strong,
    Reflective,
    Core,
    Node,
    Factory,
    Resource,
}
