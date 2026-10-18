using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>
/// Выдать или забрать боевое искусство. Психотическая драка и искусство джунглей в TG даются
/// травмой мозга и солями для ванн — здесь их источник только эта команда.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class MartialArtCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public string Command => "martialart";
    public string Description => "Обучает боевому искусству или заставляет его забыть.";
    public string Help => $"Использование: {Command} <teach|forget> <искусство> [entityUid]";

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(new[] { "teach", "forget" }, "<teach|forget>"),
            2 => CompletionResult.FromHintOptions(
                _proto.EnumeratePrototypes<MartialArtPrototype>().Select(p => p.ID).Order(),
                "<искусство>"),
            3 => CompletionResult.FromHint("[entityUid]"),
            _ => CompletionResult.Empty,
        };
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 2 or > 3)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!_proto.HasIndex<MartialArtPrototype>(args[1]))
        {
            shell.WriteLine($"Нет такого искусства: «{args[1]}».");
            return;
        }

        EntityUid target;
        if (args.Length == 3)
        {
            if (!_entManager.TryParseNetEntity(args[2], out var netEnt) || !_entManager.EntityExists(netEnt))
            {
                shell.WriteLine($"Сущность не найдена: «{args[2]}».");
                return;
            }

            target = netEnt.Value;
        }
        else if (shell.Player?.AttachedEntity is { Valid: true } playerEntity)
        {
            target = playerEntity;
        }
        else
        {
            shell.WriteLine("Укажите entityUid цели или выберите персонажа.");
            return;
        }

        var system = _entManager.System<MartialArtsSystem>();
        var art = new ProtoId<MartialArtPrototype>(args[1]);
        switch (args[0])
        {
            case "teach":
                shell.WriteLine(system.Teach(target, art)
                    ? $"{target} изучил {art}."
                    : $"Не удалось обучить {target} искусству {art}.");
                break;
            case "forget":
                system.Unlearn(target, art);
                shell.WriteLine($"{target} забыл {art}.");
                break;
            default:
                shell.WriteLine(Help);
                break;
        }
    }
}
