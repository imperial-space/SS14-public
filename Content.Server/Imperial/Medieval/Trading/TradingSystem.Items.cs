using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Trading;

public sealed partial class TradingSystem
{
    public void RefreshAllGuilds(TradingComponent component)
    {
        component.Guilds = Guilds;
    }

    public bool TryAddListing(StoreComponent component, ListingPrototype listing)
    {
        return component.FullListingsCatalog.Add(new ListingDataWithCostModifiers(listing));
    }

    public IEnumerable<Guild> GetAvailableGuilds(EntityUid buyer, EntityUid store, TradingComponent component)
    {
        return GetAvailableGuilds(buyer, component.GuildTypes, component.Guilds, store);
    }

    public IEnumerable<Guild> GetAvailableGuilds(
        EntityUid buyer,
        IReadOnlyCollection<ProtoId<GuildTypePrototype>>? guildTypes,
        IReadOnlyCollection<Guild>? guilds,
        EntityUid? storeEntity = null
    )
    {
        guilds ??= Guilds;
        var netBuyer = GetNetEntity(buyer);

        foreach (var guild in guilds)
        {
            if(guildTypes != null && !guildTypes.Contains(guild.TypePrototype))
                continue;

            var modifiedGuild = guild.Clone();
            var modifiedItems = guild.Items
                .Where(i => i.CanBuy(netBuyer, guild, _entityManager).Item1)
                .Select(x =>
                {
                    var clone = x with { };
                    clone.ChangedCost = TradingHelpers.PriceWithReputation(guild, clone, netBuyer);
                    return clone;
                })
                .ToList();

            modifiedGuild.Items = modifiedItems;

            Dictionary<GuildTradingItem, string> unavailableItems = new();
            foreach (var item in guild.Items)
            {
                var (canBuy, reason) = item.CanBuy(netBuyer, guild, _entityManager);
                if(canBuy)
                    continue;
                if(reason == null)
                    continue;

                unavailableItems.Add(item, reason);
            }

            modifiedGuild.UnavailableItems = unavailableItems;

            yield return modifiedGuild;
        }
    }
}
