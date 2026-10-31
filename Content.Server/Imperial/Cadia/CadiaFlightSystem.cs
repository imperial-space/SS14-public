using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Imperial.ZLevels;
using Content.Server.Popups;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Timing;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Чей корабль: ставится на сетку при постройке по планировке.</summary>
[RegisterComponent]
public sealed partial class CadiaShipFactionComponent : Component
{
    [DataField]
    public CadiaFaction Faction;
}

/// <summary>
/// Шаттлы над Кадией на ванильном FTL. Этажи стопки — точки назначения в карте консоли:
/// на соседний этаж вместо прыжка идёт короткий вертикальный перелёт (взлёт/посадка), в космос — только с орбиты.
/// Правила проверяются до ванильной консоли и отменяются через <see cref="ConsoleFTLAttemptEvent"/>.
/// Шаттл в небе без двигателей или консоли падает на землю и разбивается.
/// </summary>
public sealed class CadiaFlightSystem : EntitySystem
{
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly CadiaPylonVeilSystem _veil = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly Robust.Shared.Physics.Systems.SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly Robust.Shared.Random.IRobustRandom _random = default!;
    [Dependency] private readonly Content.Shared.Camera.SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly Robust.Server.GameStates.PvsOverrideSystem _pvs = default!;

    /// <summary>Разгон и полёт вертикального перелёта (обычный FTL — 5.5 и 20 секунд).</summary>
    private const float StartupTime = 3.5f;
    private const float FlightTime = 4f;
    private static readonly TimeSpan VerticalCooldown = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FallWarning = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan CheckRate = TimeSpan.FromSeconds(1);

    private static readonly SoundSpecifier TakeoffSound = new SoundPathSpecifier("/Audio/Effects/shuttle_thruster.ogg", AudioParams.Default.WithVolume(4));
    private static readonly SoundSpecifier FlightSound = new SoundPathSpecifier("/Audio/Imperial/DeadSector/Misc/Helicopter/Flying.ogg");
    private static readonly SoundSpecifier LandingSound = new SoundPathSpecifier("/Audio/Magic/rumble.ogg");
    private static readonly SoundSpecifier FallAlarm = new SoundPathSpecifier("/Audio/Imperial/Mission_Blackout/A_Alarm2.ogg");
    private static readonly SoundSpecifier CrashSound = new SoundCollectionSpecifier("ShuttleImpactSound", AudioParams.Default.WithVolume(8));

    private TimeSpan _nextCheck;
    private readonly HashSet<EntityUid> _hits = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextDenyPopup = new();
    private static readonly TimeSpan DenyPopupDelay = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleConsoleComponent, MapInitEvent>(OnConsoleMapInit);
        SubscribeLocalEvent<ShuttleConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnConsoleVerbs);
        SubscribeLocalEvent<CadiaFlightConsoleComponent, ShuttleConsoleFTLPositionMessage>(OnPositionRequest, before: new[] { typeof(ShuttleConsoleSystem) });
        SubscribeLocalEvent<CadiaFlightConsoleComponent, ShuttleConsoleFTLBeaconMessage>(OnBeaconRequest, before: new[] { typeof(ShuttleConsoleSystem) });
        SubscribeLocalEvent<ConsoleFTLAttemptEvent>(OnFTLAttempt);
        SubscribeLocalEvent<CadiaVerticalFlightComponent, FTLRequestEvent>(OnVerticalRequest);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<ZLevelsRelinkedEvent>(OnRelinked);
        SubscribeLocalEvent<CadiaFlightLevelComponent, ComponentStartup>(OnLevelStartup);
        SubscribeLocalEvent<CadiaFlightLevelComponent, MapInitEvent>(OnLevelMapInit);
    }

    // ───────────────────────── Этажи поля ─────────────────────────

    /// <summary>
    /// Карта этажом выше (delta &gt; 0) или ниже (delta &lt; 0) в том же поле Кадии: поверхность → небо → орбита.
    /// Своя связка не зависит от связей стопки Z-уровней; для карт без поля — по стопке.
    /// </summary>
    public EntityUid? GetLevelMap(EntityUid map, int delta)
    {
        if (TryComp<CadiaFlightLevelComponent>(map, out var from) && !string.IsNullOrEmpty(from.Field))
        {
            var wanted = (int) from.Level + delta;
            var query = EntityQueryEnumerator<CadiaFlightLevelComponent>();
            while (query.MoveNext(out var uid, out var level))
            {
                if (level.Field == from.Field && (int) level.Level == wanted && !TerminatingOrDeleted(uid))
                    return uid;
            }
            return null;
        }

        var current = (EntityUid?) map;
        for (var i = 0; i < Math.Abs(delta) && current != null; i++)
            current = _z.GetAdjacentMap(current.Value, delta > 0);
        return current;
    }

    // ───────────────────────── Правила ─────────────────────────
    /// <summary>
    /// Консоль шаттла показывает только карты, известные клиенту, а сервер отправляет игроку лишь карту, где он стоит.
    /// Этажи Кадии отправляем всем всегда (сама карта, без содержимого) — иначе их нет в списке назначений.
    /// </summary>
    private void OnLevelStartup(Entity<CadiaFlightLevelComponent> ent, ref ComponentStartup args)
    {
        _pvs.AddForceSend(ent);
    }

    /// <summary>
    /// В карте консоли переключиться на другую карту можно только кликом по объекту на ней (кораблю или маяку).
    /// Небо и орбита пустые — ставим на каждый этаж навигационный маяк, если маппер не поставил свой.
    /// </summary>
    private void OnLevelMapInit(Entity<CadiaFlightLevelComponent> ent, ref MapInitEvent args)
    {
        var beacons = EntityQueryEnumerator<FTLBeaconComponent, TransformComponent>();
        while (beacons.MoveNext(out _, out var xform))
        {
            if (xform.MapUid == ent.Owner)
                return;
        }

        var beacon = Spawn("FTLPoint", new EntityCoordinates(ent, Vector2.Zero));
        // Маяк называется по карте — в списке консоли сразу видно, какое это поле и какой этаж.
        _meta.SetEntityName(beacon, Loc.GetString("cadia-flight-beacon", ("level", Name(ent))));
    }

    private void OnConsoleMapInit(Entity<ShuttleConsoleComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<CadiaFlightConsoleComponent>(ent);
    }

    private void OnPositionRequest(Entity<CadiaFlightConsoleComponent> ent, ref ShuttleConsoleFTLPositionMessage args)
    {
        Check(ent, args.Actor, new MapCoordinates(args.Coordinates.Position, args.Coordinates.MapId), args.Angle);
    }

    private void OnBeaconRequest(Entity<CadiaFlightConsoleComponent> ent, ref ShuttleConsoleFTLBeaconMessage args)
    {
        var beacon = GetEntity(args.Beacon);
        if (!Exists(beacon))
            return;
        Check(ent, args.Actor, _xform.GetMapCoordinates(beacon), args.Angle);
    }

    /// <summary>
    /// Проверяет прыжок до ванильной консоли. Запрет — всплывающее сообщение пилоту и отмена в CanFTL;
    /// перелёт на соседний этаж помечается, чтобы ванильный FTL прошёл как короткий взлёт или посадка.
    /// </summary>
    private void Check(EntityUid console, EntityUid pilot, MapCoordinates target, Angle angle)
    {
        if (Transform(console).GridUid is not { } shuttle || !HasComp<ShuttleComponent>(shuttle) || HasComp<MapComponent>(shuttle))
            return;

        RemComp<CadiaFlightBlockComponent>(shuttle);
        RemComp<CadiaVerticalFlightComponent>(shuttle);

        var fromMap = Transform(shuttle).MapUid;
        var toMap = _map.GetMapOrInvalid(target.MapId);
        if (fromMap == null || !toMap.IsValid())
            return;

        var fromLevel = CompOrNull<CadiaFlightLevelComponent>(fromMap.Value);
        var toLevel = CompOrNull<CadiaFlightLevelComponent>(toMap);
        if (fromLevel == null && toLevel == null)
            return;

        // Отказ от прошлого клика по карте прыжков не должен мешать прямой команде.
        RemComp<CadiaFlightBlockComponent>(shuttle);
        string? reason = null;
        if (fromMap == toMap)
        {
            reason = "cadia-flight-same-level";
        }
        else if (fromLevel != null && toLevel != null)
        {
            if (GetLevelMap(fromMap.Value, 1) != toMap && GetLevelMap(fromMap.Value, -1) != toMap)
                reason = "cadia-flight-not-adjacent";
            else if (PhaseReason(shuttle, fromLevel.Level, toLevel.Level) is { } phase)
                reason = phase;
            else
                Mark(shuttle, GetLevelMap(fromMap.Value, 1) == toMap, toLevel.Level);
        }
        else if (fromLevel != null)
        {
            if (fromLevel.Level != CadiaFlightLevel.Orbit)
                reason = "cadia-flight-space-from-orbit";
        }
        else if (toLevel!.Level != CadiaFlightLevel.Orbit)
        {
            reason = "cadia-flight-arrive-orbit";
        }

        // Посадка: площадка под шаттлом должна быть свободна от стен и построек.
        if (reason == null && toLevel?.Level == CadiaFlightLevel.Surface && !LandingFree(shuttle, target, angle))
            reason = "cadia-flight-landing-blocked";

        if (reason == null)
            return;

        RemComp<CadiaVerticalFlightComponent>(shuttle);
        var block = EnsureComp<CadiaFlightBlockComponent>(shuttle);
        block.Reason = Loc.GetString(reason, ("from", Name(fromMap.Value)), ("to", Name(toMap)));
        // Клиент шлёт запрос на каждый клик по карте — не засыпаем пилота одинаковыми отказами.
        var now = _timing.CurTime;
        if (_nextDenyPopup.TryGetValue(pilot, out var next) && now < next)
            return;
        _nextDenyPopup[pilot] = now + DenyPopupDelay;
        _popup.PopupEntity(block.Reason, pilot, pilot, PopupType.MediumCaution);
        _audio.PlayEntity(new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg"), Filter.Entities(pilot), console, false);
    }

    /// <summary>
    /// Прямые команды консоли над Кадией: «Взлёт» и «Снижение» — перелёт на соседний этаж в ту же точку,
    /// без карты прыжков. Посадка проверяется так же, как при прыжке через карту.
    /// </summary>
    private void OnConsoleVerbs(Entity<ShuttleConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || Transform(ent).GridUid is not { } shuttle ||
            !HasComp<ShuttleComponent>(shuttle) || HasComp<MapComponent>(shuttle) ||
            Transform(shuttle).MapUid is not { } map || !HasComp<CadiaFlightLevelComponent>(map))
            return;

        var user = args.User;
        foreach (var (delta, key, priority) in new[] { (1, "cadia-flight-verb-up", 3), (-1, "cadia-flight-verb-down", 2) })
        {
            if (GetLevelMap(map, delta) is not { } target)
                continue;
            var d = delta;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(key, ("level", Name(target))),
                Priority = priority,
                Act = () => VerticalFlight(ent, user, shuttle, target, d > 0),
            });
        }
    }

    private void VerticalFlight(EntityUid console, EntityUid pilot, EntityUid shuttle, EntityUid target, bool up)
    {
        if (!TryComp<ShuttleComponent>(shuttle, out var shuttleComp) || !shuttleComp.Enabled ||
            !TryComp<CadiaFlightLevelComponent>(target, out var targetLevel) || !TryComp<MapGridComponent>(shuttle, out var grid))
            return;

        // Отказ от прошлого клика по карте прыжков не должен мешать прямой команде.
        RemComp<CadiaFlightBlockComponent>(shuttle);
        string? reason = null;
        if (!_shuttle.CanFTL(shuttle, out var ftlReason))
            reason = ftlReason;
        if (reason == null && CompOrNull<CadiaFlightLevelComponent>(Transform(shuttle).MapUid) is { } fromLevel &&
            PhaseReason(shuttle, fromLevel.Level, targetLevel.Level) is { } phase)
            reason = Loc.GetString(phase);

        var xform = Transform(shuttle);
        var (position, rotation) = _xform.GetWorldPositionRotation(xform);
        var center = Vector2.Transform(grid.LocalAABB.Center, _xform.GetWorldMatrix(xform));
        var targetId = Comp<MapComponent>(target).MapId;
        if (reason == null && targetLevel.Level == CadiaFlightLevel.Surface && !LandingFree(shuttle, new MapCoordinates(center, targetId), rotation))
            reason = Loc.GetString("cadia-flight-landing-blocked");

        if (reason != null)
        {
            _popup.PopupEntity(reason, pilot, pilot, PopupType.MediumCaution);
            _audio.PlayEntity(new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg"), Filter.Entities(pilot), console, false);
            return;
        }

        Mark(shuttle, up, targetLevel.Level);
        _shuttle.FTLToCoordinates(shuttle, shuttleComp, new EntityCoordinates(target, position), rotation);
    }

    /// <summary>
    /// Что запрещает перелёт по ходу войны (ключ локализации) или null. Пока держится завеса пилонов, сквозь небо над
    /// Кадией не пройти ни вниз, ни вверх. Боевые корабли Хаоса — с пушками и бомбоотсеками — спускаются с орбиты только
    /// во время обстрела; корабли флота Империума ходят свободно.
    /// </summary>
    private string? PhaseReason(EntityUid shuttle, CadiaFlightLevel from, CadiaFlightLevel to)
    {
        if ((from == CadiaFlightLevel.Orbit || to == CadiaFlightLevel.Orbit) && _veil.Active())
            return "cadia-veil-sky";
        if (from != CadiaFlightLevel.Orbit || to >= from || CompOrNull<CadiaShipFactionComponent>(shuttle)?.Faction == CadiaFaction.Imperium ||
            !IsArmed(shuttle))
            return null;
        var wars = EntityQueryEnumerator<CadiaWarStateComponent>();
        while (wars.MoveNext(out var war))
        {
            if (war.Phase != CadiaWarPhase.Bombardment)
                return "cadia-flight-armed-bombardment";
        }
        return null;
    }

    /// <summary>Боевой корабль: на нём стоит пушка или бомбоотсек.</summary>
    private bool IsArmed(EntityUid shuttle)
    {
        var guns = EntityQueryEnumerator<CadiaShipGunComponent, TransformComponent>();
        while (guns.MoveNext(out _, out var xform))
        {
            if (xform.GridUid == shuttle)
                return true;
        }
        var bays = EntityQueryEnumerator<CadiaShipBombBayComponent, TransformComponent>();
        while (bays.MoveNext(out _, out var xform))
        {
            if (xform.GridUid == shuttle)
                return true;
        }
        return false;
    }

    private void Mark(EntityUid shuttle, bool up, CadiaFlightLevel to)
    {
        var mark = EnsureComp<CadiaVerticalFlightComponent>(shuttle);
        mark.Up = up;
        mark.To = to;
    }

    private bool LandingFree(EntityUid shuttle, MapCoordinates target, Angle angle)
    {
        if (!TryComp<MapGridComponent>(shuttle, out var grid))
            return true;
        var box = grid.LocalAABB;
        var world = new Box2Rotated(box.Translated(target.Position - box.Center), angle, target.Position).CalcBoundingBox();
        foreach (var ent in _lookup.GetEntitiesIntersecting(target.MapId, world.Enlarged(-0.1f), LookupFlags.Static))
        {
            if (!TryComp<FixturesComponent>(ent, out var fixtures) || !TryComp<PhysicsComponent>(ent, out var body) || !body.CanCollide)
                continue;
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (fixture.Hard && (fixture.CollisionLayer & (int) (CollisionGroup.Impassable | CollisionGroup.HighImpassable)) != 0)
                    return false;
            }
        }
        return true;
    }

    private void OnFTLAttempt(ref ConsoleFTLAttemptEvent ev)
    {
        if (ev.Cancelled || !TryComp<CadiaFlightBlockComponent>(ev.Uid, out var block))
            return;
        ev.Cancelled = true;
        ev.Reason = block.Reason;
        RemCompDeferred<CadiaFlightBlockComponent>(ev.Uid);
    }

    // ───────────────────────── Вертикальный перелёт ─────────────────────────

    private void OnVerticalRequest(Entity<CadiaVerticalFlightComponent> ent, ref FTLRequestEvent args)
    {
        if (!TryComp<FTLComponent>(ent, out var ftl))
            return;

        // Вместо гиперпространственного прыжка — короткий набор высоты или снижение.
        ftl.StartupTime = StartupTime;
        ftl.TravelTime = _shuttle.DefaultArrivalTime + FlightTime;
        ftl.StateTime = StartEndTime.FromStartDuration(_timing.CurTime, TimeSpan.FromSeconds(StartupTime));
        ftl.TravelSound = FlightSound;
        _audio.Stop(ftl.StartupStream);
        var audio = _audio.PlayPvs(TakeoffSound, ent);
        _audio.SetGridAudio(audio);
        ftl.StartupStream = audio?.Entity;

        var text = Loc.GetString(ent.Comp.Up ? "cadia-flight-takeoff" : "cadia-flight-descent",
            ("level", Loc.GetString($"cadia-flight-level-{ent.Comp.To.ToString().ToLowerInvariant()}")));
        PopupOnGrid(ent, text, PopupType.Large);
    }

    private void OnFTLCompleted(ref FTLCompletedEvent ev)
    {
        if (!TryComp<CadiaVerticalFlightComponent>(ev.Entity, out var mark))
            return;

        if (TryComp<FTLComponent>(ev.Entity, out var ftl))
            ftl.StateTime = StartEndTime.FromCurTime(_timing, VerticalCooldown);

        var landed = mark.To == CadiaFlightLevel.Surface;
        _audio.PlayPvs(LandingSound, ev.Entity);
        PopupOnGrid(ev.Entity, Loc.GetString(landed ? "cadia-flight-landed" : "cadia-flight-arrived",
            ("level", Loc.GetString($"cadia-flight-level-{mark.To.ToString().ToLowerInvariant()}"))), PopupType.Large);
        RemComp<CadiaVerticalFlightComponent>(ev.Entity);
    }

    private void PopupOnGrid(EntityUid grid, string text, PopupType type)
    {
        var filter = Filter.Empty().AddWhereAttachedEntity(e => Transform(e).GridUid == grid);
        foreach (var session in filter.Recipients)
        {
            if (session.AttachedEntity is { } mob)
                _popup.PopupEntity(text, mob, mob, type);
        }
    }

    // ───────────────────────── Падение ─────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        UpdateCollisions(now);

        var falling = EntityQueryEnumerator<CadiaShuttleFallComponent>();
        while (falling.MoveNext(out var uid, out var fall))
        {
            if (now >= fall.CrashAt)
                Crash(uid);
        }

        if (now < _nextCheck)
            return;
        _nextCheck = now + CheckRate;

        var shuttles = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();
        while (shuttles.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid is not { } map ||
                !TryComp<CadiaFlightLevelComponent>(map, out var level) || level.Level != CadiaFlightLevel.Air ||
                HasComp<FTLComponent>(uid) || HasComp<CadiaShuttleFallComponent>(uid) || HasComp<CadiaHoverComponent>(uid) || uid == map)
                continue;

            if (CanStayAloft(uid))
                continue;

            var fall = AddComp<CadiaShuttleFallComponent>(uid);
            fall.CrashAt = now + FallWarning;
            var alarm = _audio.PlayPvs(FallAlarm, uid, AudioParams.Default.WithVolume(4));
            _audio.SetGridAudio(alarm);
            PopupOnGrid(uid, Loc.GetString("cadia-flight-falling"), PopupType.LargeCaution);
        }
    }

    // ───────────────────────── Столкновения с постройками ─────────────────────────

    /// <summary>
    /// Движок не сталкивает шаттл с постройками на карте-планете: сетка проверяется только против других сеток,
    /// а её стены — неподвижные тела, как и стены Кадии. Поэтому каждый кадр смотрим, не легла ли палуба шаттла
    /// на непроходимую постройку поверхности; если легла — откатываем на последнее свободное место с отскоком,
    /// гремит удар, экипаж трясёт.
    /// </summary>
    private void UpdateCollisions(TimeSpan now)
    {
        var shuttles = EntityQueryEnumerator<ShuttleComponent, MapGridComponent, TransformComponent, PhysicsComponent>();
        while (shuttles.MoveNext(out var uid, out _, out var grid, out var xform, out var body))
        {
            if (xform.MapUid is not { } map || map == uid || !HasComp<CadiaFlightLevelComponent>(map) || HasComp<FTLComponent>(uid))
                continue;

            var state = EnsureComp<CadiaShuttleCollisionComponent>(uid);
            var (position, rotation) = _xform.GetWorldPositionRotation(xform);

            if (state.Map != map)
            {
                state.Map = map;
                state.SafePosition = position;
                state.SafeRotation = rotation;
            }

            if (body.LinearVelocity.LengthSquared() < 0.0001f && Math.Abs(body.AngularVelocity) < 0.0001f)
                continue;

            if (!Overlaps((uid, grid, xform), Comp<MapComponent>(map).MapId))
            {
                state.SafePosition = position;
                state.SafeRotation = rotation;
                continue;
            }

            // Удар: назад на свободное место, скорость гасится с отскоком.
            var impact = body.LinearVelocity.Length();
            _xform.SetWorldPositionRotation(uid, state.SafePosition, state.SafeRotation, xform);
            _physics.SetLinearVelocity(uid, -body.LinearVelocity * 0.25f, body: body);
            _physics.SetAngularVelocity(uid, 0f, body: body);

            if (now < state.NextImpact || impact < 0.5f)
                continue;
            state.NextImpact = now + TimeSpan.FromSeconds(0.6);

            var sound = _audio.PlayPvs(CrashSound, uid, AudioParams.Default.WithVolume(Math.Clamp(impact / 3f - 4f, -6f, 6f)));
            _audio.SetGridAudio(sound);

            var kick = new Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f)) * Math.Clamp(impact / 4f, 0.3f, 3f);
            var crew = Filter.Empty().AddWhereAttachedEntity(e => Transform(e).GridUid == uid);
            foreach (var session in crew.Recipients)
            {
                if (session.AttachedEntity is { } mob)
                    _recoil.KickCamera(mob, kick);
            }
        }
    }

    /// <summary>Лежит ли какая-нибудь плитка шаттла на непроходимой постройке поверхности.</summary>
    private bool Overlaps(Entity<MapGridComponent, TransformComponent> grid, MapId mapId)
    {
        var worldMatrix = _xform.GetWorldMatrix(grid.Comp2);
        var inverse = Matrix3x2.Invert(worldMatrix, out var inv) ? inv : Matrix3x2.Identity;
        var aabb = worldMatrix.TransformBox(grid.Comp1.LocalAABB);

        _hits.Clear();
        _lookup.GetEntitiesIntersecting(mapId, aabb, _hits, LookupFlags.Static);
        foreach (var hit in _hits)
        {
            var hitXform = Transform(hit);
            // Своё и стоящее на других сетках (станции, корабли) движок сталкивает сам.
            if (hitXform.GridUid != hitXform.MapUid || !TryComp<PhysicsComponent>(hit, out var hitBody) || !hitBody.CanCollide)
                continue;
            if (!TryComp<FixturesComponent>(hit, out var fixtures))
                continue;

            var solid = false;
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (fixture.Hard && (fixture.CollisionLayer & (int) (CollisionGroup.Impassable | CollisionGroup.HighImpassable)) != 0)
                {
                    solid = true;
                    break;
                }
            }
            if (!solid)
                continue;

            // Постройка под палубой — столкновение (центр и углы её коробки чуть внутрь).
            var box = _lookup.GetWorldAABB(hit, hitXform);
            foreach (var point in new[] { box.Center, box.BottomLeft + new Vector2(0.15f), box.TopRight - new Vector2(0.15f),
                         new Vector2(box.Left + 0.15f, box.Top - 0.15f), new Vector2(box.Right - 0.15f, box.Bottom + 0.15f) })
            {
                var local = Vector2.Transform(point, inverse);
                var index = new Vector2i((int) Math.Floor(local.X / grid.Comp1.TileSize), (int) Math.Floor(local.Y / grid.Comp1.TileSize));
                if (_map.TryGetTileRef(grid, grid.Comp1, index, out var tile) && !tile.Tile.IsEmpty)
                    return true;
            }
        }
        return false;
    }

    /// <summary>В воздухе держат работающие двигатели и консоль пилота.</summary>
    private bool CanStayAloft(EntityUid grid)
    {
        var thrust = false;
        var thrusters = EntityQueryEnumerator<ThrusterComponent, TransformComponent>();
        while (thrusters.MoveNext(out var thruster, out var xform))
        {
            if (xform.GridUid == grid && thruster.IsOn)
            {
                thrust = true;
                break;
            }
        }
        if (!thrust)
            return false;

        var consoles = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out _, out var xform))
        {
            if (xform.GridUid == grid)
                return true;
        }
        return false;
    }

    /// <summary>Сбитый шаттл рушится на этаж ниже в ту же точку и взрывается от удара.</summary>
    private void Crash(EntityUid grid)
    {
        RemComp<CadiaShuttleFallComponent>(grid);
        var xform = Transform(grid);
        if (xform.MapUid is not { } map || GetLevelMap(map, -1) is not { } below || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        // Двигатели могли починить, пока шаттл падал.
        if (CanStayAloft(grid))
        {
            PopupOnGrid(grid, Loc.GetString("cadia-flight-recovered"), PopupType.Large);
            return;
        }

        var position = _xform.GetWorldPosition(xform);
        var rotation = _xform.GetWorldRotation(xform);
        var belowId = Comp<MapComponent>(below).MapId;
        _xform.SetCoordinates(grid, new EntityCoordinates(below, position));
        _xform.SetWorldRotation(grid, rotation);

        var center = _xform.GetWorldMatrix(grid);
        var epicenter = new MapCoordinates(Vector2.Transform(gridComp.LocalAABB.Center, center), belowId);
        var area = gridComp.LocalAABB.Width * gridComp.LocalAABB.Height;
        _explosion.QueueExplosion(epicenter, "Default", Math.Clamp(area * 1.5f, 60f, 600f), 2.5f, 14f, null, canCreateVacuum: false);
        _audio.PlayPvs(CrashSound, grid);
        PopupOnGrid(grid, Loc.GetString("cadia-flight-crashed"), PopupType.LargeCaution);
    }

    /// <summary>Перестройка стопки включает всем верхним этажам гравитацию; на орбите её быть не должно.</summary>
    private void OnRelinked(ZLevelsRelinkedEvent ev)
    {
        var query = EntityQueryEnumerator<CadiaFlightLevelComponent>();
        while (query.MoveNext(out var uid, out var level))
        {
            if (level.Level == CadiaFlightLevel.Orbit && TryComp<Content.Shared.Gravity.GravityComponent>(uid, out var gravity) && gravity.Enabled)
            {
                gravity.Enabled = false;
                Dirty(uid, gravity);
            }
        }
    }
}

/// <summary>Консоль шаттла, чьи прыжки проверяются правилами полётов Кадии.</summary>
[RegisterComponent]
public sealed partial class CadiaFlightConsoleComponent : Component;

/// <summary>Прыжок шаттла запрещён правилами Кадии — CanFTL откажет с этой причиной.</summary>
[RegisterComponent]
public sealed partial class CadiaFlightBlockComponent : Component
{
    [ViewVariables]
    public string Reason = string.Empty;
}

/// <summary>Шаттл летит на соседний этаж: короткий взлёт или посадка вместо прыжка.</summary>
[RegisterComponent]
public sealed partial class CadiaVerticalFlightComponent : Component
{
    [ViewVariables]
    public bool Up;

    [ViewVariables]
    public CadiaFlightLevel To;
}

/// <summary>Шаттл потерял тягу в небе и падает.</summary>
[RegisterComponent]
public sealed partial class CadiaShuttleFallComponent : Component
{
    [ViewVariables]
    public TimeSpan CrashAt;
}


/// <summary>Последнее положение шаттла над Кадией, где он ни во что не упирался.</summary>
[RegisterComponent]
public sealed partial class CadiaShuttleCollisionComponent : Component
{
    [ViewVariables]
    public EntityUid? Map;

    [ViewVariables]
    public Vector2 SafePosition;

    [ViewVariables]
    public Angle SafeRotation;

    [ViewVariables]
    public TimeSpan NextImpact;
}

/// <summary>Борт держится в небе без тяги (учебные мишени, дирижабли) — механика падения его не трогает.</summary>
[RegisterComponent]
public sealed partial class CadiaHoverComponent : Component;
