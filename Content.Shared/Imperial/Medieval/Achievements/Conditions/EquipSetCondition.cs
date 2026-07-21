using System.Linq;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed partial class EquipSetCondition : AchievementCondition
{
    [DataField(required: true)]
    public Dictionary<string, List<string>> Sets = new();

    public override FormattedMessage GetDescription(IPrototypeManager protoManager)
    {
        var msg = new FormattedMessage();

        if (Sets.Count == 0)
            return msg;

        msg.PushColor(Color.FromHex("#9e8c78"));
        msg.AddText(Loc.GetString("achievement-condition-equip-set-header"));

        var first = true;
        foreach (var items in Sets.Values)
        {
            if (!first)
            {
                msg.AddText("\n");
                msg.PushColor(Color.FromHex("#8b6914"));
                msg.AddText(Loc.GetString("achievement-condition-equip-set-or").ToUpper());
                msg.Pop();
            }

            foreach (var id in items)
            {
                var name = protoManager.TryIndex<EntityPrototype>(id, out var ep) ? ep.Name : id;
                msg.AddText($"\n {name}");
            }

            first = false;
        }

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

        var equipped = GetEquippedIds(player, entManager);

        foreach (var items in Sets.Values)
        {
            if (items.Count == 0)
                continue;

            if (items.All(id => equipped.Contains(id)))
                return true;
        }

        return false;
    }

    public override bool TryUpdateProgress(
        EntityUid player,
        IEntityManager entManager,
        IPrototypeManager protoManager,
        object? context,
        Dictionary<string, int> progress)
    {
        if (!CheckFilters(player, entManager, protoManager))
            return false;

        return true;
    }

    private static HashSet<string> GetEquippedIds(EntityUid player, IEntityManager entManager)
    {
        var ids = new HashSet<string>();
        var invSystem = entManager.System<InventorySystem>();

        if (!invSystem.TryGetContainerSlotEnumerator(player, out var enumerator))
            return ids;

        while (enumerator.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } item)
                continue;

            if (entManager.TryGetComponent<MetaDataComponent>(item, out var meta) &&
                meta.EntityPrototype != null)
            {
                ids.Add(meta.EntityPrototype.ID);
            }
        }

        return ids;
    }
}
