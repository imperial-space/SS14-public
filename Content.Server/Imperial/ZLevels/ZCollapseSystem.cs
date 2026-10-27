using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ZLevels;

/// <summary>
/// Обрушение этажей без опоры. Пол верхнего этажа держится, только если не дальше <see cref="SupportRange"/>
/// клеток по полу от клетки, под которой на этаже ниже стоит стена.
/// Неподпёртый пол сначала трещит, потом осыпается кусками: тайл пропадает, стены на нём разрушаются,
/// остальное и всё, что стояло сверху, падает этажом ниже. Нижний этаж стопки и карты на паузе (маппинг) не рушатся.
/// </summary>
public sealed class ZCollapseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly ZFallSystem _fall = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    /// <summary>Сколько клеток пол держится без опоры снизу.</summary>
    public const int SupportRange = 6;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CrumbleInterval = TimeSpan.FromSeconds(0.4);
    /// <summary>Между первым треском и первым обвалившимся куском.</summary>
    private static readonly TimeSpan Warning = TimeSpan.FromSeconds(4);
    /// <summary>Сколько клеток осыпается за раз на одной сетке.</summary>
    private const int CrumblePerStep = 3;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly SoundSpecifier CreakSound = new SoundPathSpecifier("/Audio/Machines/airlock_creaking.ogg");
    private static readonly SoundSpecifier CrumbleSound = new SoundPathSpecifier("/Audio/Effects/break_stone.ogg");
    private static readonly DamageSpecifier WallBreak = new() { DamageDict = { ["Structural"] = 1000 } };

    /// <summary>Неподпёртые клетки и когда они начнут осыпаться.</summary>
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), TimeSpan> _pending = new();
    private TimeSpan _nextScan;
    private TimeSpan _nextCrumble;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now >= _nextScan)
        {
            _nextScan = now + ScanInterval;
            Scan(now);
        }
        if (now >= _nextCrumble)
        {
            _nextCrumble = now + CrumbleInterval;
            Crumble(now);
        }
    }

    // ---------- Поиск неподпёртого пола ----------

    private void Scan(TimeSpan now)
    {
        var unsupported = new HashSet<(EntityUid, Vector2i)>();
        var maps = EntityQueryEnumerator<ZLevelMapComponent, MapComponent>();
        while (maps.MoveNext(out var mapUid, out var z, out var map))
        {
            if (z.Below == null || !map.MapInitialized || map.MapPaused)
                continue;
            var grids = new List<Entity<MapGridComponent>>();
            var children = Transform(mapUid).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (TryComp<MapGridComponent>(child, out var grid))
                    grids.Add((child, grid));
            }
            foreach (var grid in grids)
                FindUnsupported(grid, unsupported);
        }

        // Подпёрли или уже обрушилось — забыть; новые — затрещать.
        foreach (var key in new List<(EntityUid, Vector2i)>(_pending.Keys))
        {
            if (!unsupported.Contains(key))
                _pending.Remove(key);
        }
        var warned = new HashSet<EntityUid>();
        foreach (var key in unsupported)
        {
            if (_pending.ContainsKey(key))
                continue;
            _pending[key] = now + Warning + TimeSpan.FromSeconds(_random.NextFloat(0f, 3f));
            if (warned.Add(key.Item1) && TryComp<MapGridComponent>(key.Item1, out var gridComp))
            {
                var coords = _map.GridTileToLocal(key.Item1, gridComp, key.Item2);
                _audio.PlayPvs(CreakSound, coords);
                _popup.PopupCoordinates(Loc.GetString("zlevel-collapse-warning"), coords, PopupType.LargeCaution);
            }
        }
    }

    /// <summary>Поиск в ширину по полу от подпёртых клеток; дальше <see cref="SupportRange"/> — без опоры.</summary>
    private void FindUnsupported(Entity<MapGridComponent> grid, HashSet<(EntityUid, Vector2i)> result)
    {
        var floor = new HashSet<Vector2i>();
        foreach (var tile in _map.GetAllTiles(grid.Owner, grid.Comp))
        {
            if (!ZLevelTiles.IsOpenSpace(_tileDefs, tile.Tile))
                floor.Add(tile.GridIndices);
        }
        if (floor.Count == 0)
            return;

        var dist = new Dictionary<Vector2i, int>();
        var queue = new Queue<Vector2i>();
        foreach (var idx in floor)
        {
            if (!IsSupported(grid, idx))
                continue;
            dist[idx] = 0;
            queue.Enqueue(idx);
        }
        while (queue.TryDequeue(out var idx))
        {
            var d = dist[idx];
            if (d >= SupportRange)
                continue;
            foreach (var n in new[] { idx + Vector2i.Up, idx + Vector2i.Down, idx + Vector2i.Left, idx + Vector2i.Right })
            {
                if (!floor.Contains(n) || dist.ContainsKey(n))
                    continue;
                dist[n] = d + 1;
                queue.Enqueue(n);
            }
        }

        foreach (var idx in floor)
        {
            if (!dist.ContainsKey(idx))
                result.Add((grid.Owner, idx));
        }
    }

    /// <summary>Под клеткой на этаже ниже стена — она и держит пол.</summary>
    private bool IsSupported(Entity<MapGridComponent> grid, Vector2i idx)
    {
        var here = _map.GridTileToWorld(grid.Owner, grid.Comp, idx);
        if (_z.GetStep(here, false) is not { } below)
            return true;
        foreach (var ent in _z.GetAnchored(below))
        {
            if (_tags.HasTag(ent, WallTag))
                return true;
        }
        return false;
    }

    // ---------- Обвал ----------

    private void Crumble(TimeSpan now)
    {
        if (_pending.Count == 0)
            return;
        var perGrid = new Dictionary<EntityUid, int>();
        var due = new List<(EntityUid Grid, Vector2i Tile)>();
        foreach (var (key, time) in _pending)
        {
            if (time <= now)
                due.Add(key);
        }
        _random.Shuffle(due);
        foreach (var key in due)
        {
            perGrid.TryGetValue(key.Grid, out var count);
            if (count >= CrumblePerStep)
                continue;
            perGrid[key.Grid] = count + 1;
            _pending.Remove(key);
            if (TryComp<MapGridComponent>(key.Grid, out var grid))
                Collapse((key.Grid, grid), key.Tile);
        }
    }

    /// <summary>Тайл пропадает: стены рушатся, закреплённое и всё сверху падает этажом ниже вместе с обломком пола.</summary>
    private void Collapse(Entity<MapGridComponent> grid, Vector2i idx)
    {
        var tile = _map.GetTileRef(grid.Owner, grid.Comp, idx).Tile;
        if (tile.IsEmpty || ZLevelTiles.IsOpenSpace(_tileDefs, tile))
            return;
        var here = _map.GridTileToWorld(grid.Owner, grid.Comp, idx);
        var anchored = new List<EntityUid>(_map.GetAnchoredEntities(grid.Owner, grid.Comp, idx));
        var tileBox = Box2.CenteredAround(here.Position, new Vector2(grid.Comp.TileSize * 0.9f));
        var onTop = _lookup.GetEntitiesIntersecting(here.MapId, tileBox, LookupFlags.Uncontained | LookupFlags.Dynamic | LookupFlags.Sundries);

        _audio.PlayPvs(CrumbleSound, _xform.ToCoordinates(here));
        _map.SetTile(grid.Owner, grid.Comp, idx, Tile.Empty);

        // Обломок пола падает вниз.
        if (_tileDefs[tile.TypeId] is ContentTileDefinition { ItemDropPrototypeName: { } drop } && !string.IsNullOrEmpty(drop))
            onTop.Add(Spawn(drop, here));

        foreach (var ent in anchored)
        {
            if (TerminatingOrDeleted(ent))
                continue;
            if (_tags.HasTag(ent, WallTag))
            {
                _damage.TryChangeDamage(ent, WallBreak, true);
                continue;
            }
            if (Transform(ent).Anchored)
                _xform.Unanchor(ent);
            if (TryComp<PhysicsComponent>(ent, out var body) && body.BodyType == BodyType.Static)
                _physics.SetBodyType(ent, BodyType.Dynamic, body: body);
            onTop.Add(ent);
        }

        foreach (var ent in onTop)
        {
            if (TerminatingOrDeleted(ent) || HasComp<MapGridComponent>(ent))
                continue;
            var xform = Transform(ent);
            if (xform.Anchored || xform.ParentUid != xform.GridUid && xform.ParentUid != xform.MapUid)
                continue;
            _xform.AttachToGridOrMap(ent, xform);
            _fall.ZFall(ent);
        }
    }
}
