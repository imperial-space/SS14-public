using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

[Serializable, NetSerializable]
public sealed partial class ImperialSealDoAfterEvent : SimpleDoAfterEvent;
