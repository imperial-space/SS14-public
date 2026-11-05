using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>do_after зелий любви и мира.</summary>
[Serializable, NetSerializable]
public sealed partial class SlimeCrossPotionDoAfterEvent : SimpleDoAfterEvent;
