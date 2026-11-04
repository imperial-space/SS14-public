using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Core;

[Serializable, NetSerializable]
public sealed class HereticTargetSelectedMessage : BoundUserInterfaceMessage { }
