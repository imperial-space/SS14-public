using Content.Server.Imperial.Lavaland.OrePoints;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Lavaland.OrePoints.MiningVoucher;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>
/// element/voucher_redeemer: ваучер на шахтёрском автомате открывает радиальное меню наборов,
/// выбранный набор появляется у автомата, ваучер исчезает.
/// </summary>
public sealed class MiningVoucherSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MiningVoucherComponent, AfterInteractEvent>(OnAfterInteract);

        Subs.BuiEvents<MiningVoucherComponent>(MiningVoucherUiKey.Key, subs =>
        {
            subs.Event<MiningVoucherSelectKitMessage>(OnSelectKit);
        });
    }

    private void OnAfterInteract(Entity<MiningVoucherComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<OrePointsVendingComponent>(target))
            return;

        args.Handled = true;
        ent.Comp.Redeemer = target;
        _ui.OpenUi(ent.Owner, MiningVoucherUiKey.Key, args.User);
    }

    private void OnSelectKit(Entity<MiningVoucherComponent> ent, ref MiningVoucherSelectKitMessage args)
    {
        var user = args.Actor;
        _ui.CloseUi(ent.Owner, MiningVoucherUiKey.Key, user);

        if (args.KitIndex < 0 || args.KitIndex >= MiningVoucherKits.All.Length)
            return;

        // check_menu(): ваучер всё ещё в руке, автомат рядом.
        if (ent.Comp.Redeemer is not { } redeemer || TerminatingOrDeleted(redeemer) ||
            !_hands.IsHolding(user, ent.Owner) || !_interaction.InRangeUnobstructed(user, redeemer))
            return;

        var kit = MiningVoucherKits.All[args.KitIndex];

        // spawn_set(source.drop_location()): на клетке автомата, а не в координатах ваучера,
        // которые привязаны к держащему его игроку.
        var coords = _transform.GetMapCoordinates(redeemer);
        foreach (var item in kit.Items)
        {
            Spawn(item, coords);
        }

        _popup.PopupEntity(Loc.GetString("mining-voucher-redeemed", ("kit", Loc.GetString(kit.Name))), redeemer, user, PopupType.Medium);
        QueueDel(ent);
    }
}
