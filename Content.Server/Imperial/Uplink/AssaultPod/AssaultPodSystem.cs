using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Station.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Administration;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Uplink.AssaultPod;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Uplink.AssaultPod;

/// <summary>
/// Штурмовые поды (Paradise, modules/shuttle/assault_pod.dm) в виде десантных капсул:
/// устройство наведения выбирает маяк станции, капсула летит к нему и садится прямо внутри станции.
/// </summary>
public sealed class AssaultPodSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly Content.Server.Administration.Logs.IAdminLogManager _adminLog = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly AssaultPodEffectsSystem _effects = default!;

    private static readonly SoundSpecifier AlarmSound = new SoundPathSpecifier("/Audio/Machines/Nuke/general_beep.ogg");
    private static readonly SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/Imperial/Uplink/podwoosh.ogg");
    private static readonly SoundSpecifier LandSound = new SoundPathSpecifier("/Audio/Effects/explosion1.ogg");
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    private const int MaxLandingSearch = 4;

    private VerbCategory? _landingCategory;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AssaultPodTargeterComponent, GetVerbsEvent<Verb>>(OnTargeterVerbs);
        SubscribeLocalEvent<AssaultPodTargeterComponent, UseInHandEvent>(OnTargeterUse);

        SubscribeLocalEvent<AssaultPodComponent, GetVerbsEvent<AlternativeVerb>>(OnPodVerbs);
        SubscribeLocalEvent<AssaultPodComponent, ExaminedEvent>(OnPodExamined);
        SubscribeLocalEvent<AssaultPodTargeterComponent, ExaminedEvent>(OnTargeterExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AssaultPodComponent>();
        while (query.MoveNext(out var uid, out var pod))
        {
            if (pod.State == AssaultPodState.Launching)
                _effects.UpdateCountdown(uid, pod.NextStage, grid: false);

            if (pod.State == AssaultPodState.Flying && _effects.ShouldBeginArrival(uid, pod.NextStage))
                BeginArrival((uid, pod));

            if (now < pod.NextStage)
                continue;

            switch (pod.State)
            {
                case AssaultPodState.Launching:
                    TakeOff((uid, pod), now);
                    break;
                case AssaultPodState.Flying:
                    Land((uid, pod));
                    break;
            }
        }
    }

    #region Устройство наведения

    private void OnTargeterExamined(Entity<AssaultPodTargeterComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.TargetName == null
            ? Loc.GetString("assault-pod-no-zone-selected")
            : Loc.GetString("assault-pod-examine-zone", ("zone", ent.Comp.TargetName)));
    }

    /// <summary>Маяки навигации на станциях — возможные зоны высадки.</summary>
    public List<(EntityUid Beacon, string Name)> GetLandingZones()
    {
        var result = new List<(EntityUid, string)>();
        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beacon, out var xform))
        {
            if (!beacon.Enabled || xform.GridUid == null || _station.GetOwningStation(uid, xform) == null)
                continue;

            var name = string.IsNullOrWhiteSpace(beacon.Text) ? Name(uid) : beacon.Text;
            result.Add((uid, name));
        }

        return result.OrderBy(z => z.Item2).ToList();
    }

    /// <summary>activate_self(): выбор зоны высадки из списка маяков.</summary>
    private void OnTargeterVerbs(Entity<AssaultPodTargeterComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_hands.IsHolding(args.User, ent))
            return;

        _landingCategory ??= new VerbCategory("assault-pod-verb-category", null);
        var user = args.User;
        foreach (var (beacon, name) in GetLandingZones())
        {
            args.Verbs.Add(new Verb
            {
                Text = name,
                Category = _landingCategory,
                Act = () =>
                {
                    ent.Comp.Target = beacon;
                    ent.Comp.TargetName = name;
                    _popup.PopupEntity(Loc.GetString("assault-pod-zone-set", ("zone", name)), user, user);
                },
            });
        }
    }

    /// <summary>Развернуть капсулу под ногами, если зона высадки уже выбрана.</summary>
    private void OnTargeterUse(Entity<AssaultPodTargeterComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;

        if (ent.Comp.Target is not { } target || TerminatingOrDeleted(target))
        {
            _popup.PopupEntity(GetLandingZones().Count == 0
                    ? Loc.GetString("assault-pod-no-zones")
                    : Loc.GetString("assault-pod-no-zone-selected"),
                user,
                user);
            return;
        }

        var xform = Transform(user);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            _popup.PopupEntity(Loc.GetString("assault-pod-need-floor"), user, user);
            return;
        }

        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        var pod = Spawn(ent.Comp.Pod, _map.GridTileToLocal(grid, gridComp, tile));
        _transform.AnchorEntity(pod);

        var podComp = EnsureComp<AssaultPodComponent>(pod);
        podComp.Target = target;
        podComp.TargetName = ent.Comp.TargetName;

        _popup.PopupEntity(Loc.GetString("assault-pod-deployed", ("zone", ent.Comp.TargetName ?? string.Empty)), user, user);
        QueueDel(ent);
    }

    #endregion

    #region Капсула

    private void OnPodExamined(Entity<AssaultPodComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.State == AssaultPodState.Landed
            ? Loc.GetString("assault-pod-examine-landed")
            : Loc.GetString("assault-pod-examine-zone", ("zone", ent.Comp.TargetName ?? "?")));
    }

    private void OnPodVerbs(Entity<AssaultPodComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.State != AssaultPodState.Ready)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("assault-pod-launch-verb", ("zone", ent.Comp.TargetName ?? "?")),
            Priority = 2,
            Act = () => StartLaunch(ent, user),
        });
    }

    /// <summary>Сбросить капсулу без десанта сразу в полёт к маяку (админ-команда).</summary>
    public EntityUid DropPod(EntProtoId proto, EntityUid? beacon, string? beaconName)
    {
        var pod = Spawn(proto, GetTransitSpot());
        var comp = EnsureComp<AssaultPodComponent>(pod);
        comp.Target = beacon;
        comp.TargetName = beaconName;
        comp.State = AssaultPodState.Flying;
        comp.NextStage = _timing.CurTime + comp.FlightTime;
        Dirty(pod, comp);
        _effects.StartTravel(pod, grid: false);
        return pod;
    }

    /// <summary>
    /// Точка в гиперпространстве (та же карта, что у FTL шаттлов), далеко от трасс шаттлов.
    /// В nullspace капсулу отправлять нельзя: у десанта внутри камера оказывается вне карты.
    /// </summary>
    public MapCoordinates GetTransitSpot()
    {
        var map = EnsureTransitMap();
        var offset = new Vector2(_random.NextFloat(-30000f, -20000f), _random.NextFloat(-30000f, -20000f));
        return new MapCoordinates(offset, Transform(map).MapID);
    }

    private EntityUid EnsureTransitMap()
    {
        var query = AllEntityQuery<FTLMapComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            return uid;
        }

        var mapUid = _map.CreateMap(out _);
        var ftlMap = AddComp<FTLMapComponent>(mapUid);
        _metaData.SetEntityName(mapUid, "FTL");
        EnsureComp<ParallaxComponent>(mapUid).Parallax = ftlMap.Parallax;
        return mapUid;
    }

    /// <summary>Отсчёт: двери закрываются и запираются, кто не успел — остаётся.</summary>
    public void StartLaunch(Entity<AssaultPodComponent> ent, EntityUid? user)
    {
        if (ent.Comp.State != AssaultPodState.Ready)
            return;

        _entityStorage.CloseStorage(ent);
        ent.Comp.State = AssaultPodState.Launching;
        ent.Comp.NextStage = _timing.CurTime + ent.Comp.Countdown;
        Dirty(ent);

        _audio.PlayPvs(AlarmSound, ent);
        _popup.PopupEntity(Loc.GetString("assault-pod-countdown", ("seconds", (int) ent.Comp.Countdown.TotalSeconds)), ent, PopupType.LargeCaution);
    }

    private void TakeOff(Entity<AssaultPodComponent> ent, TimeSpan now)
    {
        _entityStorage.CloseStorage(ent);
        _audio.PlayPvs(LaunchSound, ent);

        ent.Comp.State = AssaultPodState.Flying;
        ent.Comp.NextStage = now + ent.Comp.FlightTime;
        Dirty(ent);
        _effects.StartTravel(ent, grid: false);

        // Капсула летит через гиперпространство вместе с десантом до самой посадки.
        var xform = Transform(ent);
        if (xform.Anchored)
            _transform.Unanchor(ent, xform);

        _transform.SetMapCoordinates((ent, xform), GetTransitSpot());
    }

    /// <summary>За 5 с до посадки: место выбрано заранее, на нём загорается метка.</summary>
    private void BeginArrival(Entity<AssaultPodComponent> ent)
    {
        var spots = new List<EntityCoordinates>();
        if (ResolveLandingTarget(ent) is { } target && TryFindLandingSpot(target, out var spot))
        {
            ent.Comp.LandingSpot = spot;
            spots.Add(spot);
        }

        _effects.BeginArrival(ent, false, spots, ent.Comp.NextStage);
    }

    private EntityUid? ResolveLandingTarget(Entity<AssaultPodComponent> ent)
    {
        if (ent.Comp.Target is { } target && !TerminatingOrDeleted(target))
            return target;

        var zones = GetLandingZones();
        if (zones.Count == 0)
            return null;

        ent.Comp.Target = _random.Pick(zones).Beacon;
        return ent.Comp.Target;
    }

    /// <summary>dock(): посадка у маяка с грохотом; всё вокруг сбито с ног, под капсулой — раздавлено.</summary>
    private void Land(Entity<AssaultPodComponent> ent)
    {
        // Садимся туда, где горела метка: десант видел её, а экипаж был предупреждён.
        EntityCoordinates coords = default;
        var found = ent.Comp.LandingSpot is { } marked && !TerminatingOrDeleted(marked.EntityId);
        if (found)
            coords = ent.Comp.LandingSpot!.Value;
        else if (ResolveLandingTarget(ent) is { } target)
            found = TryFindLandingSpot(target, out coords);

        if (!found)
        {
            // Садиться некуда — через 5 с новая попытка с новой меткой; случайный маяк выбираем заново.
            if (ent.Comp.TargetName == null)
                ent.Comp.Target = null;
            ent.Comp.LandingSpot = null;
            _effects.ResetArrival(ent);
            ent.Comp.NextStage = _timing.CurTime + TimeSpan.FromSeconds(5);
            return;
        }

        _transform.SetCoordinates(ent, coords);
        _transform.AnchorEntity(ent);

        ent.Comp.State = AssaultPodState.Landed;
        Dirty(ent);

        Log.Info($"Штурмовая капсула {ToPrettyString(ent)} приземлилась у маяка {ent.Comp.TargetName} в {coords}");
        _adminLog.Add(LogType.Action, LogImpact.High, $"Штурмовая капсула {ToPrettyString(ent):pod} приземлилась у маяка {ent.Comp.TargetName} в {coords}");
        _audio.PlayPvs(LandSound, ent);
        CrushLandingSite(ent, coords);
        _effects.Landed(ent, coords);
        _entityStorage.OpenStorage(ent);
    }

    /// <summary>Клетка у маяка, не занятая стенами и машинами; если занята — ближайшая свободная.</summary>
    private bool TryFindLandingSpot(EntityUid beacon, out EntityCoordinates coords)
    {
        coords = default;
        var xform = Transform(beacon);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var origin = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        for (var radius = 0; radius <= MaxLandingSearch; radius++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                        continue;

                    var indices = origin + new Vector2i(x, y);
                    if (!_map.TryGetTileRef(grid, gridComp, indices, out var tile)
                        || _turf.IsSpace(tile)
                        || _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                        continue;

                    coords = _map.GridTileToLocal(grid, gridComp, indices);
                    return true;
                }
            }
        }

        return false;
    }

    private void CrushLandingSite(Entity<AssaultPodComponent> ent, EntityCoordinates coords)
    {
        foreach (var other in _lookup.GetEntitiesInRange(coords, ent.Comp.KnockdownRange))
        {
            if (other == ent.Owner)
                continue;

            if (HasComp<MobStateComponent>(other))
            {
                _stun.TryKnockdown(other, ent.Comp.Knockdown);
                if (_transform.InRange(Transform(other).Coordinates, coords, 0.75f))
                {
                    var crush = new DamageSpecifier();
                    crush.DamageDict["Blunt"] = ent.Comp.CrushDamage;
                    _damageable.TryChangeDamage(other, crush, origin: ent);
                }

                continue;
            }

            // Мебель и машины на самой клетке посадки сминаются.
            if (Transform(other).Anchored
                && !_tag.HasTag(other, WallTag)
                && _transform.InRange(Transform(other).Coordinates, coords, 0.5f))
            {
                var damage = new DamageSpecifier();
                damage.DamageDict["Structural"] = ent.Comp.StructureDamage;
                damage.DamageDict["Blunt"] = ent.Comp.StructureDamage;
                _damageable.TryChangeDamage(other, damage, origin: ent);
            }
        }
    }

    #endregion
}

/// <summary>Админ-команда: выдать устройство наведения штурмового пода (ЯО, ОБР, эскадрон смерти).</summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class AssaultPodCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    private static readonly Dictionary<string, string> Pods = new()
    {
        ["syndicate"] = "ImperialAssaultPodSyndicate",
        ["ert"] = "ImperialAssaultPodErt",
        ["deathsquad"] = "ImperialAssaultPodDeathSquad",
    };

    private static readonly Dictionary<string, string> Targeters = new()
    {
        ["syndicate"] = "ImperialAssaultPodTargeterSyndicate",
        ["ert"] = "ImperialAssaultPodTargeterErt",
        ["deathsquad"] = "ImperialAssaultPodTargeterDeathSquad",
    };

    public string Command => "assaultpod";
    public string Description => Loc.GetString("assault-pod-command-description");
    public string Help => "assaultpod <ert|deathsquad|syndicate> [игрок] — выдать устройство наведения; assaultpod drop <тип> [маяк] — сбросить пустую капсулу; assaultpod ship <тип> [x y карта] — большой штурмовой под (отдельный грид) рядом с вами; assaultpod launch <грид> [маяк] — запустить большой под";

    private void Launch(IConsoleShell shell, string[] args)
    {
        EntityUid? grid = null;
        if (args[1] != "all")
        {
            if (!NetEntity.TryParse(args[1], out var netGrid) || !_entManager.TryGetEntity(netGrid, out grid))
            {
                shell.WriteError(Help);
                return;
            }
        }

        var name = args.Length > 2 ? string.Join(" ", args[2..]) : null;
        shell.WriteLine(_entManager.System<AssaultPodShipSystem>().LaunchFromCommand(grid, name));
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length >= 2 && args[0] == "ship")
        {
            Ship(shell, args);
            return;
        }

        if (args.Length >= 2 && args[0] == "drop")
        {
            Drop(shell, args);
            return;
        }

        if (args.Length >= 2 && args[0] == "launch")
        {
            Launch(shell, args);
            return;
        }

        if (args.Length < 1 || !Targeters.TryGetValue(args[0], out var proto))
        {
            shell.WriteError(Help);
            return;
        }

        EntityUid? target = shell.Player?.AttachedEntity;
        if (args.Length > 1)
        {
            if (!_players.TryGetSessionByUsername(args[1], out var session) || session.AttachedEntity == null)
            {
                shell.WriteError(Loc.GetString("assault-pod-command-no-player", ("player", args[1])));
                return;
            }

            target = session.AttachedEntity;
        }

        if (target == null)
        {
            shell.WriteError(Help);
            return;
        }

        var item = _entManager.SpawnEntity(proto, _entManager.GetComponent<TransformComponent>(target.Value).Coordinates);
        _entManager.System<SharedHandsSystem>().PickupOrDrop(target.Value, item);
        shell.WriteLine(Loc.GetString("assault-pod-command-given", ("player", target.Value)));
    }

    private void Ship(IConsoleShell shell, string[] args)
    {
        AssaultPodShipStyle? style = args[1] switch
        {
            "syndicate" => AssaultPodShipStyle.Syndicate,
            "ert" => AssaultPodShipStyle.Ert,
            "deathsquad" => AssaultPodShipStyle.DeathSquad,
            _ => null,
        };

        if (style == null)
        {
            shell.WriteError(Help);
            return;
        }

        MapCoordinates coords;
        if (args.Length >= 5
            && float.TryParse(args[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
            && float.TryParse(args[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)
            && int.TryParse(args[4], out var map))
        {
            coords = new MapCoordinates(x, y, new MapId(map));
        }
        else if (shell.Player?.AttachedEntity is { } admin)
        {
            coords = _entManager.System<TransformSystem>().GetMapCoordinates(admin);
        }
        else
        {
            shell.WriteError(Help);
            return;
        }

        var grid = _entManager.System<AssaultPodShipSystem>().SpawnShip(style.Value, coords);
        shell.WriteLine(Loc.GetString("assault-pod-command-ship", ("grid", grid)));
    }

    private void Drop(IConsoleShell shell, string[] args)
    {
        if (!Pods.TryGetValue(args[1], out var proto))
        {
            shell.WriteError(Help);
            return;
        }

        var system = _entManager.System<AssaultPodSystem>();
        var zones = system.GetLandingZones();
        if (zones.Count == 0)
        {
            shell.WriteError(Loc.GetString("assault-pod-no-zones"));
            return;
        }

        var zone = zones[0];
        if (args.Length > 2)
        {
            var name = string.Join(" ", args[2..]);
            var found = zones.FindIndex(z => z.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (found < 0)
            {
                shell.WriteError(Loc.GetString("assault-pod-command-no-zone", ("zone", name)));
                return;
            }

            zone = zones[found];
        }
        else
        {
            zone = IoCManager.Resolve<Robust.Shared.Random.IRobustRandom>().Pick(zones);
        }

        system.DropPod(proto, zone.Beacon, zone.Name);
        shell.WriteLine(Loc.GetString("assault-pod-command-dropped", ("zone", zone.Name)));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromOptions(Targeters.Keys.Append("drop").Append("ship")),
            2 when args[0] is "drop" or "ship" => CompletionResult.FromOptions(Pods.Keys),
            2 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "<игрок>"),
            _ => CompletionResult.Empty,
        };
    }
}
