using Content.Shared.Mind;
using Content.Shared.Store;

namespace Content.Server.Imperial.Contractor;

/// <summary>
/// uplink_item/contractor/can_be_bought(): товары контрактника доступны только после входа в программу контрактов.
/// </summary>
public sealed partial class ContractorHubCondition : ListingCondition
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

        return args.EntityManager.HasComponent<ContractorHubComponent>(mindId);
    }
}
