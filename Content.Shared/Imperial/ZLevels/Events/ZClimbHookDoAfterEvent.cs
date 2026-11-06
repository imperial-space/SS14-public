using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

[Serializable, NetSerializable]
public sealed partial class ZClimbHookDoAfterEvent : SimpleDoAfterEvent;
