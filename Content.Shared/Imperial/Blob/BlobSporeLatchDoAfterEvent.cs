using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob;

/// <summary>Спора забирается на голову цели.</summary>
[Serializable, NetSerializable]
public sealed partial class BlobSporeLatchDoAfterEvent : SimpleDoAfterEvent;
