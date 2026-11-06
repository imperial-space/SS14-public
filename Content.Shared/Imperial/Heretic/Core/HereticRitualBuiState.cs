using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Core;

// ─── Ritual BUI messages / states ───────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class HereticRitualBuiState : BoundUserInterfaceState
{
    public List<HereticRitualNodeData> Rituals = new();
    public List<HereticOfferingNodeData> Offerings = new();
}
