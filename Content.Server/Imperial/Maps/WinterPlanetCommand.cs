using System.Linq;
using Content.Server.Administration;
using Content.Server.Parallax;
using Content.Shared.Administration;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Maps;

/// <summary>
/// Преобразует карту в зимнюю планету (снег) с горизонтальной дорогой.
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed class WinterPlanetCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSys = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;

    public override string Command => "winterplanet";
    public override string Description => "Преобразует карту в зимнюю планету со снегом и дорогой";
    public override string Help => @"Использование: winterplanet <mapId>
  <mapId> - ID карты для преобразования

Примеры:
  winterplanet 1       - преобразовать карту 1 в снежную планету

Создает горизонтальную дорогу (асфальт) в центре карты, остальное заполняется снегом.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Укажите ID карты. Использование: winterplanet <mapId>");
            return;
        }

        if (!int.TryParse(args[0], out var mapInt))
        {
            shell.WriteError($"Неверный ID карты: {args[0]}");
            return;
        }

        var mapId = new MapId(mapInt);
        if (!_mapSys.MapExists(mapId))
        {
            shell.WriteError($"Карта с ID {mapId} не существует");
            return;
        }

        ProtoId<BiomeTemplatePrototype> snowBiomeId = "Snow";
        if (!_protoManager.TryIndex(snowBiomeId, out var snowTemplate))
        {
            shell.WriteError("Биом Snow не найден");
            return;
        }

        try
        {
            // Применяем снежный биом
            var biomeSystem = _entManager.System<BiomeSystem>();
            var mapUid = _mapSys.GetMapOrInvalid(mapId);
            biomeSystem.EnsurePlanet(mapUid, snowTemplate);

            const int roadWidth = 20;
            if (!_tileDefs.TryGetDefinition("FloorAsphalt", out var roadDef))
            {
                shell.WriteError("Тайл FloorAsphalt не найден");
                return;
            }

            var changedTiles = PaintRoadOnMap(mapId, new Tile(roadDef.TileId), roadWidth);

            var removedEntities = ClearRoadObstacles(mapId, roadWidth);

            shell.WriteLine($"✓ Карта преобразована в зимнюю планету!");
            shell.WriteLine($"Map ID: {mapId}");
            shell.WriteLine($"Биом: Snow (снег)");
            shell.WriteLine($"Дорога: {roadWidth} тайлов в ширину (в центре, горизонтально)");
            shell.WriteLine($"Изменено тайлов дороги: {changedTiles}");
            shell.WriteLine($"Удалено объектов с дороги: {removedEntities}");
        }
        catch (Exception ex)
        {
            shell.WriteError($"Ошибка при создании зимней планеты: {ex.Message}");
        }
    }

    private int PaintRoadOnMap(MapId mapId, Tile roadTile, int roadWidth)
    {
        var changedTiles = 0;
        var grids = GetOrCreateMapGrids(mapId);
        foreach (var gridEnt in grids)
        {
            var gridUid = gridEnt.Owner;
            var grid = gridEnt.Comp;

            // На новых планетах чанки могут быть еще не сгенерированы.
            // В этом случае рисуем дорогу в стандартной зоне вокруг (0,0).
            if (!TryGetGridBounds(gridUid, grid, out var minX, out var maxX, out var minY, out var maxY))
            {
                minX = -256;
                maxX = 256;
                minY = -128;
                maxY = 128;
            }

            var centerY = (minY + maxY) / 2;
            var startY = centerY - roadWidth / 2;
            var tiles = new List<(Vector2i GridIndices, Tile Tile)>((maxX - minX + 1) * roadWidth);

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = startY; y < startY + roadWidth; y++)
                {
                    var pos = new Vector2i(x, y);
                    tiles.Add((pos, roadTile));
                }
            }

            if (tiles.Count == 0)
                continue;

            _mapSys.SetTiles(gridUid, grid, tiles);
            changedTiles += tiles.Count;
        }

        return changedTiles;
    }

    private int ClearRoadObstacles(MapId mapId, int roadWidth)
    {
        var removed = 0;
        var mapUid = _mapSys.GetMapOrInvalid(mapId);
        if (mapUid == EntityUid.Invalid)
            return removed;

        var grids = GetOrCreateMapGrids(mapId);
        foreach (var gridEnt in grids)
        {
            var gridUid = gridEnt.Owner;
            var grid = gridEnt.Comp;
            if (!TryGetGridBounds(gridUid, grid, out var minX, out var maxX, out var minY, out var maxY))
            {
                minX = -256;
                maxX = 256;
                minY = -128;
                maxY = 128;
            }

            var centerY = (minY + maxY) / 2;
            var startY = centerY - roadWidth / 2;

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = startY; y < startY + roadWidth; y++)
                {
                    var entities = _mapSys.GetAnchoredEntities(gridUid, grid, new Vector2i(x, y)).ToArray();
                    foreach (var ent in entities)
                    {
                        if (ent == gridUid || ent == mapUid)
                            continue;

                        if (_entManager.HasComponent<MapGridComponent>(ent) || _entManager.HasComponent<MapComponent>(ent))
                            continue;

                        _entManager.DeleteEntity(ent);
                        removed++;
                    }
                }
            }
        }

        return removed;
    }

    private List<Entity<MapGridComponent>> GetOrCreateMapGrids(MapId mapId)
    {
        var grids = _mapManager.GetAllGrids(mapId).ToList();
        if (grids.Count > 0)
            return grids;

        var gridEnt = _mapManager.CreateGridEntity(mapId);
        grids.Add(gridEnt);
        return grids;
    }

    private bool TryGetGridBounds(
        EntityUid gridUid,
        MapGridComponent grid,
        out int minX,
        out int maxX,
        out int minY,
        out int maxY)
    {
        minX = int.MaxValue;
        minY = int.MaxValue;
        maxX = int.MinValue;
        maxY = int.MinValue;
        var any = false;

        foreach (var tile in _mapSys.GetAllTiles(gridUid, grid, ignoreEmpty: true))
        {
            any = true;
            if (tile.GridIndices.X < minX)
                minX = tile.GridIndices.X;
            if (tile.GridIndices.X > maxX)
                maxX = tile.GridIndices.X;
            if (tile.GridIndices.Y < minY)
                minY = tile.GridIndices.Y;
            if (tile.GridIndices.Y > maxY)
                maxY = tile.GridIndices.Y;
        }

        return any;
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(CompletionHelper.MapIds(_entManager), "Map Id");
        return CompletionResult.Empty;
    }
}
