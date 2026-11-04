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
    public string Help => Loc.GetString("imperial-uplink-assault-pod-command-1");

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
