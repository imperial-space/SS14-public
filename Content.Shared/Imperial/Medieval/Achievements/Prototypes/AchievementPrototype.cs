using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

[Prototype]
public sealed partial class AchievementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string? Name;

    [DataField]
    public string? Description;

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;

    [DataField]
    public SoundSpecifier AchievementSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/achievement_unlocked.ogg");

    [DataField]
    public AchievementVisibility Visibility = AchievementVisibility.Visible;

    [DataField]
    public bool RoundOnly = false;

    [DataField]
    public List<ProtoId<AchievementPrototype>> Prerequisites = new();

    [DataField]
    public List<AchievementCondition> Conditions = new();

    /// <summary>
    /// Explicitly defined rarity. If null, it is determined automatically
    /// based on ownership percentage via <see cref="AchievementRarityPrototype"/>.
    /// </summary>
    [DataField]
    public ProtoId<AchievementRarityPrototype>? Rarity;

    [DataField]
    public int Priority = 0;

    [DataField]
    public ProtoId<AchievementTabPrototype>? Tab;
}

/// <summary>
/// Controls how an achievement and its descendants are displayed in the tree
/// </summary>
public enum AchievementVisibility : byte
{
    /// <summary>
    /// Default. The achievement and all descendants are always visible
    /// </summary>
    Visible = 0,

    /// <summary>
    /// The achievement is completely invisible until be unlocked.
    /// All descendants are also invisible until this achievement is unlocked
    /// </summary>
    Hidden = 1,

    /// <summary>
    /// The achievement itself is shown as available, but all descendants
    /// are invisible until this achievement is unlocked
    /// </summary>
    Blocking = 2,

    /// <summary>
    /// The achievement itself is shown as available. Descendants are shown
    /// as question marks their count and order are visible, but their
    /// content (name, description, icon) is hidden until this is unlocked
    /// </summary>
    BlockingRevealed = 3,
}
