using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>Вызывает событие в загруженном домене: существо «мутирует», призраки получают роль глюка.</summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class BitrunningEventCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private static readonly Dictionary<string, EntProtoId?> Events = new()
    {
        ["police"] = "BitrunningGlitchSpawnerCyberPolice",
        ["tac"] = "BitrunningGlitchSpawnerCyberTac",
        ["netguardian"] = "BitrunningGlitchSpawnerNetguardian",
        ["random"] = null,
    };

    public string Command => "bitrunevent";
    public string Description => Loc.GetString("imperial-bitrunning-bitrunning-event-command-1");
    public string Help => $"Использование: {Command} <police|tac|netguardian|random> [entityUid сервера]\n" +
                          "random — роль по уровню угрозы сервера, как в обычном сбое.";

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(Events.Keys, "<событие>"),
            2 => CompletionResult.FromHint("[entityUid сервера]"),
            _ => CompletionResult.Empty,
        };
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2 || !Events.TryGetValue(args[0], out var spawner))
        {
            shell.WriteLine(Help);
            return;
        }

        var servers = _entManager.System<QuantumServerSystem>();
        Entity<QuantumServerComponent>? server = null;
        if (args.Length == 2)
        {
            if (!_entManager.TryParseNetEntity(args[1], out var netEnt) || !_entManager.EntityExists(netEnt))
            {
                shell.WriteLine(Loc.GetString("imperial-bitrunning-bitrunning-event-command-2", ("arg0", args[1])));
                return;
            }

            server = _entManager.TryGetComponent<QuantumServerComponent>(netEnt.Value, out var direct)
                ? (netEnt.Value, direct)
                : servers.FindServer(netEnt.Value);
        }
        else
        {
            var query = _entManager.EntityQueryEnumerator<QuantumServerComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.Domain != null && comp.DomainMap != null)
                {
                    server = (uid, comp);
                    break;
                }
            }
        }

        if (server is not { } found || found.Comp.Domain == null)
        {
            shell.WriteLine(Loc.GetString("imperial-bitrunning-bitrunning-event-command-3"));
            return;
        }

        if (!servers.SetupGlitch(found, spawner))
        {
            shell.WriteLine(Loc.GetString("imperial-bitrunning-bitrunning-event-command-4"));
            return;
        }

        shell.WriteLine(Loc.GetString("imperial-bitrunning-bitrunning-event-command-5", ("arg0", args[0]), ("arg1", found.Comp.Domain), ("arg2", _entManager.ToPrettyString(found))));
    }
}
