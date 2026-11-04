using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Core;

[Serializable, NetSerializable]
public sealed class HereticKnowledgeNodeData
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Cost;
    public HereticPath Path;
    public bool IsResearched;
    public bool PrerequisitesMet;
    public SpriteSpecifier? Icon;
    public List<string> Prerequisites = new();
    public List<List<string>> PrerequisitesAny = new();
    public bool IsGift;
    public int ShopLevel;
    public List<string> ConflictsWith = new();
}
