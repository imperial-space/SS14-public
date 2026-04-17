using System;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob;

[Serializable, NetSerializable]
public enum BlobVisuals : byte
{
    Color,
}

public enum BlobVisualLayers : byte
{
    Base,
    Overlay,
    Glow,
}