using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Hypertorus;

/// <summary>Заварка треснувшего порта сваркой (10 секунд).</summary>
[Serializable, NetSerializable]
public sealed partial class HypertorusRepairDoAfterEvent : SimpleDoAfterEvent;
