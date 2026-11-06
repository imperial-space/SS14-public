using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.Flora;

/// <summary>Сбор флоры лаваленда (harvest do_after).</summary>
[Serializable, NetSerializable]
public sealed partial class LavalandFloraHarvestDoAfterEvent : SimpleDoAfterEvent;
