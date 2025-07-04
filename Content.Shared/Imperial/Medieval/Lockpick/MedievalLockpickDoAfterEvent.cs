using Robust.Shared.GameStates;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.MedievalLockpick.Components;

[Serializable, NetSerializable]
public sealed partial class MedievalLockpickDoAfterEvent : SimpleDoAfterEvent;
