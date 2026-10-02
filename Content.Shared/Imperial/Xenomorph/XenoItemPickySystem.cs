using Content.Shared.Item;
using Content.Shared.Popups;

namespace Content.Shared.Imperial.Xenomorph;

/// <summary>
/// component/itempicky у ксеноморфов: когтями можно держать только xeno_allowed_items (<see cref="XenoHoldableComponent"/>).
/// </summary>
public sealed class XenoItemPickySystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenomorphComponent, PickupAttemptEvent>(OnPickupAttempt);
    }

    private void OnPickupAttempt(Entity<XenomorphComponent> ent, ref PickupAttemptEvent args)
    {
        if (args.Cancelled || HasComp<XenoHoldableComponent>(args.Item))
            return;

        args.Cancel();
        if (args.ShowPopup)
            _popup.PopupClient(Loc.GetString("xeno-claws-too-clumsy", ("item", args.Item)), ent, ent);
    }
}

/// <summary>
/// facehugger/can_mob_unequip: присосавшегося живого лицехвата не может снять сам носитель
/// («Get help or wait for it to let go!»), а другие — могут.
/// </summary>
[RegisterComponent, Robust.Shared.GameStates.NetworkedComponent]
public sealed partial class XenoLatchedComponent : Component;

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
