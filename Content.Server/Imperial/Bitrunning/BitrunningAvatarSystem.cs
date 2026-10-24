using System.Linq;
using Content.Server.Body.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Server.Power.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Dataset;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Station;
using Content.Shared.Stunnable;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>
/// Нетпод и аватар (netpod/*.dm, components/avatar_connection.dm, components/netpod_healing.dm):
/// разум битраннера «посещает» аватара в домене, урон аватара переходит на тело в капсуле.
/// </summary>
public sealed class BitrunningAvatarSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly Content.Server.Chat.Systems.ChatSystem _chatSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly QuantumServerSystem _servers = default!;
    [Dependency] private readonly BitrunningShopSystem _shop = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStationSpawningSystem _gear = default!;
    [Dependency] private readonly LoadoutSystem _loadout = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedToolSystem _tools = default!;
    [Dependency] private readonly Content.Shared.Inventory.InventorySystem _inventory = default!;
    [Dependency] private readonly Content.Shared.Storage.EntitySystems.SharedStorageSystem _storageSystem = default!;
    [Dependency] private readonly Content.Shared.Body.SharedVisualBodySystem _visualBody = default!;

    private static readonly SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Imperial/Bitrunning/tramopen.ogg");
    private static readonly SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/Imperial/Bitrunning/tramclose.ogg");
    private static readonly SoundSpecifier SubmergeSound = new SoundPathSpecifier("/Audio/Imperial/Bitrunning/submerge.ogg");
    private static readonly SoundSpecifier SplashSound = new SoundPathSpecifier("/Audio/Imperial/Bitrunning/splash.ogg");
    private static readonly SoundSpecifier BlinkSound = new SoundPathSpecifier("/Audio/Magic/blink.ogg");
    private static readonly SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Imperial/Bitrunning/terminal_alert.ogg");
    private static readonly SoundSpecifier SuccessSound = new SoundPathSpecifier("/Audio/Imperial/Bitrunning/terminal_success.ogg");
    private static readonly SoundSpecifier PrySound = new SoundPathSpecifier("/Audio/Machines/airlock_creaking.ogg");

    private static readonly EntProtoId AvatarProto = "MobHuman";
    private static readonly EntProtoId DomainInfoAction = "ActionBitrunningDomainInfo";
    private static readonly ProtoId<DatasetPrototype> AliasDataset = "BitrunningHackerAliases";
    private static readonly ProtoId<AlertPrototype> CompletedAlert = "BitrunningDomainComplete";
    private static readonly string[] IgnoredDamage = { "Asphyxiation", "Bloodloss", "Structural" };

    private static readonly string[] StrippedSlots = { "ears", "eyes", "gloves", "pocket1", "pocket2", "outerClothing", "suitstorage", "id", "belt" };
    private static readonly string[] BackpackContents = { "BoxSurvival", "MedkitFilled", "FlashlightLantern" };
    private const string HostMonitorProto = "BitrunningHostMonitor";

    private static readonly TimeSpan EnterTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PryTime = TimeSpan.FromSeconds(15);
    private static readonly ProtoId<Content.Shared.Tools.ToolQualityPrototype> PryingQuality = "Prying";
    private const float BaseHeal = 4f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NetpodComponent, MapInitEvent>(OnNetpodInit);
        SubscribeLocalEvent<NetpodComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<NetpodComponent, NetpodEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<NetpodComponent, InteractUsingEvent>(OnNetpodInteractUsing);
        SubscribeLocalEvent<NetpodComponent, NetpodPryDoAfterEvent>(OnPryDoAfter);
        SubscribeLocalEvent<NetpodComponent, ContainerRelayMovementEntityEvent>(OnRelayMovement);
        SubscribeLocalEvent<NetpodComponent, PowerChangedEvent>(OnNetpodPower);
        SubscribeLocalEvent<NetpodComponent, EntityTerminatingEvent>(OnNetpodTerminating);
        SubscribeLocalEvent<NetpodComponent, DamageChangedEvent>(OnNetpodDamaged);
        SubscribeLocalEvent<NetpodComponent, GetVerbsEvent<AlternativeVerb>>(OnNetpodAltVerbs);
        SubscribeLocalEvent<NetpodComponent, ActivatableUIOpenAttemptEvent>(OnNetpodUiAttempt);
        SubscribeLocalEvent<NetpodComponent, BoundUIOpenedEvent>(OnNetpodUiOpened);
        SubscribeLocalEvent<NetpodComponent, NetpodSelectOutfitMessage>(OnNetpodSelectOutfit);
        SubscribeLocalEvent<BitrunningAvatarComponent, Content.Shared.Polymorph.PolymorphedEvent>(OnAvatarPolymorphed);
        SubscribeLocalEvent<NetpodComponent, ExaminedEvent>(OnNetpodExamined);
        SubscribeLocalEvent<NetpodComponent, EntRemovedFromContainerMessage>(OnOccupantRemoved);

        SubscribeLocalEvent<BitrunningAvatarComponent, DamageChangedEvent>(OnAvatarDamaged);
        SubscribeLocalEvent<BitrunningAvatarComponent, MobStateChangedEvent>(OnAvatarMobState);
        SubscribeLocalEvent<BitrunningAvatarComponent, EntityTerminatingEvent>(OnAvatarTerminating);
        SubscribeLocalEvent<BitrunningAvatarComponent, BitrunningSafeDisconnectAlertEvent>(OnSafeDisconnectAlert);
        SubscribeLocalEvent<BitrunningAvatarComponent, BitrunningDomainInfoActionEvent>(OnDomainInfo);

        SubscribeLocalEvent<BitrunningPilotComponent, MobStateChangedEvent>(OnPilotMobState);

        SubscribeLocalEvent<BitrunningHostMonitorComponent, Content.Shared.Interaction.Events.UseInHandEvent>(OnHostMonitorUse);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<NetpodComponent>();
        while (query.MoveNext(out var uid, out var pod))
        {
            if (pod.AutoOpenAt is { } open && now >= open)
            {
                pod.AutoOpenAt = null;
                AutoDisconnect((uid, pod));
            }

            if (!pod.Open && now >= pod.NextHeal && Occupant((uid, pod)) is { } occupant)
            {
                pod.NextHeal = now + TimeSpan.FromSeconds(1);
                HealOccupant(occupant);
            }
        }
    }

    #region Нетпод

    private void OnNetpodInit(Entity<NetpodComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);
        UpdateNetpodAppearance(ent);
    }

    public EntityUid? Occupant(Entity<NetpodComponent> ent)
    {
        return _container.TryGetContainer(ent, ent.Comp.ContainerId, out var container) && container.ContainedEntities.Count > 0
            ? container.ContainedEntities[0]
            : null;
    }

    private void UpdateNetpodAppearance(Entity<NetpodComponent> ent)
    {
        string state;
        if (!_power.IsPowered(ent.Owner))
            state = "netpod";
        else if (ent.Comp.Open)
            state = "netpod_open_active";
        else
            state = Occupant(ent) != null ? "netpod_closed_active" : "netpod_closed";

        _appearance.SetData(ent, BitrunningVisuals.State, state);
    }

    /// <summary>mouse_drop_receive() → close_machine() → enter_matrix().</summary>
    private void OnDragDrop(Entity<NetpodComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || !ent.Comp.Open || args.Dragged != args.User || !_power.IsPowered(ent.Owner))
            return;

        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container) || !_container.Insert(args.User, container))
            return;

        args.Handled = true;
        ent.Comp.Open = false;
        Dirty(ent);
        _audio.PlayPvs(CloseSound, ent);
        UpdateNetpodAppearance(ent);
        EnterMatrix(ent, args.User);
    }

    /// <summary>enter_matrix(): 2 с подключения, руки заблокированы, чтобы ничего не протащить.</summary>
    private void EnterMatrix(Entity<NetpodComponent> ent, EntityUid neo)
    {
        if (!_mind.TryGetMind(neo, out _, out _) || _mobs.IsDead(neo))
        {
            _popup.PopupEntity(Loc.GetString("bitrunning-netpod-invalid-user"), ent, neo);
            return;
        }

        if (_servers.FindServer(ent) is not { } server)
        {
            _popup.PopupEntity(Loc.GetString("bitrunning-netpod-no-server"), ent, neo);
            return;
        }

        if (server.Comp.Domain == null || !server.Comp.Ready)
        {
            _popup.PopupEntity(Loc.GetString("bitrunning-netpod-nothing-loaded"), ent, neo);
            return;
        }

        ent.Comp.Server = server;
        _popup.PopupEntity(Loc.GetString("bitrunning-netpod-connecting"), ent, neo);
        var args = new DoAfterArgs(EntityManager, neo, EnterTime, new NetpodEnterDoAfterEvent(), ent, target: ent)
        {
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(args))
            OpenPod(ent);
    }

    private void OnEnterDoAfter(Entity<NetpodComponent> ent, ref NetpodEnterDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var neo = args.User;
        if (args.Cancelled || Occupant(ent) != neo)
        {
            OpenPod(ent);
            return;
        }

        if (ent.Comp.Server is not { } serverUid || !TryComp<QuantumServerComponent>(serverUid, out var server)
            || server.Domain == null || !_mind.TryGetMind(neo, out var mindId, out var mind))
        {
            OpenPod(ent);
            return;
        }

        // Живой аватар этого нетпода переиспользуется, иначе — новый (тратит одну попытку).
        var avatar = ent.Comp.Avatar;
        if (avatar == null || TerminatingOrDeleted(avatar.Value) || !_mobs.IsAlive(avatar.Value))
        {
            avatar = StartNewConnection((serverUid, server), ent, neo);
            if (avatar == null)
            {
                _popup.PopupEntity(Loc.GetString("bitrunning-netpod-out-of-bandwidth"), ent, neo);
                return;
            }
        }

        if (Occupant(ent) != neo || _mobs.IsIncapacitated(neo))
        {
            OpenPod(ent);
            return;
        }

        _audio.PlayEntity(SubmergeSound, neo, neo);
        Connect((avatar.Value, ent, neo, mindId, (serverUid, server)));
    }

    /// <summary>start_new_connection() + generate_avatar() + stock_gear().</summary>
    private EntityUid? StartNewConnection(Entity<QuantumServerComponent> server, Entity<NetpodComponent> pod, EntityUid neo)
    {
        if (_servers.GetAvatarDestination(server) is not { } destination)
            return null;

        if (!_proto.TryIndex(server.Comp.Domain, out var domain))
            return null;

        var avatar = SpawnAtPosition(AvatarProto, destination);
        if (pod.Comp.CopyBody && HasComp<BodyComponent>(neo))
            _visualBody.CopyAppearanceFrom(neo, avatar);

        EquipAvatarOutfit(avatar, pod, domain);
        StripAvatarGear(avatar);
        _shop.StockGear(neo, avatar, domain);

        // Хакерский псевдоним вместо имени (hacker_alias).
        if (_proto.TryIndex(AliasDataset, out var aliases) && aliases.Values.Count > 0)
            _meta.SetEntityName(avatar, _random.Pick(aliases.Values));
        pod.Comp.Avatar = avatar;
        return avatar;
    }

    private void Connect((EntityUid Avatar, Entity<NetpodComponent> Pod, EntityUid Body, EntityUid Mind, Entity<QuantumServerComponent> Server) c)
    {
        var avatar = EnsureComp<BitrunningAvatarComponent>(c.Avatar);
        avatar.Body = c.Body;
        avatar.Mind = c.Mind;
        avatar.Server = c.Server;
        avatar.Netpod = c.Pod;
        avatar.Disconnecting = false;
        if (_proto.TryIndex(c.Server.Comp.Domain, out var domain) && domain.Help is { } help)
            avatar.HelpText = Loc.GetString(help);

        EnsureComp<BitrunningAvatarMarkerComponent>(c.Avatar);
        EnsureComp<BitrunningPilotComponent>(c.Body).Avatar = c.Avatar;
        c.Server.Comp.Avatars.Add(c.Avatar);
        c.Pod.Comp.Connected = true;

        if (!_actions.GetActions(c.Avatar).Any(a => MetaData(a.Owner).EntityPrototype?.ID == DomainInfoAction.Id))
            _actions.AddAction(c.Avatar, DomainInfoAction.Id);

        _servers.SetBodycam(c.Avatar, c.Server.Comp.Broadcasting);
        _mind.Visit(c.Mind, c.Avatar);
        _servers.AssignObjectives(c.Mind, c.Server);
        _servers.StartAmbience(c.Avatar);
        _audio.PlayEntity(BlinkSound, c.Avatar, c.Avatar);
        _stun.TryUpdateParalyzeDuration(c.Avatar, TimeSpan.FromSeconds(1));
        UpdateNetpodAppearance(c.Pod);
        Log.Info($"{ToPrettyString(c.Body)} подключился к аватару {ToPrettyString(c.Avatar)}");
    }

    /// <summary>
    /// generate_avatar(): уши, очки, перчатки, карманы, костюм и ID снимаются, пояс — монитор хоста,
    /// рюкзак — коробка выживания, аптечка и фонарь.
    /// </summary>
    private void StripAvatarGear(EntityUid avatar)
    {
        foreach (var slot in StrippedSlots)
        {
            if (_inventory.TryGetSlotEntity(avatar, slot, out var item))
                Del(item.Value);
        }

        _inventory.SpawnItemInSlot(avatar, "belt", HostMonitorProto, silent: true, force: true);

        if (_inventory.TryGetSlotEntity(avatar, "back", out var bag) && TryComp<Content.Shared.Storage.StorageComponent>(bag, out var storage))
        {
            foreach (var inside in storage.Container.ContainedEntities.ToList())
            {
                Del(inside);
            }

            foreach (var proto in BackpackContents)
            {
                _storageSystem.Insert(bag.Value, Spawn(proto, Transform(avatar).Coordinates), out _, playSound: false);
            }
        }
    }

    /// <summary>full_avatar_disconnect(): разум возвращается в тело, нетпод отрабатывает отключение.</summary>
    public void Disconnect(EntityUid avatarUid, bool causeDamage)
    {
        if (!TryComp<BitrunningAvatarComponent>(avatarUid, out var avatar) || avatar.Disconnecting)
            return;

        avatar.Disconnecting = true;
        if (TryComp<MindComponent>(avatar.Mind, out var mind) && mind.VisitingEntity == avatarUid)
            _mind.UnVisit(avatar.Mind, mind);
        _servers.RemoveObjectives(avatar.Mind);
        _servers.StopAmbience(avatarUid);

        if (TryComp<QuantumServerComponent>(avatar.Server, out var server))
            server.Avatars.Remove(avatarUid);

        RemComp<BitrunningPilotComponent>(avatar.Body);
        _alerts.ClearAlert(avatarUid, CompletedAlert);

        if (TryComp<NetpodComponent>(avatar.Netpod, out var pod))
            DisconnectOccupant((avatar.Netpod, pod), causeDamage);

        RemComp<BitrunningAvatarComponent>(avatarUid);
    }

    /// <summary>disconnect_occupant(): ослепление, паралич, при насильном выходе — урон и крик.</summary>
    private void DisconnectOccupant(Entity<NetpodComponent> ent, bool causeDamage)
    {
        ent.Comp.Connected = false;
        if (Occupant(ent) is not { } body || _mobs.IsDead(body))
        {
            OpenPod(ent);
            return;
        }

        _audio.PlayEntity(BlinkSound, body, body);
        // Удаляемая капсула: оглушение выронит предметы в умирающий контейнер.
        if (!TerminatingOrDeleted(ent))
            _stun.TryUpdateParalyzeDuration(body, TimeSpan.FromSeconds(2));

        if (!_power.IsPowered(ent.Owner))
        {
            OpenPod(ent);
            return;
        }

        var healTime = 1;
        if (TryComp<DamageableComponent>(body, out var damageable) && _damage.GetTotalDamage((body, damageable)) > 0)
            healTime = ((int) (_mobs.IsCritical(body) ? 2 : 0) + 2) * 5;
        ent.Comp.AutoOpenAt = _timing.CurTime + TimeSpan.FromSeconds(healTime);

        if (!causeDamage)
            return;

        // Мозга с уроном в SS14 нет: удар по нервной системе — клеточный урон.
        var server = ent.Comp.Server is { } s && TryComp<QuantumServerComponent>(s, out var serverComp) ? serverComp.ServoBonus : 0f;
        var amount = ent.Comp.DisconnectDamage * (1f - server) / 4f;
        var spec = new DamageSpecifier();
        spec.DamageDict["Cellular"] = amount;
        _damage.TryChangeDamage(body, spec, ignoreResistances: true);
        Scream(body);
        _popup.PopupEntity(Loc.GetString("bitrunning-netpod-forced-disconnect"), body, body, PopupType.LargeCaution);
    }

    /// <summary>auto_disconnect(): вода сливается, капсула открывается.</summary>
    private void AutoDisconnect(Entity<NetpodComponent> ent)
    {
        if (Occupant(ent) is not { } player || ent.Comp.Open || ent.Comp.Connected)
            return;

        _audio.PlayEntity(SplashSound, player, player);
        _popup.PopupEntity(Loc.GetString("bitrunning-netpod-draining"), player, player);
        OpenPod(ent);
    }

    public void OpenPod(Entity<NetpodComponent> ent)
    {
        if (ent.Comp.Open)
            return;

        // Удаляемую капсулу не открываем: содержимое выгрузит сам контейнер.
        if (TerminatingOrDeleted(ent))
            return;

        ent.Comp.Open = true;
        ent.Comp.AutoOpenAt = null;
        Dirty(ent);
        _audio.PlayPvs(OpenSound, ent);
        if (_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            _container.EmptyContainer(container);
        UpdateNetpodAppearance(ent);
    }

    private void ClosePod(Entity<NetpodComponent> ent)
    {
        ent.Comp.Open = false;
        Dirty(ent);
        _audio.PlayPvs(CloseSound, ent);
        UpdateNetpodAppearance(ent);
    }

    /// <summary>netpod_healing: 4 ед. в секунду грубого, ожогов, токсинов и крови.</summary>
    private void HealOccupant(EntityUid body)
    {
        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"] = -BaseHeal / 3f;
        heal.DamageDict["Slash"] = -BaseHeal / 3f;
        heal.DamageDict["Piercing"] = -BaseHeal / 3f;
        heal.DamageDict["Heat"] = -BaseHeal / 2f;
        heal.DamageDict["Cold"] = -BaseHeal / 2f;
        heal.DamageDict["Poison"] = -BaseHeal;
        _damage.TryChangeDamage(body, heal, ignoreResistances: true, interruptsDoAfters: false);
        _blood.TryModifyBloodLevel(body, BaseHeal);
    }

    private void OnRelayMovement(Entity<NetpodComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        // container_resist_act(): выйти можно, пока нет подключения.
        if (!ent.Comp.Open && !ent.Comp.Connected)
            OpenPod(ent);
    }

    private void OnOccupantRemoved(Entity<NetpodComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        // Пилот покинул капсулу — связь рвётся.
        if (TryComp<BitrunningPilotComponent>(args.Entity, out var pilot))
            Disconnect(pilot.Avatar, causeDamage: true);
    }

    /// <summary>default_pry_open(): занятый нетпод вскрывается 15 с, пользователя предупреждают.</summary>
    private void OnNetpodInteractUsing(Entity<NetpodComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tools.HasQuality(args.Used, PryingQuality))
            return;

        args.Handled = true;
        if (Occupant(ent) == null)
        {
            if (ent.Comp.Open)
                ClosePod(ent);
            else
                OpenPod(ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("bitrunning-netpod-prying", ("user", args.User)), ent, PopupType.MediumCaution);
        _audio.PlayPvs(PrySound, ent);
        if (TryComp<BitrunningPilotComponent>(Occupant(ent), out var pilot))
            Alert(pilot.Avatar, "BitrunningCrowbar");

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, PryTime, new NetpodPryDoAfterEvent(), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnPryDoAfter(Entity<NetpodComponent> ent, ref NetpodPryDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        SeverPod(ent);
        OpenPod(ent);
    }

    /// <summary>sever_connection().</summary>
    private void SeverPod(Entity<NetpodComponent> ent)
    {
        if (Occupant(ent) is { } body && TryComp<BitrunningPilotComponent>(body, out var pilot))
            Disconnect(pilot.Avatar, causeDamage: true);
    }

    private void OnNetpodPower(Entity<NetpodComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered && !ent.Comp.Open)
        {
            if (ent.Comp.Connected)
                SeverPod(ent);
            else
                OpenPod(ent);
        }

        UpdateNetpodAppearance(ent);
    }

    private void OnNetpodTerminating(Entity<NetpodComponent> ent, ref EntityTerminatingEvent args)
    {
        SeverPod(ent);
    }

    private void OnNetpodDamaged(Entity<NetpodComponent> ent, ref DamageChangedEvent args)
    {
        // on_damage_taken(): капсула сильно повреждена — пользователь получает предупреждение.
        if (!args.DamageIncreased || Occupant(ent) is not { } body || !TryComp<BitrunningPilotComponent>(body, out var pilot))
            return;

        if (_damage.GetTotalDamage((ent, args.Damageable)) >= 150)
            Alert(pilot.Avatar, "BitrunningIntegrity");
    }

    /// <summary>ui_interact(): окно образов открывается только у пустой работающей капсулы.</summary>
    private void OnNetpodUiAttempt(Entity<NetpodComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (Occupant(ent) != null || !_power.IsPowered(ent.Owner))
            args.Cancel();
    }

    private void OnNetpodUiOpened(Entity<NetpodComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateNetpodUi(ent);
    }

    /// <summary>ui_static_data(): make_outfit_collection("Jobs", subtypesof(/datum/outfit/job)).</summary>
    private void UpdateNetpodUi(Entity<NetpodComponent> ent)
    {
        var jobs = _proto.EnumeratePrototypes<JobPrototype>()
            .Where(j => j.SetPreference && j.StartingGear != null)
            .OrderBy(j => j.LocalizedName)
            .Select(j => new NetpodOutfitEntry(j.ID, j.LocalizedName))
            .ToList();

        _ui.SetUiState(ent.Owner, NetpodUiKey.Key, new NetpodOutfitState(ent.Comp.Job?.Id, jobs));
    }

    /// <summary>ui_act("select_outfit"): resolve_outfit() принимает только образ из списка.</summary>
    private void OnNetpodSelectOutfit(Entity<NetpodComponent> ent, ref NetpodSelectOutfitMessage args)
    {
        if (!_proto.TryIndex<JobPrototype>(args.Id, out var job) || !job.SetPreference || job.StartingGear == null)
            return;

        ent.Comp.Job = job.ID;
        Dirty(ent);
        UpdateNetpodUi(ent);
    }

    /// <summary>
    /// equipOutfit(netsuit): снаряжение профессии и её лоадаут по умолчанию — в SS14 комбинезон, обувь и рюкзак
    /// выдаёт именно лоадаут.
    /// </summary>
    private void EquipAvatarOutfit(EntityUid avatar, Entity<NetpodComponent> pod, BitrunningDomainPrototype domain)
    {
        if (domain.ForcedOutfit is { } forced)
        {
            _gear.EquipStartingGear(avatar, forced);
            return;
        }

        if (pod.Comp.Job is not { } jobId || !_proto.TryIndex(jobId, out var job))
        {
            _gear.EquipStartingGear(avatar, pod.Comp.Outfit);
            return;
        }

        var roleLoadout = LoadoutSystem.GetJobPrototype(job.ID);
        _loadout.Equip(avatar,
            job.StartingGear is { } gear ? new List<ProtoId<StartingGearPrototype>> { gear } : null,
            _proto.HasIndex<RoleLoadoutPrototype>(roleLoadout) ? new List<ProtoId<RoleLoadoutPrototype>> { roleLoadout } : null);
    }

    /// <summary>
    /// on_mind_transfer(): при превращении аватара (диск «дракон», «белый медведь») связь переезжает на новое тело.
    /// </summary>
    private void OnAvatarPolymorphed(Entity<BitrunningAvatarComponent> ent, ref Content.Shared.Polymorph.PolymorphedEvent args)
    {
        var old = ent.Comp;
        var next = args.NewEntity;
        if (!TryComp<MindComponent>(old.Mind, out var mind))
            return;

        if (mind.VisitingEntity == args.OldEntity)
            _mind.UnVisit(old.Mind, mind);

        var comp = EnsureComp<BitrunningAvatarComponent>(next);
        comp.Body = old.Body;
        comp.Mind = old.Mind;
        comp.Server = old.Server;
        comp.Netpod = old.Netpod;
        comp.Nohit = old.Nohit;
        comp.HelpText = old.HelpText;
        comp.BuzzStream = old.BuzzStream;
        comp.NextAmbience = old.NextAmbience;
        old.BuzzStream = null;
        EnsureComp<BitrunningAvatarMarkerComponent>(next);

        if (TryComp<QuantumServerComponent>(old.Server, out var server))
        {
            server.Avatars.Remove(args.OldEntity);
            server.Avatars.Add(next);
            _servers.SetBodycam(next, server.Broadcasting);
        }

        if (TryComp<NetpodComponent>(old.Netpod, out var pod))
            pod.Avatar = next;
        if (TryComp<BitrunningPilotComponent>(old.Body, out var pilot))
            pilot.Avatar = next;

        // Старое тело не должно оборвать связь при удалении.
        old.Disconnecting = true;
        RemComp<BitrunningAvatarComponent>(args.OldEntity);
        _mind.Visit(comp.Mind, next, mind);
    }

    private void OnNetpodAltVerbs(Entity<NetpodComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.CopyBody ? "bitrunning-netpod-scan-off" : "bitrunning-netpod-scan-on"),
            Act = () =>
            {
                ent.Comp.CopyBody = !ent.Comp.CopyBody;
                Dirty(ent);
                _popup.PopupEntity(Loc.GetString(ent.Comp.CopyBody ? "bitrunning-netpod-scan-enabled" : "bitrunning-netpod-scan-disabled"), ent, user);
            },
        });
    }

    private void OnNetpodExamined(Entity<NetpodComponent> ent, ref ExaminedEvent args)
    {
        if (_servers.FindServer(ent) == null)
        {
            args.PushMarkup(Loc.GetString("bitrunning-netpod-examine-no-server"));
            return;
        }

        args.PushMarkup(Loc.GetString("bitrunning-netpod-examine-help"));
        args.PushMarkup(Loc.GetString(ent.Comp.CopyBody ? "bitrunning-netpod-examine-scan-on" : "bitrunning-netpod-examine-scan-off"));
        if (Occupant(ent) is { } occupant)
            args.PushMarkup(Loc.GetString("bitrunning-netpod-examine-occupant", ("occupant", occupant)));
    }

    #endregion

    #region Аватар

    /// <summary>on_linked_damage(): урон аватара (кроме удушья) переходит на тело.</summary>
    private void OnAvatarDamaged(Entity<BitrunningAvatarComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Disconnecting || !args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        var linked = new DamageSpecifier();
        foreach (var (type, amount) in delta.DamageDict)
        {
            if (amount > 0 && !IgnoredDamage.Contains(type))
                linked.DamageDict[type] = amount;
        }

        if (linked.Empty)
            return;

        ent.Comp.Nohit = false;
        var total = linked.GetTotal().Float();
        _damage.TryChangeDamage(ent.Comp.Body, linked, ignoreResistances: true, interruptsDoAfters: false);

        if (total > 30 && _random.Prob(0.3f))
            Scream(ent.Comp.Body);

        if (!_mobs.IsAlive(ent.Comp.Body))
            Disconnect(ent, causeDamage: true);
    }

    private void OnAvatarMobState(Entity<BitrunningAvatarComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            Disconnect(ent, causeDamage: true);
    }

    private void OnAvatarTerminating(Entity<BitrunningAvatarComponent> ent, ref EntityTerminatingEvent args)
    {
        Disconnect(ent, causeDamage: true);
    }

    private void OnPilotMobState(Entity<BitrunningPilotComponent> ent, ref MobStateChangedEvent args)
    {
        // Пилот потерял сознание или умер — отключение.
        if (args.NewMobState != MobState.Alive)
            Disconnect(ent.Comp.Avatar, causeDamage: true);
    }

    private void OnSafeDisconnectAlert(Entity<BitrunningAvatarComponent> ent, ref BitrunningSafeDisconnectAlertEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Disconnect(ent, causeDamage: false);
    }

    private void OnDomainInfo(Entity<BitrunningAvatarComponent> ent, ref BitrunningDomainInfoActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        RaiseNetworkEvent(new BitrunningAvatarHelpEvent(ent.Comp.HelpText ?? Loc.GetString("bitrunning-domain-info-none")), actor.PlayerSession);
    }

    /// <summary>on_domain_completed(): звук и тревога с безопасным выходом.</summary>
    public void OnDomainCompleted(EntityUid avatar, int reward)
    {
        if (!TryComp<BitrunningAvatarComponent>(avatar, out var comp))
            return;

        _audio.PlayEntity(SuccessSound, avatar, avatar);
        _alerts.ShowAlert(avatar, CompletedAlert);
        RaiseLocalEvent(comp.Body, new BitrunningDomainCompletedEvent(reward));
    }

    /// <summary>Тревога аватару (crowbar, integrity, shutdown, threat, breach).</summary>
    public void Alert(EntityUid avatar, string alert)
    {
        if (TerminatingOrDeleted(avatar))
            return;

        _audio.PlayEntity(AlertSound, avatar, avatar);
        _alerts.ShowAlert(avatar, alert, autoRemove: true, cooldown: (_timing.CurTime, _timing.CurTime + TimeSpan.FromSeconds(10)));
    }

    /// <summary>get_avatar_data().</summary>
    public QuantumConsoleAvatarEntry? GetVitals(EntityUid avatar)
    {
        if (!TryComp<BitrunningAvatarComponent>(avatar, out var comp) || !TryComp<DamageableComponent>(avatar, out var damage))
            return null;

        var groups = _damage.GetDamagePerGroup((avatar, damage));
        float Group(string id) => groups.TryGetValue(id, out var value) ? value.Float() : 0f;
        var total = _damage.GetTotalDamage((avatar, damage)).Float();
        return new QuantumConsoleAvatarEntry(Name(avatar), Name(comp.Body), 100f - total, Group("Brute"), Group("Burn"), Group("Toxin"), Group("Airloss"));
    }

    /// <summary>on_domain_scrubbed(): брошенные аватары удаляются вместе с доменом.</summary>
    public void DeleteDomainAvatars(Entity<QuantumServerComponent> server)
    {
        var query = EntityQueryEnumerator<NetpodComponent>();
        while (query.MoveNext(out _, out var pod))
        {
            if (pod.Avatar is { } avatar && Transform(avatar).MapUid == server.Comp.DomainMap)
            {
                QueueDel(avatar);
                pod.Avatar = null;
            }
        }
    }

    private void OnHostMonitorUse(Entity<BitrunningHostMonitorComponent> ent, ref Content.Shared.Interaction.Events.UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!TryComp<BitrunningAvatarComponent>(args.User, out var avatar))
        {
            _popup.PopupEntity(Loc.GetString("bitrunning-host-monitor-no-data"), args.User, args.User);
            return;
        }

        var health = TryComp<DamageableComponent>(avatar.Body, out var damage)
            ? Math.Max(0f, 100f - _damage.GetTotalDamage((avatar.Body, damage)).Float())
            : 0f;
        _popup.PopupEntity(Loc.GetString("bitrunning-host-monitor-health", ("health", (int) health)), args.User, args.User);
    }

    private void Scream(EntityUid body)
    {
        _chatSystem.TryEmoteWithChat(body, "Scream", ignoreActionBlocker: true);
    }

    #endregion
}

/// <summary>Домен пройден, пока тело было подключено — для начисления очков битраннинга (Б2).</summary>
public sealed class BitrunningDomainCompletedEvent(int reward) : EntityEventArgs
{
    public readonly int Reward = reward;
}
