using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed partial class CollectAnyItemsCondition : AchievementCondition
{
    [DataField(required: true)]
    public List<string> ItemPrototypes = new();

    public override FormattedMessage GetDescription(IPrototypeManager protoManager)
    {
        var msg = new FormattedMessage();
        msg.PushColor(Color.FromHex("#9e8c78"));

        var itemNames = ItemPrototypes
            .Select(id => protoManager.TryIndex<EntityPrototype>(id, out var ep) ? ep.Name : id)
            .ToList();

        var targets = string.Join(", ", itemNames);
        msg.AddText(Loc.GetString("achievement-condition-collect-any-n",
            ("count", RequiredCount), ("targets", targets)));

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

        return progress.GetValueOrDefault(ProgressKey, 0) >= RequiredCount;
    }
}
