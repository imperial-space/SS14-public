using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.Vents;

/// <summary>Сканирование рудной жилы (4 с).</summary>
[Serializable, NetSerializable]
public sealed partial class OreVentScanDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Удар киркой по валуну (manual_process).</summary>
[Serializable, NetSerializable]
public sealed partial class BoulderBreakDoAfterEvent : SimpleDoAfterEvent;
