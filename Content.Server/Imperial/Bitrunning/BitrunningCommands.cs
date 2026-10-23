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
    public string Description => "Начисляет очки битраннинга: серверу на домены или игроку на торгомат NexaCache.";
    public string Help => $"Использование: {Command} <server|nexa> <количество> [entityUid]\n" +
                          "server — очки квантового сервера (без entityUid — всем серверам; можно указать сервер или консоль).\n" +
                          "nexa — очки NexaCache на банковский счёт (без entityUid — вашему персонажу).";

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(new[] { "server", "nexa" }, "<server|nexa>"),
            2 => CompletionResult.FromHint("<количество>"),
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
                shell.WriteLine($"Сущность не найдена: «{args[2]}».");
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
            shell.WriteLine("Квантовый сервер не найден.");
            return;
        }

        foreach (var server in list)
        {
            server.Comp.Points = Math.Max(0, server.Comp.Points + amount);
            shell.WriteLine($"{_entManager.ToPrettyString(server)}: очков сервера {server.Comp.Points}.");
        }
    }

    private void AddNexaPoints(IConsoleShell shell, int amount, EntityUid? target)
    {
        target ??= shell.Player?.AttachedEntity;
        if (target is not { } uid)
        {
            shell.WriteLine("Укажите entityUid персонажа или выберите его.");
            return;
        }

        var shop = _entManager.System<BitrunningShopSystem>();
        if (!shop.AddPoints(uid, amount))
        {
            shell.WriteLine($"У {_entManager.ToPrettyString(uid)} нет банковского счёта (ID-карта или КПК с NT Pay).");
            return;
        }

        shell.WriteLine($"{_entManager.ToPrettyString(uid)}: очков NexaCache {shop.GetPoints(uid)}.");
    }
}

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
    public string Description => "Вызывает сбой в загруженном виртуальном домене: киберполиция, кибертактик или нетгардиан.";
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
                shell.WriteLine($"Сущность не найдена: «{args[1]}».");
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
            shell.WriteLine("Нет квантового сервера с загруженным доменом.");
            return;
        }

        if (!servers.SetupGlitch(found, spawner))
        {
            shell.WriteLine("В домене нет живых существ без разума, которые могли бы мутировать.");
            return;
        }

        shell.WriteLine($"Сбой «{args[0]}» вызван в домене {found.Comp.Domain} ({_entManager.ToPrettyString(found)}). Роль ждёт призраков.");
    }
}
