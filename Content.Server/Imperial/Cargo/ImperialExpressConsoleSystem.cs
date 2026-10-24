using System.Linq;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chat;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.Cargo;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Server.Chat.Systems;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cargo;

/// <summary>
/// Экспресс-консоль снабжения TG (code/modules/cargo/expressconsole.dm) и маяк капсул (supplypod_beacon.dm):
/// заказ доставляется капсулой сразу, в грузовой отсек или к маяку.
/// </summary>
public sealed class ImperialExpressConsoleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly ImperialSupplyPodSystem _pods = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    private static readonly ProtoId<CargoAccountPrototype> CargoAccount = "Cargo";
    private static readonly EntProtoId BeaconProto = "ImperialSupplyPodBeacon";
    private static readonly ProtoId<CargoProductPrototype> EmptyPodPack = "ImperialCargoMiscEmpty";

    /// <summary>EXPRESS_EMAG_DISCOUNT.</summary>
    private const float EmagDiscount = 0.72f;

    /// <summary>BEACON_COST.</summary>
    private const int BeaconCost = 500;

    /// <summary>MAX_EMAG_ROCKETS.</summary>
    private const int MaxEmagRockets = 5;

    private static readonly TimeSpan BeaconPrintCooldown = TimeSpan.FromSeconds(10);

    private static readonly SoundSpecifier LinkSound = new SoundPathSpecifier("/Audio/Imperial/Cargo/twobeep.ogg");
    private static readonly SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/Imperial/Cargo/triple_beep.ogg");
    private static readonly SoundSpecifier WarningSound = new SoundPathSpecifier("/Audio/Imperial/Cargo/warning-buzzer.ogg");
    private static readonly SoundSpecifier UnlinkSound = new SoundPathSpecifier("/Audio/Imperial/Cargo/synth_no.ogg");

    /// <summary>printed_beacons.</summary>
    private int _printedBeacons;

    private float _uiAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialExpressConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ImperialExpressConsoleComponent, ImperialExpressActionMessage>(OnAction);
        SubscribeLocalEvent<ImperialExpressConsoleComponent, ImperialExpressBuyMessage>(OnBuy);
        SubscribeLocalEvent<ImperialExpressConsoleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ImperialExpressConsoleComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<ImperialExpressConsoleComponent, EntityTerminatingEvent>(OnConsoleTerminating);

        SubscribeLocalEvent<ImperialSupplyPodBeaconComponent, GetVerbsEvent<AlternativeVerb>>(OnBeaconVerbs);
        SubscribeLocalEvent<ImperialSupplyPodBeaconComponent, ExaminedEvent>(OnBeaconExamined);
        SubscribeLocalEvent<ImperialSupplyPodBeaconComponent, EntityTerminatingEvent>(OnBeaconTerminating);
    }

    private bool Emagged(EntityUid uid) => _emag.CheckFlag(uid, EmagType.Interaction);

    #region Консоль

    private void OnUiOpened(Entity<ImperialExpressConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnInteractUsing(Entity<ImperialExpressConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<ImperialSupplyPodBeaconComponent>(args.Used))
        {
            args.Handled = true;
            var beacon = Comp<ImperialSupplyPodBeaconComponent>(args.Used);
            if (beacon.Console == ent.Owner)
            {
                _popup.PopupEntity(Loc.GetString("imperial-express-already-linked", ("beacon", args.Used)), ent, args.User);
                return;
            }

            Link((args.Used, beacon), ent, args.User);
            return;
        }

        if (HasComp<ImperialBluespacePodDiskComponent>(args.Used))
        {
            args.Handled = true;
            if (ent.Comp.BluespacePods)
            {
                _popup.PopupEntity(Loc.GetString("imperial-express-already-upgraded"), ent, args.User);
                return;
            }

            ent.Comp.BluespacePods = true;
            QueueDel(args.Used);
            _popup.PopupEntity(Loc.GetString("imperial-express-disk-inserted"), ent, args.User);
            return;
        }

        // Карта с доступом карго (allowed()) запирает и отпирает интерфейс.
        if (_idCard.TryGetIdCard(args.Used, out _) && _access.IsAllowed(args.User, ent))
        {
            args.Handled = true;
            ent.Comp.Locked = !ent.Comp.Locked;
            _popup.PopupEntity(Loc.GetString(ent.Comp.Locked ? "imperial-express-locked" : "imperial-express-unlocked"), ent, args.User);
            UpdateUi(ent);
        }
    }

    private void OnEmagged(Entity<ImperialExpressConsoleComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || Emagged(ent))
            return;

        // emag_act(): капсулы летят куда угодно на станцию, открыта контрабанда.
        _popup.PopupEntity(Loc.GetString("imperial-express-emagged"), ent, args.UserUid);
        args.Handled = true;
    }

    private void OnConsoleTerminating(Entity<ImperialExpressConsoleComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Beacon is { } beacon && TryComp<ImperialSupplyPodBeaconComponent>(beacon, out var comp))
            Unlink((beacon, comp));
    }

    private int Points(EntityUid console)
    {
        return _station.GetOwningStation(console) is { } station && TryComp<StationBankAccountComponent>(station, out var bank)
            ? _cargo.GetBalanceFromAccount((station, bank), CargoAccount)
            : 0;
    }

    private float Discount(EntityUid console) => Emagged(console) ? EmagDiscount : 1f;

    /// <summary>get_packs_data(): обычные наборы, наборы только для капсул и контрабанда у взломанной консоли.</summary>
    private IEnumerable<CargoProductPrototype> Packs(EntityUid console)
    {
        var emagged = Emagged(console);
        foreach (var product in _proto.EnumeratePrototypes<CargoProductPrototype>())
        {
            var group = product.Group;
            if (group == ImperialCargoCatalogSystem.Market || group == ImperialCargoCatalogSystem.Pod
                || emagged && (group == ImperialCargoCatalogSystem.Contraband || group == ImperialCargoCatalogSystem.Emag))
                yield return product;
        }
    }

    private bool BeaconUsable(EntityUid beacon)
    {
        // canBeacon: маяк лежит на полу или у кого-то в руках.
        if (!_container.TryGetContainingContainer((beacon, null, null), out var container))
            return true;

        return HasComp<MobStateComponent>(container.Owner);
    }

    public void UpdateUi(Entity<ImperialExpressConsoleComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, ImperialExpressConsoleUiKey.Key))
            return;

        var comp = ent.Comp;
        var beacon = comp.Beacon;
        var canBeacon = beacon != null && BeaconUsable(beacon.Value);
        var now = _timing.CurTime;
        var printReady = now >= comp.NextBeaconPrint;
        var points = Points(ent);

        var message = Loc.GetString("imperial-express-message-default");
        if (comp.UsingBeacon && beacon == null)
            message = Loc.GetString("imperial-express-message-beacon-missing");
        else if (comp.UsingBeacon && !canBeacon)
            message = Loc.GetString("imperial-express-message-beacon-exposed");
        if (Emagged(ent))
            message = Loc.GetString("imperial-express-message-emag");

        var printMessage = printReady
            ? Loc.GetString("imperial-express-print", ("cost", BeaconCost))
            : Loc.GetString("imperial-express-print-cooldown", ("cost", BeaconCost), ("time", (int) Math.Ceiling((comp.NextBeaconPrint - now).TotalSeconds)));

        var discount = Discount(ent);
        var packs = Packs(ent).Select(p => new ImperialExpressPack
        {
            Id = p.ID,
            Name = p.Name,
            Description = p.Description,
            Category = Loc.GetString(p.Category),
            Cost = (int) (p.Cost * discount),
        }).OrderBy(p => p.Category).ThenBy(p => p.Name).ToList();

        _ui.SetUiState(ent.Owner, ImperialExpressConsoleUiKey.Key, new ImperialExpressConsoleState(
            points,
            comp.Locked,
            comp.UsingBeacon,
            beacon != null,
            !comp.UsingBeacon || canBeacon,
            printReady && points >= BeaconCost,
            beacon != null ? Name(beacon.Value) : Loc.GetString("imperial-express-no-beacon"),
            printMessage,
            message,
            packs));
    }

    private void OnAction(Entity<ImperialExpressConsoleComponent> ent, ref ImperialExpressActionMessage args)
    {
        if (ent.Comp.Locked)
            return;

        switch (args.Action)
        {
            case ImperialExpressAction.LandingCargo:
                ent.Comp.UsingBeacon = false;
                if (ent.Comp.Beacon is { } off && TryComp<ImperialSupplyPodBeaconComponent>(off, out var offComp))
                    SetReady((off, offComp), false);
                break;
            case ImperialExpressAction.LandingBeacon:
                ent.Comp.UsingBeacon = true;
                if (ent.Comp.Beacon is { } on && TryComp<ImperialSupplyPodBeaconComponent>(on, out var onComp))
                    SetReady((on, onComp), true);
                break;
            case ImperialExpressAction.PrintBeacon:
                PrintBeacon(ent, args.Actor);
                break;
        }

        UpdateUi(ent);
    }

    private bool TryPay(EntityUid console, int cost)
    {
        if (_station.GetOwningStation(console) is not { } station || !TryComp<StationBankAccountComponent>(station, out var bank))
            return false;
        if (_cargo.GetBalanceFromAccount((station, bank), CargoAccount) < cost)
            return false;

        _cargo.UpdateBankAccount((station, bank), -cost, CargoAccount);
        return true;
    }

    private void PrintBeacon(Entity<ImperialExpressConsoleComponent> ent, EntityUid user)
    {
        if (_timing.CurTime < ent.Comp.NextBeaconPrint || !TryPay(ent, BeaconCost))
            return;

        ent.Comp.NextBeaconPrint = _timing.CurTime + BeaconPrintCooldown;
        var beacon = Spawn(BeaconProto, Transform(ent).Coordinates);
        Link((beacon, EnsureComp<ImperialSupplyPodBeaconComponent>(beacon)), ent, user);
        _printedBeacons++;
        _meta.SetEntityName(beacon, Loc.GetString("imperial-express-beacon-name", ("number", _printedBeacons)));
    }

    private void Say(EntityUid console, string message)
    {
        _chat.TrySendInGameICMessage(console, message, InGameICChatType.Speak, false);
    }

    private void OnBuy(Entity<ImperialExpressConsoleComponent> ent, ref ImperialExpressBuyMessage args)
    {
        if (ent.Comp.Locked)
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextLaunch)
        {
            Say(ent, Loc.GetString("imperial-express-recalibrating"));
            return;
        }

        if (!_proto.TryIndex<CargoProductPrototype>(args.Pack, out var pack) || !Packs(ent).Contains(pack))
            return;

        var emagged = Emagged(ent);
        var cost = (int) (pack.Cost * Discount(ent));
        var bluespace = ent.Comp.BluespacePods || pack.ID == EmptyPodPack;
        EntProtoId? cargo = pack.ID == EmptyPodPack ? (EntProtoId?) null : pack.Product;

        List<EntityCoordinates> tiles;
        if (emagged || ent.Comp.Beacon == null || !ent.Comp.UsingBeacon)
        {
            tiles = emagged ? StationTiles(ent) : CargoBayTiles(ent);
            if (tiles.Count == 0)
                return;
        }
        else
        {
            tiles = new List<EntityCoordinates>();
        }

        if (emagged)
        {
            // Взлом: до MAX_EMAG_ROCKETS капсул в случайные места станции, пока хватает денег.
            ent.Comp.NextLaunch = now + TimeSpan.FromSeconds(10);
            for (var i = 0; i < MaxEmagRockets && tiles.Count > 0; i++)
            {
                if (!TryPay(ent, cost))
                    break;

                var tile = _random.PickAndTake(tiles);
                _pods.Launch(tile, cargo, bluespace);
            }

            UpdateUi(ent);
            return;
        }

        EntityCoordinates target;
        if (ent.Comp.Beacon is { } beacon && ent.Comp.UsingBeacon)
        {
            if (!BeaconUsable(beacon))
                return;
            target = _transform.GetMoverCoordinates(beacon);
        }
        else
        {
            target = _random.Pick(tiles);
        }

        if (!TryPay(ent, cost))
            return;

        if (ent.Comp.Beacon is { } launched && ent.Comp.UsingBeacon && TryComp<ImperialSupplyPodBeaconComponent>(launched, out var launchedComp))
            Launched((launched, launchedComp));

        ent.Comp.NextLaunch = now + TimeSpan.FromSeconds(5);
        _pods.Launch(target, cargo, bluespace);
        UpdateUi(ent);
    }

    /// <summary>Свободный пол клетки.</summary>
    private bool FreeFloor(EntityUid grid, MapGridComponent gridComp, Vector2i index)
    {
        if (!_map.TryGetTileRef(grid, gridComp, index, out var tile) || tile.Tile.IsEmpty || _turf.IsSpace(tile))
            return false;

        return !_turf.IsTileBlocked(tile, CollisionGroup.Impassable);
    }

    /// <summary>landingzone = /area/station/cargo/storage: клетки вокруг приёмных паллет станции.</summary>
    private List<EntityCoordinates> CargoBayTiles(EntityUid console)
    {
        var result = new List<EntityCoordinates>();
        var station = _station.GetOwningStation(console);
        var pallets = EntityQueryEnumerator<CargoPalletComponent, TransformComponent>();
        while (pallets.MoveNext(out var pallet, out var palletComp, out var xform))
        {
            if ((palletComp.PalletType & BuySellType.Buy) == 0 || xform.GridUid is not { } grid
                || _station.GetOwningStation(pallet) != station || !TryComp<MapGridComponent>(grid, out var gridComp))
                continue;

            var center = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
            for (var dx = -3; dx <= 3; dx++)
            {
                for (var dy = -3; dy <= 3; dy++)
                {
                    var index = center + new Vector2i(dx, dy);
                    if (FreeFloor(grid, gridComp, index))
                        result.Add(_map.GridTileToLocal(grid, gridComp, index));
                }
            }
        }

        if (result.Count > 0)
            return result;

        // Нет грузового отсека — садимся рядом с консолью (get_area(src)).
        if (Transform(console).GridUid is { } consoleGrid && TryComp<MapGridComponent>(consoleGrid, out var consoleGridComp))
        {
            var center = _map.TileIndicesFor(consoleGrid, consoleGridComp, Transform(console).Coordinates);
            for (var dx = -4; dx <= 4; dx++)
            {
                for (var dy = -4; dy <= 4; dy++)
                {
                    var index = center + new Vector2i(dx, dy);
                    if (FreeFloor(consoleGrid, consoleGridComp, index))
                        result.Add(_map.GridTileToLocal(consoleGrid, consoleGridComp, index));
                }
            }
        }

        return result;
    }

    /// <summary>pick(GLOB.the_station_areas): случайные свободные клетки грида станции.</summary>
    private List<EntityCoordinates> StationTiles(EntityUid console)
    {
        var result = new List<EntityCoordinates>();
        if (Transform(console).GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return result;

        var tiles = _map.GetAllTiles(grid, gridComp).ToList();
        for (var i = 0; i < 200 && result.Count < MaxEmagRockets * 4 && tiles.Count > 0; i++)
        {
            var tile = _random.Pick(tiles);
            if (FreeFloor(grid, gridComp, tile.GridIndices))
                result.Add(_map.GridTileToLocal(grid, gridComp, tile.GridIndices));
        }

        return result;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _uiAccumulator += frameTime;
        if (_uiAccumulator >= 1f)
        {
            _uiAccumulator = 0;
            var consoles = EntityQueryEnumerator<ImperialExpressConsoleComponent>();
            while (consoles.MoveNext(out var uid, out var comp))
            {
                UpdateUi((uid, comp));
            }
        }

        var now = _timing.CurTime;
        var beacons = EntityQueryEnumerator<ImperialSupplyPodBeaconComponent>();
        while (beacons.MoveNext(out var uid, out var beacon))
        {
            if (beacon.LaunchedUntil is { } until && now >= until)
            {
                beacon.LaunchedUntil = null;
                UpdateBeaconVisuals((uid, beacon));
            }
        }
    }

    #endregion

    #region Маяк

    /// <summary>link_console().</summary>
    private void Link(Entity<ImperialSupplyPodBeaconComponent> beacon, Entity<ImperialExpressConsoleComponent> console, EntityUid? user)
    {
        if (console.Comp.Beacon is { } old && old != beacon.Owner && TryComp<ImperialSupplyPodBeaconComponent>(old, out var oldComp))
            Unlink((old, oldComp));

        if (beacon.Comp.Console is { } oldConsole && TryComp<ImperialExpressConsoleComponent>(oldConsole, out var oldConsoleComp))
            oldConsoleComp.Beacon = null;

        beacon.Comp.Console = console;
        console.Comp.Beacon = beacon;
        Dirty(beacon);
        _audio.PlayPvs(LinkSound, beacon);
        beacon.Comp.Ready = console.Comp.UsingBeacon;
        UpdateBeaconVisuals(beacon);

        if (user != null)
            _popup.PopupEntity(Loc.GetString("imperial-express-beacon-linked", ("beacon", beacon.Owner), ("console", console.Owner)), beacon, user.Value);
        UpdateUi(console);
    }

    /// <summary>unlink_console().</summary>
    private void Unlink(Entity<ImperialSupplyPodBeaconComponent> beacon)
    {
        if (beacon.Comp.Console is { } console && TryComp<ImperialExpressConsoleComponent>(console, out var consoleComp))
        {
            consoleComp.Beacon = null;
            UpdateUi((console, consoleComp));
        }

        beacon.Comp.Console = null;
        beacon.Comp.Ready = false;
        Dirty(beacon);
        _audio.PlayPvs(UnlinkSound, beacon);
        UpdateBeaconVisuals(beacon);
    }

    private void SetReady(Entity<ImperialSupplyPodBeaconComponent> beacon, bool ready)
    {
        beacon.Comp.Ready = ready;
        UpdateBeaconVisuals(beacon);
    }

    /// <summary>update_status(SP_LAUNCH): зелёный огонёк, пока капсула летит (3.3 с).</summary>
    private void Launched(Entity<ImperialSupplyPodBeaconComponent> beacon)
    {
        beacon.Comp.LaunchedUntil = _timing.CurTime + TimeSpan.FromSeconds(3.3);
        _audio.PlayPvs(LaunchSound, beacon);
        _audio.PlayPvs(WarningSound, beacon);
        UpdateBeaconVisuals(beacon);
    }

    private void UpdateBeaconVisuals(Entity<ImperialSupplyPodBeaconComponent> beacon)
    {
        var light = beacon.Comp.LaunchedUntil != null ? "sp_green"
            : beacon.Comp.Ready ? "sp_yellow"
            : beacon.Comp.Console != null ? "sp_orange"
            : string.Empty;
        _appearance.SetData(beacon, ImperialSupplyPodBeaconVisuals.Light, light);
    }

    private void OnBeaconVerbs(Entity<ImperialSupplyPodBeaconComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-express-beacon-unlink-verb"),
            Act = () =>
            {
                if (ent.Comp.Console == null)
                {
                    _popup.PopupEntity(Loc.GetString("imperial-express-beacon-no-console"), ent, user);
                    return;
                }

                Unlink(ent);
            },
        });
    }

    private void OnBeaconExamined(Entity<ImperialSupplyPodBeaconComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-express-beacon-examine-bolts"));
        args.PushMarkup(Loc.GetString(ent.Comp.Console == null ? "imperial-express-beacon-examine-unlinked" : "imperial-express-beacon-examine-linked"));
    }

    private void OnBeaconTerminating(Entity<ImperialSupplyPodBeaconComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Console is { } console && TryComp<ImperialExpressConsoleComponent>(console, out var comp))
        {
            comp.Beacon = null;
            UpdateUi((console, comp));
        }
    }

    #endregion
}
