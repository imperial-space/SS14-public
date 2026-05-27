using Content.Server.Access.Systems;
using Content.Server.Lathe;
using Content.Server.Popups;
using Content.Server.VendingMachines;
using Content.Shared.Imperial.Lavaland.OrePoints;
using Content.Shared.Access.Components;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.VendingMachines;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Lavaland.OrePoints;

public sealed class OrePointsSystem : EntitySystem
{
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly LatheSystem _lathe = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly VendingMachineSystem _vending = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OreProcessorPointsComponent, MaterialEntityInsertedEvent>(OnMaterialInserted);
        SubscribeLocalEvent<OreProcessorPointsComponent, InteractUsingEvent>(OnInteractUsingIdCard);
        SubscribeLocalEvent<OreProcessorPointsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetOreProcessorVerbs);

        SubscribeLocalEvent<OrePointsVendingComponent, BoundUIOpenedEvent>(OnOreShopUiOpened);

        Subs.BuiEvents<OrePointsVendingComponent>(OrePointsShopUiKey.Key, subs =>
        {
            subs.Event<OrePointsShopBuyMessage>(OnOreShopBuyMessage);
        });
    }

    private void OnMaterialInserted(EntityUid uid, OreProcessorPointsComponent component, ref MaterialEntityInsertedEvent args)
    {
        var inserted = args.MaterialComp.Owner;

        if (!TryComp<PhysicalCompositionComponent>(inserted, out var composition))
            return;

        var stackCount = TryComp<StackComponent>(inserted, out var stack)
            ? stack.Count
            : 1;

        var addedPoints = 0;
        foreach (var (materialId, volume) in composition.MaterialComposition)
        {
            if (!component.MaterialPointValues.TryGetValue(materialId, out var pointsPerOre))
                continue;

            var oreUnits = (volume * stackCount) / component.MaterialUnitVolume;
            if (oreUnits <= 0)
                continue;

            addedPoints += oreUnits * pointsPerOre;
        }

        if (addedPoints <= 0)
            return;

        component.StoredPoints += addedPoints;
        Dirty(uid, component);

        if (TryComp<LatheComponent>(uid, out var lathe))
            _lathe.UpdateUserInterfaceState(uid, lathe);
    }

    private void OnInteractUsingIdCard(EntityUid uid, OreProcessorPointsComponent component, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_idCard.TryGetIdCard(args.Used, out Entity<IdCardComponent> idCard))
            return;

        args.Handled = true;

        if (component.StoredPoints <= 0)
        {
            _popup.PopupEntity(Loc.GetString("ore-points-nothing-to-extract"), uid, args.User, PopupType.Medium);
            return;
        }

        var account = EnsureComp<OrePointsAccountComponent>(idCard.Owner);
        var extracted = component.StoredPoints;
        account.Points += extracted;
        component.StoredPoints = 0;

        Dirty(idCard.Owner, account);
        Dirty(uid, component);

        if (TryComp<LatheComponent>(uid, out var lathe))
            _lathe.UpdateUserInterfaceState(uid, lathe);

        _popup.PopupEntity(
            Loc.GetString("ore-points-extracted", ("points", extracted)),
            uid,
            args.User,
            PopupType.Medium);
    }

    private void OnGetOreProcessorVerbs(EntityUid uid, OreProcessorPointsComponent component, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract)
            return;

        var user = args.User;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("ore-points-verb-check-processor", ("points", component.StoredPoints)),
            Act = () =>
            {
                _popup.PopupEntity(
                    Loc.GetString("ore-points-processor-balance", ("points", component.StoredPoints)),
                    uid,
                    user,
                    PopupType.Medium);
            }
        };

        args.Verbs.Add(verb);
    }

    private void OnOreShopUiOpened(EntityUid uid, OrePointsVendingComponent component, ref BoundUIOpenedEvent args)
    {
        UpdateOreShopUi(uid, component, args.Actor);
    }

    private void OnOreShopBuyMessage(EntityUid uid, OrePointsVendingComponent component, OrePointsShopBuyMessage args)
    {
        if (!TryComp<VendingMachineComponent>(uid, out var vending))
            return;

        if (!component.ItemCosts.TryGetValue(args.ItemId, out var cost))
        {
            _vending.Deny((uid, (VendingMachineComponent?) vending), args.Actor);
            UpdateOreShopUi(uid, component, args.Actor);
            return;
        }

        if (!_vending.IsAuthorized(uid, args.Actor, vending))
        {
            UpdateOreShopUi(uid, component, args.Actor);
            return;
        }

        if (!_idCard.TryFindIdCard(args.Actor, out Entity<IdCardComponent> idCard))
        {
            _popup.PopupEntity(Loc.GetString("ore-points-id-required"), uid, args.Actor, PopupType.MediumCaution);
            _vending.Deny((uid, (VendingMachineComponent?) vending), args.Actor);
            UpdateOreShopUi(uid, component, args.Actor);
            return;
        }

        var account = EnsureComp<OrePointsAccountComponent>(idCard.Owner);
        if (account.Points < cost)
        {
            _popup.PopupEntity(
                Loc.GetString("ore-points-not-enough", ("need", cost), ("have", account.Points)),
                uid,
                args.Actor,
                PopupType.MediumCaution);

            _vending.Deny((uid, (VendingMachineComponent?) vending), args.Actor);
            UpdateOreShopUi(uid, component, args.Actor);
            return;
        }

        if (!_prototypes.TryIndex<EntityPrototype>(args.ItemId, out _))
        {
            _popup.PopupEntity(Loc.GetString("ore-points-vending-invalid-item"), uid, args.Actor, PopupType.MediumCaution);
            _vending.Deny((uid, (VendingMachineComponent?) vending), args.Actor);
            UpdateOreShopUi(uid, component, args.Actor);
            return;
        }

        account.Points -= cost;

        Dirty(idCard.Owner, account);

        Spawn(args.ItemId, Transform(uid).Coordinates);

        if (TryComp<VendingMachineComponent>(uid, out var vendSound))
            _audio.PlayPvs(vendSound.SoundVend, uid);

        _popup.PopupEntity(
            Loc.GetString("ore-points-bought", ("cost", cost), ("left", account.Points)),
            uid,
            args.Actor,
            PopupType.Medium);

        UpdateOreShopUi(uid, component, args.Actor);
    }

    private void UpdateOreShopUi(EntityUid uid, OrePointsVendingComponent component, EntityUid? actor = null)
    {
        var entries = new List<OrePointsShopEntryState>();

        foreach (var (itemId, cost) in component.ItemCosts)
        {
            if (!_prototypes.TryIndex<EntityPrototype>(itemId, out var proto))
                continue;

            entries.Add(new OrePointsShopEntryState(itemId, proto.Name, cost));
        }

        var balance = 0;
        if (actor is { } actorUid && _idCard.TryFindIdCard(actorUid, out Entity<IdCardComponent> idCard)
            && TryComp<OrePointsAccountComponent>(idCard.Owner, out var account))
        {
            balance = account.Points;
        }

        _ui.SetUiState(uid, OrePointsShopUiKey.Key, new OrePointsShopUiState(entries, balance));
    }
}
