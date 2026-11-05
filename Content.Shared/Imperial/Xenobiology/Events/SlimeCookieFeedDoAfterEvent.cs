using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>do_after кормления печеньем слайма другого существа.</summary>
[Serializable, NetSerializable]
public sealed partial class SlimeCookieFeedDoAfterEvent : SimpleDoAfterEvent;
