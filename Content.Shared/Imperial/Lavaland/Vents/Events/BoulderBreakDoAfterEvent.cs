using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.Vents;

/// <summary>Удар киркой по валуну (manual_process).</summary>
[Serializable, NetSerializable]
public sealed partial class BoulderBreakDoAfterEvent : SimpleDoAfterEvent;
