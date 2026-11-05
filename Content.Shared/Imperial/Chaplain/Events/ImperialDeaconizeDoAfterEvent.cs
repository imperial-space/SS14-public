using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Chaplain;

/// <summary>
/// Одно воззвание обряда посвящения в дьяконы.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ImperialDeaconizeDoAfterEvent : SimpleDoAfterEvent;
