using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Robust.Shared.Physics.Events;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>
/// Общая логика этажей: соседние карты, клетка «над» и «под» точкой и правила прохода между этажами
/// (zPassIn/zPassOut из /turf, /turf/open и /turf/open/openspace SS13).
/// </summary>
public abstract class SharedZLevelSystem : EntitySystem
{
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] protected readonly ITileDefinitionManager TileDefs = default!;
    [Dependency] protected readonly SharedMapSystem Maps = default!;
    [Dependency] protected readonly SharedTransformSystem Xforms = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    /// <summary>Мостик (/obj/structure/lattice/catwalk): BLOCK_Z_OUT_DOWN | BLOCK_Z_IN_UP.</summary>
    private static readonly ProtoId<TagPrototype> CatwalkTag = "Catwalk";

    /// <summary>Решётка из прутьев (/obj/structure/lattice): кладётся и на открытое пространство, закрывая дыру.</summary>
    private static readonly ProtoId<ContentTileDefinition> LatticeTile = "Lattice";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZOpenSpaceComponent, PreventCollideEvent>(OnOpenSpaceCollide);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        AllowLatticeOnOpenSpace();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ContentTileDefinition>())
            AllowLatticeOnOpenSpace();
    }

    /// <summary>
    /// Прутья кладут решётку только на космос (BaseTurf решётки). Добавляем открытое пространство в её белый
    /// список прямо в загруженное определение тайла — так работает обычная укладка прутьями, без своей.
    /// </summary>
    private void AllowLatticeOnOpenSpace()
    {
        if (!TileDefs.TryGetDefinition(LatticeTile, out var def) || def is not ContentTileDefinition lattice)
            return;
        if (!lattice.BaseWhitelist.Contains(ZLevelTiles.OpenSpace))
            lattice.BaseWhitelist.Add(ZLevelTiles.OpenSpace);
    }

    /// <summary>
    /// Над открытым пространством стоит невидимая «стена» для поиска пути: мобы под управлением ИИ обходят дыры.
    /// Для всех остальных (игроков, предметов, снарядов) её нет.
    /// </summary>
    private void OnOpenSpaceCollide(Entity<ZOpenSpaceComponent> ent, ref PreventCollideEvent args)
    {
        if (args.OurFixture.Hard && !HasComp<ZAvoidOpenSpaceComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    /// <summary>Карта этажом выше или ниже.</summary>
    public EntityUid? GetAdjacentMap(EntityUid map, bool up)
    {
        if (!TryComp<ZLevelMapComponent>(map, out var z))
            return null;
        return up ? z.Above : z.Below;
    }

    /// <summary>get_step_multiz: та же точка мира этажом выше или ниже.</summary>
    public MapCoordinates? GetStep(MapCoordinates from, bool up)
    {
        if (!Maps.TryGetMap(from.MapId, out var map) || GetAdjacentMap(map.Value, up) is not { } other)
            return null;
        var otherId = Comp<MapComponent>(other).MapId;
        return new MapCoordinates(from.Position, otherId);
    }

    /// <summary>Центр клетки, в которой лежит точка (на сетке — по её тайлам, в космосе — по целым координатам).</summary>
    public MapCoordinates SnapToTile(MapCoordinates coords)
    {
        if (MapManager.TryFindGridAt(coords, out var gridUid, out var grid))
        {
            var idx = Maps.TileIndicesFor(gridUid, grid, coords);
            return Maps.GridTileToWorld(gridUid, grid, idx);
        }
        return new MapCoordinates(new Vector2(MathF.Floor(coords.X) + 0.5f, MathF.Floor(coords.Y) + 0.5f), coords.MapId);
    }

    /// <summary>Тайл в точке и сетка (если клетка вне сеток — космос, тайл пустой).</summary>
    public Tile GetTile(MapCoordinates coords, out EntityUid? gridUid, out Vector2i indices)
    {
        gridUid = null;
        indices = default;
        if (!MapManager.TryFindGridAt(coords, out var uid, out var grid))
            return Tile.Empty;
        gridUid = uid;
        indices = Maps.TileIndicesFor(uid, grid, coords);
        return Maps.GetTileRef(uid, grid, indices).Tile;
    }

    public bool IsOpenSpace(MapCoordinates coords)
    {
        return ZLevelTiles.IsOpenSpace(TileDefs, GetTile(coords, out _, out _));
    }

    /// <summary>Прозрачная клетка: открытое пространство, стекло или пустота между этажами.</summary>
    public bool IsTransparent(MapCoordinates coords)
    {
        var tile = GetTile(coords, out _, out _);
        return tile.IsEmpty || ZLevelTiles.IsTransparent(TileDefs, tile);
    }

    /// <summary>Проём между этажами: пустота или открытое пространство.</summary>
    public bool IsOpen(MapCoordinates coords)
    {
        var tile = GetTile(coords, out _, out _);
        return tile.IsEmpty || ZLevelTiles.IsOpenSpace(TileDefs, tile);
    }

    /// <summary>Платформа лифта, на которой лежит точка.</summary>
    public Entity<ZElevatorComponent>? GetElevatorAt(MapCoordinates coords)
    {
        var query = EntityQueryEnumerator<ZElevatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var elevator, out var xform))
        {
            if (xform.MapID != coords.MapId)
                continue;
            var local = Vector2.Transform(coords.Position, Xforms.GetInvWorldMatrix(xform));
            var half = elevator.Size / 2f;
            if (MathF.Abs(local.X) <= half && MathF.Abs(local.Y) <= half)
                return (uid, elevator);
        }
        return null;
    }

    /// <summary>Стена в клетке (/turf/closed).</summary>
    public bool HasWall(MapCoordinates coords)
    {
        foreach (var ent in GetAnchored(coords))
        {
            if (_tags.HasTag(ent, WallTag))
                return true;
        }
        return false;
    }

    /// <summary>Закреплённые сущности клетки.</summary>
    public IEnumerable<EntityUid> GetAnchored(MapCoordinates coords)
    {
        if (!MapManager.TryFindGridAt(coords, out var uid, out var grid))
            yield break;
        var idx = Maps.TileIndicesFor(uid, grid, coords);
        foreach (var ent in Maps.GetAnchoredEntities(uid, grid, idx))
            yield return ent;
    }

    /// <summary>
    /// zPassIn: можно ли войти в клетку, двигаясь вверх или вниз. Стена (/turf/closed) не пускает никого;
    /// обычный пол пускает только падающих сверху; открытое пространство пускает в обе стороны.
    /// </summary>
    public bool PassIn(MapCoordinates coords, bool up)
    {
        var tile = GetTile(coords, out _, out _);
        var open = tile.IsEmpty || ZLevelTiles.IsOpenSpace(TileDefs, tile); // пустота между этажами — тоже проём
        if (!open && up)
            return false;
        foreach (var ent in GetAnchored(coords))
        {
            if (_tags.HasTag(ent, WallTag))
                return false;
            if (TryComp<ZBlockComponent>(ent, out var block) && (up ? block.InUp : block.InDown))
                return false;
            if (up && _tags.HasTag(ent, CatwalkTag))
                return false;
        }
        return true;
    }

    /// <summary>
    /// zPassOut: можно ли покинуть клетку вверх или вниз. Вниз уходят только с открытого пространства
    /// (если его не перекрывает решётка, мостик или трап), вверх — с любого пола.
    /// </summary>
    public bool PassOut(MapCoordinates coords, bool up)
    {
        var tile = GetTile(coords, out _, out _);
        var open = tile.IsEmpty || ZLevelTiles.IsOpenSpace(TileDefs, tile); // пустота между этажами — тоже проём
        if (!open && !up)
            return false;
        // С платформы лифта не падают, хоть под ней и шахта.
        if (!up && GetElevatorAt(coords) != null)
            return false;
        foreach (var ent in GetAnchored(coords))
        {
            if (_tags.HasTag(ent, WallTag))
                return false;
            if (TryComp<ZBlockComponent>(ent, out var block) && (up ? block.OutUp : block.OutDown))
                return false;
            if (!up && _tags.HasTag(ent, CatwalkTag))
                return false;
        }
        return true;
    }
}
