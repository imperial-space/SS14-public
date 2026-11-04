using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Core;

// ─── Target BUI state ────────────────────────────────────────────────────────

/// <summary>
/// Snapshot of one named target sent to the client when the target window opens.
/// </summary>
[Serializable, NetSerializable]
public sealed class HereticTargetData
{
    public NetEntity Entity;
    public string Name = string.Empty;
    /// <summary>True if the mob was already Dead at the moment the BUI state was built.</summary>
    public bool IsDead;
}
