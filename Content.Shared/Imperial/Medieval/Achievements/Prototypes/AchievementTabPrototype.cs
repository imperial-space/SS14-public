using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

[Prototype]
public sealed partial class AchievementTabPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = default!;

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;

    [DataField]
    public int Priority = 0;

    [DataField]
    public AchievementTabLayout Layout = AchievementTabLayout.Tree;
}

public enum AchievementTabLayout : byte
{
    /// <summary>
    /// Layered left-to-right tree (Sugiyama)
    /// </summary>
    Tree = 0,

    /// <summary>
    /// Radial web
    /// </summary>
    Radial = 1,
}
