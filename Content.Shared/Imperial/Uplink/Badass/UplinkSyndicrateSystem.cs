using Content.Shared.Popups;
using Content.Shared.Storage.Components;

namespace Content.Shared.Imperial.Uplink.Badass;

/// <summary>syndicrate/before_open(): объединённый ящик не открывается, пока его не отперли ключом.</summary>
public sealed class UplinkSyndicrateSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UplinkSurplusCrateComponent, StorageOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(Entity<UplinkSurplusCrateComponent> ent, ref StorageOpenAttemptEvent args)
    {
        if (!ent.Comp.United || ent.Comp.Unlocked)
            return;

        args.Cancelled = true;
        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("uplink-syndicrate-locked"), ent, args.User);
    }
}
