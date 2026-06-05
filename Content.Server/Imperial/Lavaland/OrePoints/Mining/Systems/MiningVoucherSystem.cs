using Content.Server.Imperial.Lavaland.OrePoints;
using Content.Server.Popups;
using Content.Shared.Imperial.Lavaland.OrePoints.MiningVoucher;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

public sealed class MiningVoucherSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    // Kit definitions: each kit is a list of prototype IDs to spawn
    private static readonly string[][] Kits =
    {
        // Kit 0: Survival — mining webbing + rescue capsule
        new[] { "ClothingBeltMiningWebbing", "RescueCapsule" },
        // Kit 1: Fulton — fulton beacon + fulton
        new[] { "FultonBeacon", "Fulton" },
        // Kit 2: Plasma — ore bag + plasma cutter
        new[] { "OreBagMining", "WeaponPlasmaCutter" },
        // Kit 3: Crusher — kinetic crusher + fire extinguisher
        new[] { "WeaponKineticCrusher", "FireExtinguisherMini" },
        // Kit 4: Backpack — ore bag + mining flashlight + medipen + knife
        new[] { "OreBag", "FlashlightSecliteMining", "MiningMedipen", "SurvivalKnife" },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MiningVoucherComponent, AfterInteractEvent>(OnAfterInteract);

        Subs.BuiEvents<MiningVoucherComponent>(MiningVoucherUiKey.Key, subs =>
        {
            subs.Event<MiningVoucherSelectKitMessage>(OnSelectKit);
        });
    }

    private void OnAfterInteract(EntityUid uid, MiningVoucherComponent comp, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        // Only works on the ore points vending machine
        if (!HasComp<OrePointsVendingComponent>(target))
            return;

        args.Handled = true;
        _ui.OpenUi(uid, MiningVoucherUiKey.Key, args.User);
    }

    private void OnSelectKit(EntityUid uid, MiningVoucherComponent comp, MiningVoucherSelectKitMessage args)
    {
        if (args.KitIndex < 0 || args.KitIndex >= Kits.Length)
            return;

        var items = Kits[args.KitIndex];
        var coords = Transform(uid).Coordinates;

        foreach (var itemId in items)
        {
            Spawn(itemId, coords);
        }

        _popup.PopupEntity(Loc.GetString("mining-voucher-redeemed"), uid, args.Actor, PopupType.Medium);

        QueueDel(uid);
    }
}
