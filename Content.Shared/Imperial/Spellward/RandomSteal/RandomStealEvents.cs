using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.RandomSteal.Events;

[NetSerializable, Serializable]
public sealed partial class StealDoAfterArgs : SimpleDoAfterEvent { }
