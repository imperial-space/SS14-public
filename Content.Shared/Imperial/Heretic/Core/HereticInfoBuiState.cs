using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Core;

// ─── Info BUI messages / states ─────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class HereticInfoBuiState : BoundUserInterfaceState
{
    public int KnowledgePoints;
    public HereticPath CurrentPath;
    public int PassiveLevel;
    public int SacrificeCount;
    public int RequiredSacrifices;
    public int RequiredKnowledge;
    public List<HereticPathData> Paths = new();
    public List<HereticKnowledgeNodeData> Nodes = new();
    public int CurrentShopLevel;
    public List<HereticGiftGroup> PendingGiftGroups = new();
}
