using System.Linq;
using System.Text;
using Content.Client.Construction;
using Content.Client.Popups;
using Content.Shared.Construction.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Blueprints;

/// <summary>
/// Чертежи построек: призраки из меню строительства сохраняются в YAML-файл у игрока (папка Blueprints в данных
/// клиента) и в любом следующем раунде ставятся обратно целиком — с поворотом, как обычная постройка.
/// </summary>
public sealed class ImperialBlueprintSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly IPlacementManager _placement = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly ConstructionSystem _construction = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public static readonly ResPath Folder = new("/Blueprints");
    private const int MaxNameLength = 40;

    private ImperialBlueprintWindow? _window;
    private ImperialBlueprintPlacementHijack? _hijack;
    private bool _stopPlacing;
    private readonly Dictionary<string, Texture?> _icons = new();

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new ImperialBlueprintOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<ImperialBlueprintOverlay>();
        _window?.Close();
    }

    // ---------- Окно ----------

    public void ToggleWindow()
    {
        if (_window is { IsOpen: true })
        {
            _window.Close();
            return;
        }

        _window ??= new ImperialBlueprintWindow(this);
        _window.OpenCentered();
        _window.Refresh();
    }

    // ---------- Сохранение ----------

    /// <summary>Призраки строительства игрока: сетка → призраки на ней.</summary>
    private Dictionary<EntityUid, List<(ConstructionPrototype Proto, EntityCoordinates Coords, Angle Rotation)>> GhostsByGrid()
    {
        var result = new Dictionary<EntityUid, List<(ConstructionPrototype, EntityCoordinates, Angle)>>();
        var query = EntityQueryEnumerator<ConstructionGhostComponent, TransformComponent>();
        while (query.MoveNext(out _, out var ghost, out var xform))
        {
            if (ghost.Prototype == null || xform.GridUid is not { } grid)
                continue;

            if (!result.TryGetValue(grid, out var list))
                result[grid] = list = new();
            list.Add((ghost.Prototype, xform.Coordinates, xform.LocalRotation));
        }

        return result;
    }

    public int GhostCount()
    {
        return GhostsByGrid().Values.Sum(l => l.Count);
    }

    /// <summary>
    /// Сохранить призраков в чертёж. Если призраки на нескольких сетках — берётся та, где их больше всего.
    /// Клетки считаются в осях сетки от левого нижнего угла, направление — относительно сетки.
    /// </summary>
    public bool TrySave(string name, out string message)
    {
        name = name.Trim();
        if (name.Length == 0)
        {
            message = Loc.GetString("imperial-blueprint-error-name");
            return false;
        }

        if (name.Length > MaxNameLength)
            name = name[..MaxNameLength];

        var grids = GhostsByGrid();
        if (grids.Count == 0)
        {
            message = Loc.GetString("imperial-blueprint-error-empty");
            return false;
        }

        var (gridUid, ghosts) = grids.MaxBy(g => g.Value.Count);
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
        {
            message = Loc.GetString("imperial-blueprint-error-empty");
            return false;
        }

        var cells = ghosts.Select(g => (g.Proto, Tile: _map.LocalToTile(gridUid, grid, g.Coords), g.Rotation)).ToList();
        var minX = cells.Min(c => c.Tile.X);
        var minY = cells.Min(c => c.Tile.Y);

        var blueprint = new ImperialBlueprint
        {
            Name = name,
            Author = _player.LocalSession?.Name ?? string.Empty,
            Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            File = FileName(name),
        };

        foreach (var (proto, tile, rotation) in cells.OrderBy(c => c.Tile.Y).ThenBy(c => c.Tile.X))
        {
            blueprint.Entries.Add(new ImperialBlueprintEntry(proto.ID, new Vector2i(tile.X - minX, tile.Y - minY), rotation.GetCardinalDir()));
        }

        try
        {
            _resources.UserData.CreateDir(Folder);
            using var writer = _resources.UserData.OpenWriteText(Folder / blueprint.File);
            blueprint.Write(writer);
        }
        catch (Exception e)
        {
            Log.Error($"Не удалось сохранить чертёж {blueprint.File}: {e}");
            message = Loc.GetString("imperial-blueprint-error-io");
            return false;
        }

        message = Loc.GetString("imperial-blueprint-saved", ("name", name), ("count", blueprint.Entries.Count));
        return true;
    }

    /// <summary>Имя файла из названия: буквы, цифры, пробел, дефис и подчёркивание.</summary>
    private static string FileName(string name)
    {
        var builder = new StringBuilder();
        foreach (var ch in name)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_');
        }

        return builder.ToString().Trim('_') is { Length: > 0 } clean ? clean + ".yml" : "blueprint.yml";
    }

    // ---------- Список ----------

    public List<ImperialBlueprint> List()
    {
        var list = new List<ImperialBlueprint>();
        if (!_resources.UserData.IsDir(Folder))
            return list;

        foreach (var file in _resources.UserData.DirectoryEntries(Folder))
        {
            if (!file.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var reader = _resources.UserData.OpenText(Folder / file);
                if (ImperialBlueprint.Read(reader) is not { } blueprint)
                    continue;

                blueprint.File = file;
                if (blueprint.Name.Length == 0)
                    blueprint.Name = file[..^".yml".Length]; // System.IO.Path песочница клиента не пропускает
                list.Add(blueprint);
            }
            catch (Exception e)
            {
                Log.Warning($"Чертёж {file} не прочитан: {e.Message}");
            }
        }

        return list.OrderBy(b => b.Name).ToList();
    }

    public void Delete(ImperialBlueprint blueprint)
    {
        var path = Folder / blueprint.File;
        if (_resources.UserData.Exists(path))
            _resources.UserData.Delete(path);
    }

    public void OpenFolder()
    {
        _resources.UserData.CreateDir(Folder);
        _resources.UserData.OpenOsWindow(Folder);
    }

    // ---------- Размещение ----------

    /// <summary>Чертёж на курсоре: ЛКМ — поставить, R — повернуть, ПКМ — отмена.</summary>
    public void StartPlacing(ImperialBlueprint blueprint)
    {
        _hijack = new ImperialBlueprintPlacementHijack(this, blueprint);
        _placement.BeginPlacing(new PlacementInformation
            {
                IsTile = false,
                PlacementOption = "SnapgridCenter",
            },
            _hijack);
    }

    /// <summary>Что сейчас висит на курсоре (для предпросмотра).</summary>
    public (ImperialBlueprint Blueprint, EntityCoordinates Coords, Direction Direction)? Preview()
    {
        if (_hijack is not { Active: true } || _placement.CurrentMode is not { } mode)
            return null;

        return (_hijack.Blueprint, mode.MouseCoords, _placement.Direction);
    }

    /// <summary>
    /// Клетки чертежа на сетке: поворот <paramref name="direction"/> вокруг угла чертежа, угол — в клетке курсора.
    /// </summary>
    public IEnumerable<(ImperialBlueprintEntry Entry, Vector2i Tile, Direction Direction)> Layout(
        ImperialBlueprint blueprint, Vector2i origin, Direction direction)
    {
        var rotation = direction.ToAngle();
        foreach (var entry in blueprint.Entries)
        {
            var offset = rotation.RotateVec(entry.Offset);
            var tile = origin + new Vector2i((int) MathF.Round(offset.X), (int) MathF.Round(offset.Y));
            yield return (entry, tile, (entry.Direction.ToAngle() + rotation).GetCardinalDir());
        }
    }

    public void Place(ImperialBlueprint blueprint, EntityCoordinates coords, Direction direction)
    {
        _stopPlacing = true;
        if (_xform.GetGrid(coords) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            _popup.PopupCoordinates(Loc.GetString("imperial-blueprint-error-grid"), coords);
            return;
        }

        var origin = _map.TileIndicesFor(gridUid, grid, coords);
        int placed = 0, skipped = 0, unknown = 0;
        foreach (var (entry, tile, dir) in Layout(blueprint, origin, direction))
        {
            if (!_proto.TryIndex<ConstructionPrototype>(entry.Construction, out var proto))
            {
                unknown++;
                continue;
            }

            var loc = _map.GridTileToLocal(gridUid, grid, tile);
            if (_construction.TrySpawnGhost(proto, loc, dir, out _, fromBlueprint: true))
                placed++;
            else
                skipped++;
        }

        _popup.PopupCoordinates(Loc.GetString("imperial-blueprint-placed",
            ("placed", placed), ("skipped", skipped), ("unknown", unknown)), coords);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Чертёж поставлен — снимаем его с курсора уже после обработки клика.
        if (_stopPlacing)
        {
            _stopPlacing = false;
            if (_hijack is { Active: true })
                _placement.Clear();
            _hijack = null;
        }
    }

    /// <summary>Иконка постройки рецепта (кэш).</summary>
    public Texture? Icon(string construction)
    {
        if (_icons.TryGetValue(construction, out var cached))
            return cached;

        Texture? texture = null;
        if (_construction.TryGetRecipePrototype(construction, out var target) && _proto.TryIndex<EntityPrototype>(target, out var proto))
            texture = _sprite.GetPrototypeIcon(proto).Default;

        _icons[construction] = texture;
        return texture;
    }
}

/// <summary>Перехват размещения: клик по карте ставит весь чертёж, R поворачивает его.</summary>
public sealed class ImperialBlueprintPlacementHijack(ImperialBlueprintSystem system, ImperialBlueprint blueprint) : PlacementHijack
{
    public readonly ImperialBlueprint Blueprint = blueprint;

    public override bool CanRotate => true;

    /// <summary>Этот перехват сейчас управляет размещением (до StartHijack менеджера ещё нет).</summary>
    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    public bool Active => Manager != null && Manager.Hijack == this;

    public override bool HijackPlacementRequest(EntityCoordinates coordinates)
    {
        system.Place(Blueprint, coordinates, Manager.Direction);
        return true;
    }

    public override bool HijackDeletion(EntityUid entity)
    {
        return true;
    }

    public override void StartHijack(PlacementManager manager)
    {
        base.StartHijack(manager);

        // Свой предпросмотр рисует оверлей — у курсора текстур нет.
        manager.CurrentTextures = new List<IDirectionalTextureProvider>();
    }
}
