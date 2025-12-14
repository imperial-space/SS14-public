using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Boss;

[Serializable, NetSerializable]
public sealed partial class BossRunesChargingDoAfterEvent : SimpleDoAfterEvent;
