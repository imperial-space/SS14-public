using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>do_after взятия пробы тампоном или биопсией (3 секунды).</summary>
[Serializable, NetSerializable]
public sealed partial class VatSwabDoAfterEvent : SimpleDoAfterEvent;
