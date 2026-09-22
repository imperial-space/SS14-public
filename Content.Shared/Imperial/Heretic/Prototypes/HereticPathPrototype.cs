using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Prototypes;

[Prototype]
public sealed partial class HereticPathPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public HereticPath Path;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public string Complexity = string.Empty;

    [DataField]
    public string PassiveName = string.Empty;

    [DataField]
    public string PassiveDescription = string.Empty;

    [DataField]
    public string Pros = string.Empty;

    [DataField]
    public string Cons = string.Empty;

    [DataField]
    public string Level1Description = string.Empty;

    [DataField]
    public string Level2Description = string.Empty;

    [DataField]
    public string Level3Description = string.Empty;

    [DataField]
    public SpriteSpecifier? Icon;

    [DataField]
    public string PathKnowledgeId = string.Empty;

    [DataField]
    public string Tips = string.Empty;
}
