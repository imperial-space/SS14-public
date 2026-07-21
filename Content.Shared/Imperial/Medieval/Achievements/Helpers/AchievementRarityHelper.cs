using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Achievements;

public static class AchievementRarityHelper
{
    public static AchievementRarityPrototype Resolve(
        AchievementPrototype achievement,
        float globalPercent,
        IPrototypeManager proto)
    {
        if (achievement.Rarity.HasValue
            && proto.TryIndex<AchievementRarityPrototype>(achievement.Rarity.Value, out var explicit_))
        {
            return explicit_;
        }

        return FromPercent(globalPercent, proto);
    }

    public static AchievementRarityPrototype FromPercent(float percent, IPrototypeManager proto)
    {
        AchievementRarityPrototype? best = null;

        foreach (var rarity in proto.EnumeratePrototypes<AchievementRarityPrototype>())
        {
            if (percent >= rarity.MinPercent)
            {
                if (best == null || rarity.MinPercent > best.MinPercent)
                    best = rarity;
            }
        }

        return best ??
            proto.EnumeratePrototypes<AchievementRarityPrototype>().OrderBy(r => r.MinPercent).First();
    }
}
