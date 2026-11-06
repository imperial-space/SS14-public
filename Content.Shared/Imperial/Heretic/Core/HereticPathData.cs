using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Core;

// ─── Path data / messages ────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class HereticPathData
{
    public string Id = string.Empty;
    public HereticPath Path;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public string Complexity = string.Empty;
    public string PassiveName = string.Empty;
    public string PassiveDescription = string.Empty;
    public string Pros = string.Empty;
    public string Cons = string.Empty;
    public string Level1Description = string.Empty;
    public string Level2Description = string.Empty;
    public string Level3Description = string.Empty;
    public SpriteSpecifier? Icon;
    public string PathKnowledgeId = string.Empty;
    public string Tips = string.Empty;
}
