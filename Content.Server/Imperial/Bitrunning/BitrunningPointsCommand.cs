using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>Начисляет очки квантовому серверу (покупка доменов) или битраннеру (торгомат NexaCache).</summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class BitrunningPointsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "bitrunpoints";
    public string Description => Loc.GetString("imperial-bitrunning-bitrunning-points-command-1");
    public string Help => Loc.GetString("imperial-bitrunning-points-command-help", ("command", Command));

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(new[] { "server", "nexa" }, "<server|nexa>"),
            2 => CompletionResult.FromHint(Loc.GetString("imperial-bitrunning-points-command-hint-amount")),
            3 => CompletionResult.FromHint("[entityUid]"),
            _ => CompletionResult.Empty,
        };
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 2 or > 3 || !int.TryParse(args[1], out var amount))
        {
            shell.WriteLine(Help);
            return;
        }

        EntityUid? target = null;
        if (args.Length == 3)
        {
            if (!_entManager.TryParseNetEntity(args[2], out var netEnt) || !_entManager.EntityExists(netEnt))
            {
                shell.WriteLine(Loc.GetString("imperial-bitrunning-bitrunning-points-command-2", ("arg0", args[2])));
                return;
            }

            target = netEnt.Value;
        }

        switch (args[0])
        {
            case "server":
                AddServerPoints(shell, amount, target);
                break;
            case "nexa":
                AddNexaPoints(shell, amount, target);
                break;
            default:
                shell.WriteLine(Help);
                break;
        }
    }

    private void AddServerPoints(IConsoleShell shell, int amount, EntityUid? target)
    {
        var servers = _entManager.System<QuantumServerSystem>();
        var list = new List<Entity<QuantumServerComponent>>();
        if (target is { } uid)
        {
            if (_entManager.TryGetComponent<QuantumServerComponent>(uid, out var direct))
                list.Add((uid, direct));
            else if (servers.FindServer(uid) is { } near)
                list.Add(near);
        }
        else
        {
            var query = _entManager.EntityQueryEnumerator<QuantumServerComponent>();
            while (query.MoveNext(out var uid2, out var comp))
            {
                list.Add((uid2, comp));
            }
        }

        if (list.Count == 0)
        {
            shell.WriteLine(Loc.GetString("imperial-bitrunning-bitrunning-points-command-3"));
            return;
        }

        foreach (var server in list)
        {
            server.Comp.Points = Math.Max(0, server.Comp.Points + amount);
            shell.WriteLine(Loc.GetString("imperial-bitrunning-bitrunning-points-command-4", ("arg0", _entManager.ToPrettyString(server)), ("arg1", server.Comp.Points)));
        }
    }

    private void AddNexaPoints(IConsoleShell shell, int amount, EntityUid? target)
    {
        target ??= shell.Player?.AttachedEntity;
        if (target is not { } uid)
        {
            shell.WriteLine(Loc.GetString("imperial-bitrunning-bitrunning-points-command-5"));
            return;
        }

        var shop = _entManager.System<BitrunningShopSystem>();
        if (!shop.AddPoints(uid, amount))
        {
            shell.WriteLine(Loc.GetString("imperial-bitrunning-bitrunning-points-command-6", ("arg0", _entManager.ToPrettyString(uid))));
            return;
        }

        shell.WriteLine(Loc.GetString("imperial-bitrunning-bitrunning-points-command-7", ("arg0", _entManager.ToPrettyString(uid)), ("arg1", shop.GetPoints(uid))));
    }
}
