using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed partial class CollectAllItemsCondition : AchievementCondition
{
    [DataField(required: true)]
    public Dictionary<string, int> ItemPrototypes = new();

    public override FormattedMessage GetDescription(IPrototypeManager protoManager)
    {
        var msg = new FormattedMessage();
        msg.PushColor(Color.FromHex("#9e8c78"));

        var itemNames = ItemPrototypes
            .Select(kvp =>
            {
                var name = protoManager.TryIndex<EntityPrototype>(kvp.Key, out var ep) ? ep.Name : kvp.Key;
                return kvp.Value > 1 ? $"{name} ×{kvp.Value}" : name;
            })
            .ToList();

        var targets = string.Join(", ", itemNames);
        msg.AddText(Loc.GetString("achievement-condition-collect-all", ("targets", targets)));

        msg.Pop();
        AppendRequirements(msg, protoManager);
        return msg;
    }

    public override bool Check(
        EntityUid player,
        IEntityManager entManager,
        IPrototypeManager protoManager,
        object? context,
        Dictionary<string, int> progress)
    {
        if (!CheckFilters(player, entManager, protoManager))
            return false;

        foreach (var (proto, required) in ItemPrototypes)
        {
            if (progress.GetValueOrDefault($"{ProgressKey}:{proto}", 0) < required)
                return false;
        }

        return true;
    }

    public override int GetTargetProgress() => ItemPrototypes.Values.Sum();

    public override int GetCurrentProgress(Dictionary<string, int>? progress)
    {
        if (progress == null || string.IsNullOrEmpty(ProgressKey))
            return 0;

        int total = 0;
        foreach (var (proto, required) in ItemPrototypes)
        {
            var key = $"{ProgressKey}:{proto}";
            var amount = progress.GetValueOrDefault(key, 0);

            total += Math.Min(amount, required); 
        }

        return total;
    }
}
