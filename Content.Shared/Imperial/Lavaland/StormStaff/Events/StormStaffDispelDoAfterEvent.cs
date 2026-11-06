using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.StormStaff;

/// <summary>Посох бурь поднят к небу (3 с) — буря рассеивается.</summary>
[Serializable, NetSerializable]
public sealed partial class StormStaffDispelDoAfterEvent : SimpleDoAfterEvent;
