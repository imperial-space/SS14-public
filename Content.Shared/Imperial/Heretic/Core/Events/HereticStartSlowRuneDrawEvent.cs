using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Core;

// Raised on the heretic when Mansus Grasp draws a rune (LKM null-target or Z key)
public sealed class HereticStartSlowRuneDrawEvent : EntityEventArgs { }
