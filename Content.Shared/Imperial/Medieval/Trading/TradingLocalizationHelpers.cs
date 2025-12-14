using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Trading;

public static class TradingLocalisationHelpers
{
    public static string GetLocalisedNameOrEntityName(GuildTradingItem listingData, IPrototypeManager prototypeManager)
    {
        return listingData.Name
               ?? (listingData.ProductEntity != null
                   ? prototypeManager.Index(listingData.ProductEntity.Value).Name
                   : string.Empty);
    }

    public static string GetLocalisedDescriptionOrEntityDescription(GuildTradingItem listingData, IPrototypeManager prototypeManager)
    {
        return listingData.Description
               ?? (listingData.ProductEntity != null
                   ? prototypeManager.Index(listingData.ProductEntity.Value).Description
                   : string.Empty);
    }
}
