using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MartialArts;

/// <summary>mushpunch/charge_up_attack().</summary>
[Serializable, NetSerializable]
public sealed partial class MushroomPunchDoAfterEvent : SimpleDoAfterEvent;
