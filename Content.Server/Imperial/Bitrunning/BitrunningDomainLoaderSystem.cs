using System.Globalization;
using System.IO;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>
/// lazy_template/virtual_domain.lazy_load(): строит домен по раскладке, сконвертированной из .dmm TG,
/// на отдельной карте. Зоны TG (safehouse, protected_space) запоминаются по клеткам.
/// </summary>
public sealed class BitrunningDomainLoaderSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly Content.Server.Decals.DecalSystem _decals = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedRoofSystem _roof = default!;
    [Dependency] private readonly Content.Server.Power.EntitySystems.PowerReceiverSystem _power = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private readonly Dictionary<ResPath, DomainLayout> _cache = new();

    public sealed class DomainLayout
    {
        public int Width;
        public int Height;
        public int KeyLength;
        public readonly Dictionary<string, LayoutCell> Legend = new();
        public readonly List<string> Rows = new();
    }

    public sealed class LayoutCell
    {
        public string Tile = "Space";
        public string Area = "";
        public readonly List<(string Proto, float Degrees)> Entities = new();
    }

    public sealed class BuiltDomain
    {
        public EntityUid Map;
        public EntityUid Grid;
        public readonly List<EntityUid> Created = new();
        /// <summary>VIRTUAL_SAFE_AREA: клетки сейфхауса, куда не заходят существа домена.</summary>
        public readonly HashSet<Vector2i> SafeTiles = new();
        /// <summary>protected_space: клетки, которые нельзя разрушить.</summary>
        public readonly HashSet<Vector2i> ProtectedTiles = new();
    }

    public DomainLayout? GetLayout(ResPath path)
    {
        if (_cache.TryGetValue(path, out var cached))
            return cached;

        if (!_res.TryContentFileRead(path, out var stream))
        {
            Log.Error($"Не найдена раскладка домена {path}");
            return null;
        }

        using var reader = new StreamReader(stream);
        var yaml = new YamlStream();
        yaml.Load(reader);
        var root = (YamlMappingNode) yaml.Documents[0].RootNode;

        var layout = new DomainLayout
        {
            Width = int.Parse(((YamlScalarNode) root["width"]).Value!, CultureInfo.InvariantCulture),
            Height = int.Parse(((YamlScalarNode) root["height"]).Value!, CultureInfo.InvariantCulture),
            KeyLength = int.Parse(((YamlScalarNode) root["keyLength"]).Value!, CultureInfo.InvariantCulture),
        };

        foreach (var (keyNode, valueNode) in (YamlMappingNode) root["legend"])
        {
            var cell = new LayoutCell();
            var map = (YamlMappingNode) valueNode;
            if (map.Children.TryGetValue(new YamlScalarNode("t"), out var t))
                cell.Tile = ((YamlScalarNode) t).Value!;
            if (map.Children.TryGetValue(new YamlScalarNode("a"), out var a))
                cell.Area = ((YamlScalarNode) a).Value!;
            if (map.Children.TryGetValue(new YamlScalarNode("e"), out var e))
            {
                foreach (var entNode in (YamlSequenceNode) e)
                {
                    var raw = ((YamlScalarNode) entNode).Value!;
                    var at = raw.IndexOf('@');
                    cell.Entities.Add(at < 0
                        ? (raw, 0f)
                        : (raw[..at], float.Parse(raw[(at + 1)..], CultureInfo.InvariantCulture)));
                }
            }

            layout.Legend[((YamlScalarNode) keyNode).Value!] = cell;
        }

        foreach (var row in (YamlSequenceNode) root["rows"])
        {
            layout.Rows.Add(((YamlScalarNode) row).Value!);
        }

        _cache[path] = layout;
        return layout;
    }

    /// <summary>Строит домен на новой карте. Неизвестные прототипы пропускаются с предупреждением.</summary>
    /// <summary>Области без крыши: /area/virtual_domain/fullbright и поверхность Лаваленда/Ледяной луны.</summary>
    private static readonly HashSet<string> OpenAreas = new() { "bright", "protectedbright", "outdoors", "space" };

    public BuiltDomain? Build(BitrunningDomainPrototype domain)
    {
        var layout = GetLayout(domain.Layout);
        if (layout == null)
            return null;

        var mapUid = _map.CreateMap(out var mapId);
        _meta.SetEntityName(mapUid, Loc.GetString("bitrunning-domain-map-name", ("domain", Loc.GetString(domain.Name))));
        _map.SetAmbientLight(mapId, domain.AmbientLight ?? Color.Black);

        // Виртуальный воздух: обычная станционная смесь на всей карте (у грида домена своей атмосферы нет).
        var air = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        air.AdjustMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard);
        air.AdjustMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard);
        _atmos.SetMapAtmosphere(mapUid, false, air);

        var gridEnt = _mapManager.CreateGridEntity(mapId);
        var grid = gridEnt.Owner;
        _meta.SetEntityName(grid, Loc.GetString(domain.Name));
        var gravity = EnsureComp<GravityComponent>(grid);
        gravity.Enabled = true;
        Dirty(grid, gravity);

        var built = new BuiltDomain { Map = mapUid, Grid = grid };
        var tiles = new List<(Vector2i, Tile)>();
        var tileCache = new Dictionary<string, Tile?>();
        var spawns = new List<(Vector2i Pos, string Proto, float Deg)>();
        var missing = new HashSet<string>();
        var roofed = new List<Vector2i>();

        for (var row = 0; row < layout.Rows.Count; row++)
        {
            var line = layout.Rows[row];
            var y = layout.Height - 1 - row;
            for (var col = 0; col * layout.KeyLength < line.Length; col++)
            {
                var key = line.Substring(col * layout.KeyLength, layout.KeyLength);
                if (!layout.Legend.TryGetValue(key, out var cell))
                    continue;

                var pos = new Vector2i(col, y);
                if (!tileCache.TryGetValue(cell.Tile, out var tile))
                {
                    tile = cell.Tile != "Space" && _tiles.TryGetDefinition(cell.Tile, out var def) ? new Tile(def.TileId) : null;
                    if (tile == null && cell.Tile != "Space")
                        missing.Add(cell.Tile);
                    tileCache[cell.Tile] = tile;
                }

                if (tile is { } t)
                    tiles.Add((pos, t));

                // fullbright и открытая поверхность планет — под светом карты, остальное — под крышей (свет только от ламп).
                if (tile != null && !OpenAreas.Contains(cell.Area))
                    roofed.Add(pos);

                switch (cell.Area)
                {
                    case "safe":
                        built.SafeTiles.Add(pos);
                        built.ProtectedTiles.Add(pos);
                        break;
                    case "protected":
                    case "protectedbright":
                        built.ProtectedTiles.Add(pos);
                        break;
                }

                foreach (var (proto, deg) in cell.Entities)
                {
                    spawns.Add((pos, proto, deg));
                }
            }
        }

        _map.SetTiles(grid, gridEnt.Comp, tiles);

        var roof = EnsureComp<RoofComponent>(grid);
        roof.Color = domain.RoofLight;
        foreach (var pos in roofed)
        {
            _roof.SetRoof((grid, gridEnt.Comp, roof), pos, true);
        }
        Dirty(grid, roof);

        // Новый грид сам получает свою атмосферу с вакуумом на всех клетках — домену она не нужна,
        // тогда действует воздух карты.
        RemComp<Content.Shared.Atmos.Components.GridAtmosphereComponent>(grid);

        foreach (var (pos, proto, deg) in spawns)
        {
            if (proto.StartsWith('#'))
            {
                // Декаль (кусты, трава): в SS14 это не сущности.
                _decals.TryAddDecal(proto[1..], new EntityCoordinates(grid, pos.X, pos.Y), out _, rotation: Angle.FromDegrees(deg), cleanable: false);
                continue;
            }

            if (!_proto.HasIndex<EntityPrototype>(proto))
            {
                missing.Add(proto);
                continue;
            }

            var ent = Spawn(proto, new EntityCoordinates(grid, pos.X + 0.5f, pos.Y + 0.5f));
            if (deg != 0f)
                _transform.SetLocalRotation(ent, Angle.FromDegrees(deg));
            built.Created.Add(ent);
        }

        // requires_power = FALSE у областей домена: шлюзы, автоматы и компьютеры работают без сети.
        foreach (var ent in built.Created)
        {
            if (TryComp<Content.Server.Power.Components.ApcPowerReceiverComponent>(ent, out var receiver))
                _power.SetNeedsPower(ent, false, receiver);
        }

        if (missing.Count > 0)
            Log.Warning($"Домен {domain.ID}: пропущены неизвестные прототипы: {string.Join(", ", missing)}");

        return built;
    }

    /// <summary>Клетка грида домена, на которой стоит сущность.</summary>
    public Vector2i TileOf(EntityUid grid, EntityCoordinates coords)
    {
        return TryComp<MapGridComponent>(grid, out var mapGrid)
            ? _map.TileIndicesFor(grid, mapGrid, coords)
            : Vector2i.Zero;
    }
}
