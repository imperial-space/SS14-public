using System.Linq;
using Content.Server.Cargo.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.PDA.Ringer;
using Content.Server.Store.Systems;
using Content.Server.Traitor.Uplink;
using Content.Shared.Cargo.Components;
using Content.Shared.Examine;
using Content.Shared.Imperial.Uplink.Stealth;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.PDA;
using Content.Shared.PDA.Ringer;
using Content.Shared.Popups;
using Content.Shared.Store.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Uplink.Stealth;

public sealed partial class UplinkStealthSystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly RingerSystem _ringer = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly Content.Server.Stack.StackSystem _serverStack = default!;

    private const string FailsafeListing = "ImperialUplinkFailsafe";
    private const string TelecommListing = "ImperialUplinkTelecommBlackout";
    private const string BlackoutListing = "ImperialUplinkBlackout";

    private void InitializeSabotage()
    {
        SubscribeLocalEvent<StoreBuyFinishedEvent>(OnBuyFinished);
        SubscribeLocalEvent<PdaComponent, RingerSetRingtoneMessage>(OnSetRingtone, before: [typeof(RingerSystem)]);

        SubscribeLocalEvent<UplinkFrameDiskComponent, AfterInteractEvent>(OnFrameInteract);
        SubscribeLocalEvent<UplinkFrameDiskComponent, ExaminedEvent>(OnFrameExamined);

        SubscribeLocalEvent<UplinkCrabPhoneComponent, UseInHandEvent>(OnCrabPhoneUse);
        SubscribeLocalEvent<UplinkCrabMachineComponent, InteractHandEvent>(OnCrabMachineInteract);
        SubscribeLocalEvent<UplinkCrabMachineComponent, ExaminedEvent>(OnCrabMachineExamined);

        SubscribeLocalEvent<UplinkLaunchpadBriefcaseComponent, UseInHandEvent>(OnLaunchpadBriefcaseUse);
        SubscribeLocalEvent<UplinkLaunchpadComponent, GetVerbsEvent<AlternativeVerb>>(OnLaunchpadVerbs);
        SubscribeLocalEvent<UplinkLaunchpadRemoteComponent, AfterInteractEvent>(OnRemoteInteract);
        SubscribeLocalEvent<UplinkLaunchpadRemoteComponent, GetVerbsEvent<AlternativeVerb>>(OnRemoteVerbs);
        SubscribeLocalEvent<UplinkLaunchpadRemoteComponent, ExaminedEvent>(OnRemoteExamined);
        SubscribeLocalEvent<UplinkLaunchpadRemoteComponent, UseInHandEvent>(OnRemoteUse);
        SubscribeLocalEvent<UplinkLaunchpadRemoteComponent, UplinkLaunchpadOffsetMessage>(OnLaunchpadOffset);
        SubscribeLocalEvent<UplinkLaunchpadRemoteComponent, UplinkLaunchpadFireMessage>(OnLaunchpadFire);
        SubscribeLocalEvent<UplinkLaunchpadComponent, ExaminedEvent>((_, _, args) => args.PushMarkup(Loc.GetString("uplink-launchpad-pad-help")));
    }

    private void UpdateSabotage()
    {
        var now = _timing.CurTime;
        var machines = EntityQueryEnumerator<UplinkCrabMachineComponent>();
        while (machines.MoveNext(out var uid, out var machine))
        {
            if (now >= machine.EndTime || now < machine.NextDrain)
                continue;

            machine.NextDrain = now + machine.Interval;
            var stations = _station.GetStations();
            if (_station.GetOwningStation(uid) is not { } target)
            {
                if (stations.Count == 0)
                    continue;

                target = stations[0];
            }
            if (!TryComp<StationBankAccountComponent>(target, out var bank))
                continue;

            foreach (var account in bank.Accounts.Keys.ToList())
            {
                var balance = bank.Accounts[account];
                var take = (int) (balance * machine.Fraction);
                if (take <= 0)
                    continue;

                _cargo.UpdateBankAccount((target, bank), -take, account);
                machine.Stolen += take;
            }
        }
    }

    #region Абстрактные покупки

    private void OnBuyFinished(ref StoreBuyFinishedEvent ev)
    {
        switch (ev.PurchasedItem.ID)
        {
            case FailsafeListing:
                SetupFailsafe(ev.StoreUid);
                break;
            case TelecommListing:
                _ticker.StartGameRule("ImperialUplinkCommsBlackout");
                break;
            case BlackoutListing:
                _ticker.StartGameRule("PowerGridCheck");
                break;
        }
    }

    /// <summary>device_tools/failsafe: код, при вводе которого аплинк самоуничтожается.</summary>
    private void SetupFailsafe(EntityUid store)
    {
        var failsafe = EnsureComp<UplinkFailsafeComponent>(store);
        var notes = Enum.GetValues<Note>();
        failsafe.Code = Enumerable.Range(0, SharedRingerSystem.RingtoneLength).Select(_ => _random.Pick(notes)).ToArray();

        if (TryComp<StoreComponent>(store, out var storeComp) && storeComp.AccountOwner is { } mind
            && TryComp<Content.Shared.Mind.MindComponent>(mind, out var mindComp) && mindComp.OwnedEntity is { } owner)
        {
            var code = string.Join("-", failsafe.Code.Select(n => n.ToString().Replace("sharp", "#")));
            _popup.PopupEntity(Loc.GetString("uplink-failsafe-code", ("code", code)), owner, owner, PopupType.Large);
            if (_player.TryGetSessionByEntity(owner, out var session))
            {
                var msg = Loc.GetString("uplink-failsafe-code", ("code", code));
                _chatManager.ChatMessageToOne(Content.Shared.Chat.ChatChannel.Server, msg, msg, owner, false, session.Channel, Color.Red);
            }
        }
    }

    private void OnSetRingtone(Entity<PdaComponent> ent, ref RingerSetRingtoneMessage args)
    {
        var query = EntityQueryEnumerator<UplinkFailsafeComponent>();
        while (query.MoveNext(out var store, out var failsafe))
        {
            if (failsafe.Code == null || !failsafe.Code.SequenceEqual(args.Ringtone))
                continue;

            _popup.PopupEntity(Loc.GetString("uplink-failsafe-boom"), ent, PopupType.LargeCaution);
            _explosion.QueueExplosion(_transform.GetMapCoordinates(ent), "Default", 30f, 6f, 6f, ent, canCreateVacuum: false);
            QueueDel(store);
            QueueDel(ent);
            return;
        }
    }

    #endregion

    #region F.R.A.M.E.

    private void OnFrameExamined(Entity<UplinkFrameDiskComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("uplink-frame-charges", ("charges", ent.Comp.Charges)));
    }

    /// <summary>Вирус F.R.A.M.E.: КПК цели становится аплинком без ТК, код получает агент.</summary>
    private void OnFrameInteract(Entity<UplinkFrameDiskComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<PdaComponent>(target))
            return;

        args.Handled = true;
        if (ent.Comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("uplink-frame-empty"), ent, args.User);
            return;
        }

        var store = Spawn(UplinkSystem.TraitorUplinkStore, MapCoordinates.Nullspace);
        var codeEv = new GenerateUplinkCodeEvent();
        RaiseLocalEvent(store, ref codeEv);
        if (codeEv.Code == null)
        {
            Del(store);
            return;
        }

        var access = EnsureComp<RingerAccessUplinkComponent>(store);
        _ringer.SetBoundUplinkEntity((store, access), target);

        ent.Comp.Charges--;
        var code = string.Join("-", codeEv.Code.Select(n => n.ToString().Replace("sharp", "#")));
        var msg = Loc.GetString("uplink-frame-done", ("target", target), ("code", code));
        _popup.PopupEntity(msg, args.User, args.User, PopupType.Medium);
        if (_player.TryGetSessionByEntity(args.User, out var session))
            _chatManager.ChatMessageToOne(Content.Shared.Chat.ChatChannel.Server, msg, msg, args.User, false, session.Channel, Color.Red);
    }

    #endregion

    #region CRAB-17

    /// <summary>suspiciousphone: обвал космического рынка — машина выкачивает средства станции.</summary>
    private void OnCrabPhoneUse(Entity<UplinkCrabPhoneComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var machine = Spawn(ent.Comp.Machine, Transform(args.User).Coordinates);
        var comp = EnsureComp<UplinkCrabMachineComponent>(machine);
        comp.Owner = args.User;
        comp.EndTime = _timing.CurTime + comp.Duration;
        comp.NextDrain = _timing.CurTime + comp.Interval;

        _chat.DispatchGlobalAnnouncement(Loc.GetString("uplink-crab-announcement"),
            Loc.GetString("uplink-crab-sender"),
            colorOverride: Color.Gold);
        QueueDel(ent);
    }

    private void OnCrabMachineExamined(Entity<UplinkCrabMachineComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("uplink-crab-stolen", ("amount", ent.Comp.Stolen)));
    }

    /// <summary>Владелец забирает украденное наличными.</summary>
    private void OnCrabMachineInteract(Entity<UplinkCrabMachineComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Stolen <= 0)
        {
            _popup.PopupEntity(Loc.GetString("uplink-crab-nothing"), ent, args.User);
            return;
        }

        _serverStack.SpawnMultipleAtPosition((EntProtoId) "SpaceCash", ent.Comp.Stolen, Transform(args.User).Coordinates);
        _popup.PopupEntity(Loc.GetString("uplink-crab-withdraw", ("amount", ent.Comp.Stolen)), ent, args.User);
        ent.Comp.Stolen = 0;

        if (_timing.CurTime >= ent.Comp.EndTime)
            QueueDel(ent);
    }

    #endregion

    #region Кейс-телепад

    private void OnLaunchpadBriefcaseUse(Entity<UplinkLaunchpadBriefcaseComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var pad = Spawn(ent.Comp.Pad, Transform(args.User).Coordinates);
        _transform.AnchorEntity(pad);

        var linked = false;
        var remotes = EntityQueryEnumerator<UplinkLaunchpadRemoteComponent>();
        while (remotes.MoveNext(out var remote))
        {
            if (remote.Briefcase != ent.Owner)
                continue;
            remote.Pad = pad;
            remote.Briefcase = null;
            linked = true;
        }

        // Пульт из того же набора, который ещё ни к чему не привязан, привязывается сам.
        if (!linked)
        {
            foreach (var item in _inventory.GetHandOrInventoryEntities(args.User))
            {
                if (!TryComp<UplinkLaunchpadRemoteComponent>(item, out var free) || free.Pad != null || free.Briefcase != null)
                    continue;
                free.Pad = pad;
                _popup.PopupEntity(Loc.GetString("uplink-launchpad-link"), item, args.User);
                break;
            }
        }

        QueueDel(ent);
        _popup.PopupEntity(Loc.GetString("uplink-launchpad-deploy"), pad, args.User);
    }

    /// <summary>Сложить телепад обратно в кейс.</summary>
    private void OnLaunchpadVerbs(Entity<UplinkLaunchpadComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Briefcase == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("uplink-launchpad-pack"),
            Act = () =>
            {
                var briefcase = Spawn(ent.Comp.Briefcase.Value, Transform(ent).Coordinates);
                var remotes = EntityQueryEnumerator<UplinkLaunchpadRemoteComponent>();
                while (remotes.MoveNext(out var remote))
                {
                    if (remote.Pad != ent.Owner)
                        continue;

                    remote.Pad = null;
                    remote.Briefcase = briefcase;
                }

                QueueDel(ent);
                _hands.PickupOrDrop(user, briefcase);
            },
        });
    }

    private void OnRemoteExamined(Entity<UplinkLaunchpadRemoteComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Pad != null || ent.Comp.Briefcase != null ? "uplink-launchpad-linked" : "uplink-launchpad-unlinked"));
        args.PushMarkup(Loc.GetString("uplink-launchpad-offset", ("x", ent.Comp.X), ("y", ent.Comp.Y)));
        args.PushMarkup(Loc.GetString("uplink-launchpad-help"));
    }

    /// <summary>attack_self(): задать смещение цели.</summary>
    private void OnRemoteUse(Entity<UplinkLaunchpadRemoteComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _launchpadUi.OpenUi(ent.Owner, UplinkLaunchpadUiKey.Key, args.User);
        UpdateLaunchpadUi(ent);
    }

    private void UpdateLaunchpadUi(Entity<UplinkLaunchpadRemoteComponent> ent)
    {
        var pad = ent.Comp.Pad is { } p && !TerminatingOrDeleted(p) ? p : (EntityUid?) null;
        var range = pad != null && TryComp<UplinkLaunchpadComponent>(pad, out var padComp) ? padComp.Range : 8;
        _launchpadUi.SetUiState(ent.Owner, UplinkLaunchpadUiKey.Key,
            new UplinkLaunchpadState(pad != null || ent.Comp.Briefcase != null, pad != null, ent.Comp.X, ent.Comp.Y, range));
    }

    private void OnLaunchpadOffset(Entity<UplinkLaunchpadRemoteComponent> ent, ref UplinkLaunchpadOffsetMessage args)
    {
        ent.Comp.X = Math.Clamp(args.X, -8, 8);
        ent.Comp.Y = Math.Clamp(args.Y, -8, 8);
        UpdateLaunchpadUi(ent);
    }

    private void OnLaunchpadFire(Entity<UplinkLaunchpadRemoteComponent> ent, ref UplinkLaunchpadFireMessage args)
    {
        FireLaunchpad(ent, args.Actor, args.Sending);
        UpdateLaunchpadUi(ent);
    }

    /// <summary>Пульт привязывается касанием кейса или телепада.</summary>
    private void OnRemoteInteract(Entity<UplinkLaunchpadRemoteComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (HasComp<UplinkLaunchpadBriefcaseComponent>(target))
        {
            ent.Comp.Briefcase = target;
            ent.Comp.Pad = null;
        }
        else if (HasComp<UplinkLaunchpadComponent>(target))
        {
            ent.Comp.Pad = target;
            ent.Comp.Briefcase = null;
        }
        else
        {
            return;
        }

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("uplink-launchpad-link"), ent, args.User);
    }

    private void OnRemoteVerbs(Entity<UplinkLaunchpadRemoteComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (_player.TryGetSessionByEntity(user, out var session))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("uplink-launchpad-set-offset"),
                Priority = 3,
                Act = () => _dialog.OpenDialog<int, int>(session,
                    Loc.GetString("uplink-launchpad-title"),
                    "X",
                    "Y",
                    (x, y) =>
                    {
                        ent.Comp.X = x;
                        ent.Comp.Y = y;
                        _popup.PopupEntity(Loc.GetString("uplink-launchpad-offset", ("x", x), ("y", y)), ent, user);
                    }),
            });
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("uplink-launchpad-send"),
            Priority = 2,
            Act = () => FireLaunchpad(ent, user, true),
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("uplink-launchpad-retrieve"),
            Priority = 1,
            Act = () => FireLaunchpad(ent, user, false),
        });
    }

    /// <summary>launchpad/doteleport: всё на площадке ↔ всё на целевом тайле в радиусе 8.</summary>
    private void FireLaunchpad(Entity<UplinkLaunchpadRemoteComponent> ent, EntityUid user, bool sending)
    {
        if (ent.Comp.Pad is not { } pad || !TryComp<UplinkLaunchpadComponent>(pad, out var padComp) || TerminatingOrDeleted(pad))
        {
            _popup.PopupEntity(Loc.GetString("uplink-launchpad-unlinked"), ent, user);
            return;
        }

        if (_timing.CurTime < ent.Comp.ReadyAt)
        {
            _popup.PopupEntity(Loc.GetString("uplink-launchpad-charging"), ent, user);
            return;
        }

        if (Math.Abs(ent.Comp.X) > padComp.Range || Math.Abs(ent.Comp.Y) > padComp.Range)
        {
            _popup.PopupEntity(Loc.GetString("uplink-launchpad-range", ("range", padComp.Range)), ent, user);
            return;
        }

        ent.Comp.ReadyAt = _timing.CurTime + TimeSpan.FromSeconds(3);
        // Смещение — в осях станции (сетки телепада), как x/y offset у TG.
        var padCoords = Transform(pad).Coordinates;
        var targetCoords = padCoords.Offset(new System.Numerics.Vector2(ent.Comp.X, ent.Comp.Y));
        var from = sending ? padCoords : targetCoords;
        var to = sending ? targetCoords : padCoords;

        foreach (var thing in _lookup.GetEntitiesInRange(from, 0.45f, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (thing == pad || Transform(thing).Anchored || _container.IsEntityInContainer(thing))
                continue;

            _transform.SetCoordinates(thing, to);
            _transform.AttachToGridOrMap(thing);
        }

        _popup.PopupEntity(Loc.GetString(sending ? "uplink-launchpad-sent" : "uplink-launchpad-pulled"), ent, user);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Uplink/emitter2.ogg"), pad);
    }

    #endregion
}
