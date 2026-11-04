using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Core;

// ─── FleshWeave BUI state / messages ────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class HereticFleshWeaveOrganData
{
    public NetEntity Organ;
    public string Name = string.Empty;
}
