using Content.Shared.Item;
using Content.Shared.Popups;

namespace Content.Shared.Imperial.Xenomorph;

public sealed class XenoLatchedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoLatchedComponent, Content.Shared.Inventory.Events.BeingUnequippedAttemptEvent>(OnUnequipAttempt);
    }

    private void OnUnequipAttempt(Entity<XenoLatchedComponent> ent, ref Content.Shared.Inventory.Events.BeingUnequippedAttemptEvent args)
    {
        if (args.User != args.UnEquipTarget)
            return;

        args.Cancel();
        args.Reason = "xeno-hugger-latched";
    }
}
