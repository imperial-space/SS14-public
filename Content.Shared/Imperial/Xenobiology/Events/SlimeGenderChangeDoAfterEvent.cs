using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>do_after зелья смены пола на разумном существе.</summary>
[Serializable, NetSerializable]
public sealed partial class SlimeGenderChangeDoAfterEvent : SimpleDoAfterEvent;
