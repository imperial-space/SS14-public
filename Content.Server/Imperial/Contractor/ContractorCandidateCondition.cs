using Content.Shared.Mind;
using Content.Shared.Imperial.Contractor.Components;
using Content.Shared.Roles;
using Content.Shared.Store;

namespace Content.Server.Imperial.Contractor;

/// <summary>
/// Restricts contractor conversion kit purchases to traitors that received the contractor offer.
/// </summary>
public sealed partial class ContractorCandidateCondition : ListingCondition
{
    public override bool Condition(ListingConditionArgs args)
    {
        var mindId = args.Buyer;
        if (!args.EntityManager.HasComponent<MindComponent>(mindId))
        {
            var minds = args.EntityManager.System<SharedMindSystem>();
            if (!minds.TryGetMind(args.Buyer, out mindId, out _))
                return false;
        }

        var roles = args.EntityManager.System<SharedRoleSystem>();
        return roles.MindHasRole<ContractorCandidateRoleComponent>(mindId);
    }
}