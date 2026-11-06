using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Imperial.ZLevels;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.ZLevels;

/// <summary>
/// Маппинг этажей. Этажи — отдельные карты, совмещённые по мировым координатам; нижний хранит пути остальных.
///   zlevel info                     — этаж под тобой и его соседи
///   zlevel set &lt;стопка&gt; &lt;этаж&gt;      — сделать текущую карту этажом стопки
///   zlevel newabove | newbelow      — создать пустой этаж выше/ниже и перейти на него
///   zlevel up | down                — перейти на этаж выше/ниже в ту же точку
///   zlevel link &lt;ниже&gt; &lt;выше&gt;        — связать две карты по MapId
///   zlevel save &lt;путь.yml&gt;           — сохранить всю стопку: нижний этаж в путь, остальные в путь_zN.yml
///   zlevel load &lt;путь.yml&gt;           — загрузить нижний этаж и все его этажи для правки (на паузе)
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed class ZLevelCommand : LocalizedEntityCommands
{
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;

    public override string Command => "zlevel";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromOptions(new[] { "info", "set", "newabove", "newbelow", "up", "down", "link", "save", "load" });
        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        switch (args[0])
        {
            case "info":
                Info(shell);
                break;
            case "set" when args.Length >= 3 && int.TryParse(args[2], out var level):
                if (CurrentMap(shell) is { } map)
                {
                    _z.SetLevel(map, args[1], level);
                    shell.WriteLine(Loc.GetString("cmd-zlevel-set-done", ("stack", args[1]), ("level", level)));
                }
                break;
            case "newabove":
                NewLevel(shell, true);
                break;
            case "newbelow":
                NewLevel(shell, false);
                break;
            case "up":
                Step(shell, true);
                break;
            case "down":
                Step(shell, false);
                break;
            case "link" when args.Length >= 3 && int.TryParse(args[1], out var below) && int.TryParse(args[2], out var above):
                Link(shell, new MapId(below), new MapId(above));
                break;
            case "save" when args.Length >= 2:
                Save(shell, new ResPath(args[1]));
                break;
            case "load" when args.Length >= 2:
                Load(shell, new ResPath(args[1]));
                break;
            default:
                shell.WriteLine(Help);
                break;
        }
    }

    private EntityUid? CurrentMap(IConsoleShell shell)
    {
        if (shell.Player?.AttachedEntity is not { } player || EntityManager.GetComponent<TransformComponent>(player).MapUid is not { } map)
        {
            shell.WriteError(Loc.GetString("cmd-zlevel-no-map"));
            return null;
        }
        return map;
    }

    private void Info(IConsoleShell shell)
    {
        if (CurrentMap(shell) is not { } map)
            return;
        if (!EntityManager.TryGetComponent<ZLevelMapComponent>(map, out var z) || string.IsNullOrEmpty(z.Stack))
        {
            shell.WriteLine(Loc.GetString("cmd-zlevel-info-none"));
            return;
        }
        foreach (var level in _z.GetStack(z.Stack))
        {
            var mark = level.Owner == map ? " <-" : string.Empty;
            shell.WriteLine($"  {level.Comp.Level}: MapId {EntityManager.GetComponent<MapComponent>(level).MapId} {EntityManager.GetComponent<MetaDataComponent>(level).EntityName}{mark}");
        }
    }

    /// <summary>Пустая карта этажом выше/ниже: та же пауза и инициализация, что у текущей; маппер переходит на неё.</summary>
    private void NewLevel(IConsoleShell shell, bool up)
    {
        if (CurrentMap(shell) is not { } map)
            return;
        var z = EntityManager.EnsureComponent<ZLevelMapComponent>(map);
        if (string.IsNullOrEmpty(z.Stack))
            _z.SetLevel(map, $"stack-{map.Id}", 0);
        if (_z.GetAdjacentMap(map, up) != null)
        {
            shell.WriteError(Loc.GetString("cmd-zlevel-exists"));
            return;
        }

        var initialized = _map.IsInitialized(map);
        var newMap = _map.CreateMap(out var newId, initialized);
        if (_map.IsPaused(map))
            _map.SetPaused(newId, true);
        _z.SetLevel(newMap, z.Stack, z.Level + (up ? 1 : -1));
        shell.WriteLine(Loc.GetString("cmd-zlevel-created", ("map", newId), ("level", z.Level + (up ? 1 : -1))));
        Step(shell, up);
    }

    private void Step(IConsoleShell shell, bool up)
    {
        if (shell.Player?.AttachedEntity is not { } player)
            return;
        var coords = _xform.GetMapCoordinates(player);
        if (_z.GetStep(coords, up) is not { } target)
        {
            shell.WriteError(Loc.GetString(up ? "zlevel-nothing-above" : "zlevel-nothing-below"));
            return;
        }
        _xform.SetMapCoordinates(player, target);
        _xform.AttachToGridOrMap(player);
    }

    private void Link(IConsoleShell shell, MapId below, MapId above)
    {
        if (!_map.TryGetMap(below, out var belowUid) || !_map.TryGetMap(above, out var aboveUid))
        {
            shell.WriteError(Loc.GetString("cmd-zlevel-no-such-map"));
            return;
        }
        var bz = EntityManager.EnsureComponent<ZLevelMapComponent>(belowUid.Value);
        if (string.IsNullOrEmpty(bz.Stack))
            _z.SetLevel(belowUid.Value, $"stack-{belowUid.Value.Id}", 0);
        _z.SetLevel(aboveUid.Value, bz.Stack, bz.Level + 1);
        shell.WriteLine(Loc.GetString("cmd-zlevel-linked", ("below", below), ("above", above)));
    }

    /// <summary>
    /// Сохраняет стопку: нижний этаж — в указанный файл с <see cref="ZLevelStackComponent"/>, остальные — рядом
    /// с суффиксом _zN. При загрузке нижнего этажа (раунд или <c>zlevel load</c>) остальные подтянутся сами.
    /// </summary>
    private void Save(IConsoleShell shell, ResPath path)
    {
        if (CurrentMap(shell) is not { } map || !EntityManager.TryGetComponent<ZLevelMapComponent>(map, out var z) || string.IsNullOrEmpty(z.Stack))
        {
            shell.WriteError(Loc.GetString("cmd-zlevel-info-none"));
            return;
        }
        var levels = _z.GetStack(z.Stack);
        var bottom = levels.First();
        var stackComp = EntityManager.EnsureComponent<ZLevelStackComponent>(bottom);
        stackComp.Levels.Clear();

        var dir = path.Directory;
        var stem = path.FilenameWithoutExtension;
        var ext = path.Extension;
        foreach (var level in levels.Skip(1))
        {
            var rel = level.Comp.Level - bottom.Comp.Level;
            var levelPath = dir / $"{stem}_z{rel}.{ext}";
            stackComp.Levels[rel] = levelPath;
            if (!_loader.TrySaveMap(level.Owner, levelPath))
            {
                shell.WriteError(Loc.GetString("cmd-zlevel-save-failed", ("path", levelPath)));
                return;
            }
            shell.WriteLine(Loc.GetString("cmd-zlevel-saved", ("path", levelPath)));
        }

        if (!_loader.TrySaveMap(bottom.Owner, path))
        {
            shell.WriteError(Loc.GetString("cmd-zlevel-save-failed", ("path", path)));
            return;
        }
        shell.WriteLine(Loc.GetString("cmd-zlevel-saved", ("path", path)));
    }

    /// <summary>Загружает нижний этаж и все его этажи на паузе, для правки.</summary>
    private void Load(IConsoleShell shell, ResPath path)
    {
        var opts = new DeserializationOptions { InitializeMaps = false, PauseMaps = true };
        if (!_loader.TryLoadMap(path, out var map, out _, opts))
        {
            shell.WriteError(Loc.GetString("cmd-zlevel-load-failed", ("path", path)));
            return;
        }
        var stack = EntityManager.EnsureComponent<ZLevelStackComponent>(map.Value);
        var loaded = _z.LoadStack((map.Value.Owner, stack), false);
        shell.WriteLine(Loc.GetString("cmd-zlevel-loaded", ("map", map.Value.Comp.MapId), ("count", loaded.Count + 1)));
        if (shell.Player?.AttachedEntity is { } player)
        {
            _xform.SetMapCoordinates(player, new MapCoordinates(System.Numerics.Vector2.Zero, map.Value.Comp.MapId));
            _xform.AttachToGridOrMap(player);
        }
    }
}
