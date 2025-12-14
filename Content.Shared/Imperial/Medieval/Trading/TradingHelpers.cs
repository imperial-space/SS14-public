using Content.Shared.Imperial.Medieval.Trading.Prototypes;

namespace Content.Shared.Imperial.Medieval.Trading;

public static class TradingHelpers
{

    public static float DiscountWithReputation(Guild guild, NetEntity buyer)
    {
        var rep = Math.Clamp(guild.GetReputation(buyer), 0, 100);
        var discount = (rep / 100f) * 0.25f;
        return discount;
    }

    public static int PriceWithReputation(Guild guild, GuildTradingItem item, NetEntity buyer)
    {
        var discount = DiscountWithReputation(guild, buyer);
        var basePrice = Math.Max(item.ChangedCost, 0);
        return (int)MathF.Round(basePrice * (1 - discount), MidpointRounding.AwayFromZero);
    }
}
