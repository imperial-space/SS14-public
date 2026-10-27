using System.Linq;
using Content.Server.Station.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Shared.Gravity;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ZLevels;

/// <summary>Действия «посмотреть вверх/вниз», выданные игроку, пока он на многоэтажной карте.</summary>
[RegisterComponent]
public sealed partial class ZLevelActionsComponent : Component
{
    [ViewVariables]
    public EntityUid? LookUp;

    [ViewVariables]
    public EntityUid? LookDown;
}

/// <summary>
/// Этажи как в SS13: связывание карт в стопку, подгрузка этажей из файлов, перенос сущностей между этажами (zMove),
/// подписка игроков на нижний этаж (чтобы клиент видел его сквозь открытое пространство) и взгляд вверх/вниз.
/// </summary>
public sealed class ZLevelSystem : SharedZLevelSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ViewSubscriberSystem _views = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly GunSystem _gun = default!;

    private bool _redirectingShot;
    /// <summary>Только что созданные пустые сетки: первый тайл покажет, где их строят, и их выровняют по этажу.</summary>
    private readonly HashSet<EntityUid> _freshGrids = new();
    private const float AlignRange = 48f;

    private const string LookUpAction = "ActionZLookUp";
    private const string LookDownAction = "ActionZLookDown";

    /// <summary>Подписчики на этажи ниже (до двух, как глубина отрисовки) под каждым игроком рядом с прозрачными клетками.</summary>
    private readonly Dictionary<(ICommonSession, int), EntityUid> _viewers = new();
    private const int ViewDepth = 2;
    /// <summary>Стопки, чьи верхние этажи ещё надо добавить в станцию нижнего.</summary>
    private readonly HashSet<string> _pendingStation = new();
    private TimeSpan _nextSlow;
    private const float ViewerRange = 14f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZLevelMapComponent, ComponentStartup>(OnLevelStartup);
        SubscribeLocalEvent<ZLevelMapComponent, ComponentShutdown>(OnLevelShutdown);
        SubscribeLocalEvent<ZLevelStackComponent, MapInitEvent>(OnStackMapInit);
        SubscribeLocalEvent<ZLookUpActionEvent>(OnLookUpAction);
        SubscribeLocalEvent<ZLookDownActionEvent>(OnLookDownAction);
        SubscribeLocalEvent<ZLookingComponent, ComponentShutdown>(OnLookingShutdown);
        SubscribeLocalEvent<ZLookingComponent, SelfBeforeGunShotEvent>(OnLookingShoot);
        SubscribeLocalEvent<GridStartupEvent>(OnGridStartup);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    // ---------- Стопка этажей ----------

    private void OnLevelStartup(Entity<ZLevelMapComponent> ent, ref ComponentStartup args)
    {
        RelinkStack(ent.Comp.Stack);
    }

    private void OnLevelShutdown(Entity<ZLevelMapComponent> ent, ref ComponentShutdown args)
    {
        var stack = ent.Comp.Stack;
        ent.Comp.Stack = string.Empty;
        RelinkStack(stack);
    }

    /// <summary>Все карты стопки снизу вверх.</summary>
    public List<Entity<ZLevelMapComponent>> GetStack(string stack)
    {
        var list = new List<Entity<ZLevelMapComponent>>();
        if (string.IsNullOrEmpty(stack))
            return list;
        var query = EntityQueryEnumerator<ZLevelMapComponent>();
        while (query.MoveNext(out var uid, out var z))
        {
            if (z.Stack == stack && !TerminatingOrDeleted(uid))
                list.Add((uid, z));
        }
        return list.OrderBy(e => e.Comp.Level).ToList();
    }

    /// <summary>Прописывает выше/ниже между соседними по уровню картами стопки.</summary>
    public void RelinkStack(string stack)
    {
        var levels = GetStack(stack);
        for (var i = 0; i < levels.Count; i++)
        {
            var z = levels[i].Comp;
            z.Below = i > 0 && levels[i - 1].Comp.Level == z.Level - 1 ? levels[i - 1].Owner : null;
            z.Above = i < levels.Count - 1 && levels[i + 1].Comp.Level == z.Level + 1 ? levels[i + 1].Owner : null;
            Dirty(levels[i]);
            // Верхние этажи стоят на нижнем — гравитация у них своя, постоянная (гравгенератор только внизу).
            // Нижний этаж — обычный космос со станцией, его не трогаем.
            if (z.Below != null)
            {
                var gravity = EnsureComp<GravityComponent>(levels[i]);
                gravity.Enabled = true;
                gravity.Inherent = true;
                Dirty(levels[i], gravity);
            }
        }
        RaiseLocalEvent(new ZLevelsRelinkedEvent(stack));
    }

    /// <summary>Делает карту этажом стопки.</summary>
    public void SetLevel(EntityUid map, string stack, int level)
    {
        var z = EnsureComp<ZLevelMapComponent>(map);
        var old = z.Stack;
        z.Stack = stack;
        z.Level = level;
        Dirty(map, z);
        if (old != stack)
            RelinkStack(old);
        RelinkStack(stack);
    }

    private void OnStackMapInit(Entity<ZLevelStackComponent> ent, ref MapInitEvent args)
    {
        LoadStack(ent, true);
    }

    /// <summary>
    /// Подгружает этажи из <see cref="ZLevelStackComponent"/> и связывает их с нижней картой.
    /// Имя стопки уникально на раунд, поэтому одну и ту же многоэтажную карту можно загрузить несколько раз.
    /// </summary>
    public List<EntityUid> LoadStack(Entity<ZLevelStackComponent> baseMap, bool initialize)
    {
        var loaded = new List<EntityUid>();
        if (baseMap.Comp.Loaded)
            return loaded;
        baseMap.Comp.Loaded = true;

        var stack = $"stack-{baseMap.Owner.Id}";
        var baseZ = EnsureComp<ZLevelMapComponent>(baseMap);
        var baseLevel = baseZ.Level;
        baseZ.Stack = stack;
        Dirty(baseMap.Owner, baseZ);

        foreach (var (level, path) in baseMap.Comp.Levels.OrderBy(kv => kv.Key))
        {
            var opts = new DeserializationOptions { InitializeMaps = initialize, PauseMaps = !initialize };
            if (!_loader.TryLoadMap(path, out var map, out _, opts))
            {
                Log.Error($"Не удалось загрузить этаж {level} стопки {ToPrettyString(baseMap)}: {path}");
                continue;
            }
            var z = EnsureComp<ZLevelMapComponent>(map.Value);
            z.Stack = stack;
            z.Level = baseLevel + level;
            Dirty(map.Value.Owner, z);
            loaded.Add(map.Value);
        }

        RelinkStack(stack);
        _pendingStation.Add(stack);
        return loaded;
    }

    /// <summary>Верхние этажи становятся частью станции нижнего: свои записи, питание, объявления.</summary>
    private void TryJoinStation(string stack)
    {
        var levels = GetStack(stack);
        EntityUid? station = null;
        foreach (var level in levels)
        {
            var mapId = Comp<MapComponent>(level).MapId;
            foreach (var grid in MapManager.GetAllGrids(mapId))
            {
                if (TryComp<StationMemberComponent>(grid, out var member))
                {
                    station = member.Station;
                    break;
                }
            }
            if (station != null)
                break;
        }
        if (station == null)
            return;

        foreach (var level in levels)
        {
            var mapId = Comp<MapComponent>(level).MapId;
            foreach (var grid in MapManager.GetAllGrids(mapId))
            {
                if (!HasComp<StationMemberComponent>(grid))
                    _station.AddGridToStation(station.Value, grid, grid.Comp);
            }
        }
        _pendingStation.Remove(stack);
    }

    // ---------- Перенос между этажами (zMove) ----------

    /// <summary>
    /// get_z_move_affected: сама сущность, пристёгнутые к ней и (если нужно) цепочка тех, кого тащат.
    /// </summary>
    public List<EntityUid> GetZMoveAffected(EntityUid ent, bool includePulled)
    {
        var list = new List<EntityUid>();
        Collect(ent, includePulled, list);
        return list;
    }

    private void Collect(EntityUid ent, bool includePulled, List<EntityUid> list)
    {
        if (list.Contains(ent))
            return;
        list.Add(ent);
        if (TryComp<StrapComponent>(ent, out var strap))
        {
            foreach (var buckled in strap.BuckledEntities)
                Collect(buckled, includePulled, list);
        }
        if (includePulled && TryComp<PullerComponent>(ent, out var puller) && puller.Pulling is { } pulled)
            Collect(pulled, includePulled, list);
    }

    /// <summary>
    /// zMove: переносит сущность и всё, что движется вместе с ней, в точку на другом этаже.
    /// Таскание обрывается, если тащащий и таскаемый оказались на разных этажах или далеко.
    /// </summary>
    public void ZMove(EntityUid ent, MapCoordinates target, bool includePulled)
    {
        var moving = GetZMoveAffected(ent, includePulled);
        foreach (var mov in moving)
        {
            Xforms.SetMapCoordinates(mov, target);
            Xforms.AttachToGridOrMap(mov);
        }

        foreach (var mov in moving)
        {
            if (TryComp<PullableComponent>(mov, out var pullable) && pullable.Puller is { } puller)
            {
                var a = Xforms.GetMapCoordinates(mov);
                var b = Xforms.GetMapCoordinates(puller);
                if (a.MapId != b.MapId || (a.Position - b.Position).Length() > 1.5f)
                    _pulling.TryStopPull(mov, pullable);
            }
        }
    }

    // ---------- Взгляд вверх и вниз (look_up / look_down) ----------

    private void OnLookUpAction(ZLookUpActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        LookVerb(args.Performer, true);
    }

    private void OnLookDownAction(ZLookDownActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        LookVerb(args.Performer, false);
    }

    /// <summary>/mob/living/proc/lookup и lookdown: повторное нажатие возвращает взгляд.</summary>
    public void LookVerb(EntityUid user, bool up)
    {
        if (TryComp<ZLookingComponent>(user, out _))
        {
            _popup.PopupEntity(Loc.GetString("zlevel-look-straight"), user, user);
            EndLook(user);
            return;
        }

        var coords = Xforms.GetMapCoordinates(user);
        if (GetStep(coords, up) == null)
        {
            _popup.PopupEntity(Loc.GetString(up ? "zlevel-nothing-above" : "zlevel-nothing-below"), user, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString(up ? "zlevel-tilt-up" : "zlevel-tilt-down"), user, user);
        Look(user, up);
    }

    /// <summary>can_look_up: в сознании и руки не связаны чем-то кроме наручников.</summary>
    private bool CanLook(EntityUid user)
    {
        return _blocker.CanInteract(user, null) || _blocker.CanConsciouslyPerformAction(user);
    }

    private void Look(EntityUid user, bool up)
    {
        if (!CanLook(user))
            return;
        if (GetLookingTarget(user, up, true) is not { } target)
            return;

        // looking_holder: камера остаётся на игроке (плавно, с предсказанием), а точка взгляда только подписывает
        // игрока на тот этаж — клиент рисует его поверх своего.
        var holder = Spawn(null, target);
        if (TryComp<ActorComponent>(user, out var actor))
            _views.AddViewSubscriber(holder, actor.PlayerSession);
        var looking = EnsureComp<ZLookingComponent>(user);
        looking.Holder = holder;
        looking.Up = up;
        looking.Map = Xforms.GetMap(holder);
        Dirty(user, looking);
    }

    /// <summary>
    /// get_looking_turf: смотреть можно только сквозь прозрачную клетку — над собой (вверх), под собой (вниз),
    /// а если там сплошной пол — в клетку впереди или в любую соседнюю.
    /// </summary>
    public MapCoordinates? GetLookingTarget(EntityUid user, bool up, bool feedback)
    {
        var here = SnapToTile(Xforms.GetMapCoordinates(user));
        if (GetStep(here, up) is not { } step)
        {
            if (feedback)
                _popup.PopupEntity(Loc.GetString("zlevel-nothing-interesting"), user, user);
            return null;
        }

        var check = up ? step : here;
        if (!IsTransparent(check))
        {
            var dir = Xforms.GetWorldRotation(user).GetCardinalDir().ToVec();
            var front = new MapCoordinates(check.Position + dir, check.MapId);
            if (IsTransparent(front))
                check = front;
            else
            {
                var found = false;
                for (var dx = -1; dx <= 1 && !found; dx++)
                for (var dy = -1; dy <= 1 && !found; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    var n = new MapCoordinates(check.Position + new System.Numerics.Vector2(dx, dy), check.MapId);
                    if (IsTransparent(n))
                    {
                        check = n;
                        found = true;
                    }
                }
                if (!found)
                {
                    if (feedback)
                        _popup.PopupEntity(Loc.GetString(up ? "zlevel-cant-see-above" : "zlevel-cant-see-below"), user, user);
                    return null;
                }
            }
        }

        return up ? check : GetStep(check, false);
    }

    /// <summary>end_look.</summary>
    public void EndLook(EntityUid user)
    {
        RemCompDeferred<ZLookingComponent>(user);
    }

    private void OnLookingShutdown(Entity<ZLookingComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<ActorComponent>(ent, out var actor) && !TerminatingOrDeleted(ent.Comp.Holder))
            _views.RemoveViewSubscriber(ent.Comp.Holder, actor.PlayerSession);
        QueueDel(ent.Comp.Holder);
    }

    // ---------- Выравнивание новых сеток по этажам ----------

    private void OnGridStartup(GridStartupEvent ev)
    {
        if (TryComp<MapGridComponent>(ev.EntityUid, out var grid) && grid.ChunkCount == 0)
            _freshGrids.Add(ev.EntityUid);
    }

    /// <summary>
    /// Тайл в пустоте создаёт новую сетку ровно по осям мира. На многоэтажной карте её сразу ставим с позицией и
    /// поворотом ближайшей сетки соседнего этажа (сначала ниже, потом выше, потом своего), чтобы тайл был над тайлом.
    /// </summary>
    private void OnTileChanged(ref TileChangedEvent args)
    {
        if (!_freshGrids.Remove(args.Entity) || args.Changes.Length == 0)
            return;
        var grid = args.Entity;
        if (Transform(grid).MapUid is not { } map || !HasComp<ZLevelMapComponent>(map))
            return;

        var index = args.Changes[0].GridIndices;
        var world = Maps.GridTileToWorldPos(grid, grid.Comp, index);
        if (FindAlignGrid(map, grid, world) is not { } reference)
            return;

        var tile = Maps.GetTileRef(grid, grid.Comp, index).Tile;
        var newIndex = Maps.WorldToTile(reference, reference.Comp, world);
        var (pos, rot) = Xforms.GetWorldPositionRotation(reference);
        Xforms.SetWorldPositionRotation(grid, pos, rot);
        if (newIndex == index)
            return;
        // Сначала новый тайл, потом убрать старый — иначе пустую сетку удалят.
        Maps.SetTile(grid, grid.Comp, newIndex, tile);
        Maps.SetTile(grid, grid.Comp, index, Tile.Empty);
    }

    private Entity<MapGridComponent>? FindAlignGrid(EntityUid map, EntityUid self, System.Numerics.Vector2 world)
    {
        for (var below = GetAdjacentMap(map, false); below != null; below = GetAdjacentMap(below.Value, false))
        {
            if (NearestGrid(below.Value, self, world) is { } found)
                return found;
        }
        for (var above = GetAdjacentMap(map, true); above != null; above = GetAdjacentMap(above.Value, true))
        {
            if (NearestGrid(above.Value, self, world) is { } found)
                return found;
        }
        return NearestGrid(map, self, world);
    }

    private Entity<MapGridComponent>? NearestGrid(EntityUid map, EntityUid self, System.Numerics.Vector2 world)
    {
        var grids = new List<Entity<MapGridComponent>>();
        MapManager.FindGridsIntersecting(Comp<MapComponent>(map).MapId, Box2.CenteredAround(world, new System.Numerics.Vector2(AlignRange * 2)), ref grids);
        Entity<MapGridComponent>? best = null;
        var bestDist = float.MaxValue;
        foreach (var g in grids)
        {
            if (g.Owner == self)
                continue;
            var local = Maps.WorldToLocal(g, g.Comp, world);
            var dist = (local - g.Comp.LocalAABB.ClosestPoint(local)).Length();
            if (dist < bestDist)
            {
                best = g;
                bestDist = dist;
            }
        }
        return best;
    }

    /// <summary>
    /// Стрельба, пока смотришь на соседний этаж: выстрел уходит туда, куда смотришь — из клетки, сквозь которую
    /// смотришь, в ту же мировую точку цели на том этаже (этажи совмещены по мировым координатам).
    /// </summary>
    private void OnLookingShoot(Entity<ZLookingComponent> ent, ref SelfBeforeGunShotEvent args)
    {
        if (_redirectingShot || Deleted(ent.Comp.Holder) || args.Gun.Comp.ShootCoordinates is not { } to)
            return;
        var from = Xforms.GetMapCoordinates(ent.Comp.Holder);
        var toMap = Xforms.ToMapCoordinates(to);
        if (from.MapId == toMap.MapId || !Maps.TryGetMap(from.MapId, out var fromMapUid))
            return;

        args.Cancel();
        _redirectingShot = true;
        try
        {
            var fromCoords = new EntityCoordinates(fromMapUid.Value, from.Position);
            var toCoords = new EntityCoordinates(fromMapUid.Value, toMap.Position);
            _gun.Shoot(args.Gun, args.Ammo, fromCoords, toCoords, out _, ent);
        }
        finally
        {
            _redirectingShot = false;
        }
    }

    /// <summary>mirror_move: точка взгляда идёт за игроком; потерял прозрачную клетку — взгляд кончился.</summary>
    private void UpdateLooking()
    {
        var query = EntityQueryEnumerator<ZLookingComponent>();
        var stop = new List<EntityUid>();
        while (query.MoveNext(out var uid, out var looking))
        {
            if (Deleted(looking.Holder) || !CanLook(uid) || GetLookingTarget(uid, looking.Up, false) is not { } target)
            {
                stop.Add(uid);
                continue;
            }
            Xforms.SetMapCoordinates(looking.Holder, target);
        }
        foreach (var uid in stop)
            EndLook(uid);
    }

    // ---------- Действия и подписка на нижний этаж ----------

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _freshGrids.Clear();
        UpdateLooking();

        var now = _timing.CurTime;
        if (now < _nextSlow)
            return;
        _nextSlow = now + TimeSpan.FromSeconds(0.5);

        foreach (var stack in _pendingStation.ToList())
            TryJoinStation(stack);
        UpdateActorsAndViewers();
    }

    private void UpdateActorsAndViewers()
    {
        var alive = new HashSet<(ICommonSession, int)>();
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var actor, out var xform))
        {
            var mapUid = xform.MapUid;
            var onLevels = mapUid != null && TryComp<ZLevelMapComponent>(mapUid, out var z) && (z.Above != null || z.Below != null);
            UpdateActions(uid, onLevels && HasComp<MobStateComponent>(uid));

            if (!onLevels)
                continue;
            var coords = Xforms.GetMapCoordinates(uid, xform);
            var session = actor.PlayerSession;
            var level = mapUid!.Value;
            for (var depth = 1; depth <= ViewDepth; depth++)
            {
                if (!HasTransparentNear(new MapCoordinates(coords.Position, Comp<MapComponent>(level).MapId)) || GetAdjacentMap(level, false) is not { } below)
                    break;
                level = below;
                alive.Add((session, depth));
                var target = new MapCoordinates(coords.Position, Comp<MapComponent>(below).MapId);
                if (_viewers.TryGetValue((session, depth), out var viewer) && !Deleted(viewer) && Transform(viewer).MapUid == below)
                {
                    Xforms.SetMapCoordinates(viewer, target);
                    continue;
                }
                if (_viewers.TryGetValue((session, depth), out var old) && !Deleted(old))
                    QueueDel(old);
                var ent = Spawn(null, target);
                _views.AddViewSubscriber(ent, session);
                _viewers[(session, depth)] = ent;
            }
        }

        foreach (var (key, viewer) in _viewers.ToList())
        {
            if (alive.Contains(key))
                continue;
            if (!Deleted(viewer))
            {
                _views.RemoveViewSubscriber(viewer, key.Item1);
                QueueDel(viewer);
            }
            _viewers.Remove(key);
        }
    }

    private bool HasTransparentNear(MapCoordinates coords)
    {
        var box = Box2.CenteredAround(coords.Position, new System.Numerics.Vector2(ViewerRange * 2, ViewerRange * 2));
        var grids = new List<Entity<MapGridComponent>>();
        MapManager.FindGridsIntersecting(coords.MapId, box, ref grids);
        foreach (var grid in grids)
        {
            foreach (var tile in Maps.GetTilesIntersecting(grid.Owner, grid.Comp, box))
            {
                if (ZLevelTiles.IsTransparent(TileDefs, tile.Tile))
                    return true;
            }
        }

        // Космос на многоэтажной карте тоже прозрачен: хоть одна клетка вне сеток — этаж ниже виден.
        for (var x = box.Left; x <= box.Right; x += 2f)
        for (var y = box.Bottom; y <= box.Top; y += 2f)
        {
            if (!MapManager.TryFindGridAt(coords.MapId, new System.Numerics.Vector2(x, y), out _, out _))
                return true;
        }
        return false;
    }

    private void UpdateActions(EntityUid uid, bool want)
    {
        if (want)
        {
            var comp = EnsureComp<ZLevelActionsComponent>(uid);
            if (comp.LookUp == null)
                _actions.AddAction(uid, ref comp.LookUp, LookUpAction);
            if (comp.LookDown == null)
                _actions.AddAction(uid, ref comp.LookDown, LookDownAction);
            return;
        }

        if (!TryComp<ZLevelActionsComponent>(uid, out var actions))
            return;
        _actions.RemoveAction(uid, actions.LookUp);
        _actions.RemoveAction(uid, actions.LookDown);
        RemComp<ZLevelActionsComponent>(uid);
        if (HasComp<ZLookingComponent>(uid))
            EndLook(uid);
    }
}

/// <summary>Связи этажей стопки пересчитаны: трапы перепривязываются.</summary>
public readonly record struct ZLevelsRelinkedEvent(string Stack);
