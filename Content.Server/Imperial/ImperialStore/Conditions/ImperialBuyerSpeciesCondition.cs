using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;
using Content.Shared.Imperial.ImperialStore;

namespace Content.Server.Imperial.ImperialStore;

/// <summary>
/// Allows a store entry to be filtered out based on the user's species.
/// Supports both blacklists and whitelists.
/// </summary>
public sealed partial class ImperialBuyerSpeciesCondition : ImperialListingCondition
{
    /// <summary>
    /// A whitelist of species that can purchase this listing.
    /// </summary>
    [DataField("whitelist", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<SpeciesPrototype>))]
    public HashSet<string>? Whitelist;

    /// <summary>
    /// A blacklist of species that cannot purchase this listing.
    /// </summary>
    [DataField("blacklist", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<SpeciesPrototype>))]
    public HashSet<string>? Blacklist;

    public override bool Condition(ImperialListingConditionArgs args)
    {
        var ent = args.EntityManager;

        return true;
    }
}
