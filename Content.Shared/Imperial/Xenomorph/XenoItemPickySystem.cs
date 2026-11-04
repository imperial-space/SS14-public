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
