using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.Prototypes;

[DataDefinition]
public sealed partial class HereticRitualIngredient
{
    /// <summary>Entity prototype ID that must be present near the rune. Ignored if <see cref="Tag"/> is set.</summary>
    [DataField]
    public EntProtoId EntityId;

    /// <summary>If set, matches any entity carrying this tag instead of a specific prototype.</summary>
    [DataField]
    public ProtoId<TagPrototype>? Tag;

    /// <summary>If true, matches any entity that has a MobStateComponent (i.e. any living creature).</summary>
    [DataField]
    public bool HasMobState;

    /// <summary>If true, matches any living entity that is currently on fire.</summary>
    [DataField]
    public bool IsBurning;

    /// <summary>If true, matches only entities with MobStateComponent that are dead or in critical state.</summary>
    [DataField]
    public bool IsDeadOrCritical;

    /// <summary>If true, additionally requires HumanoidAppearanceComponent (any playable race).</summary>
    [DataField]
    public bool RequireHumanoid;

    [DataField]
    public int Amount = 1;
}
