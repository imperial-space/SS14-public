using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Heretic;

[AdminCommand(AdminFlags.Fun)]
sealed class GiveHereticKnowledgeCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command     => "hereticknowledge";
    public string Description => "Выдаёт очки знания еретику.";
    public string Help        => $"Использование: {Command} <количество> [entityUid]";

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHint("<количество>");
        if (args.Length == 2)
            return CompletionResult.FromHint("[entityUid]");
        return CompletionResult.Empty;
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!int.TryParse(args[0], out var amount))
        {
            shell.WriteLine($"Не удалось распознать количество: «{args[0]}».");
            return;
        }

        EntityUid target;

        if (args.Length == 2)
        {
            if (!_entManager.TryParseNetEntity(args[1], out var netEnt) || !_entManager.EntityExists(netEnt))
            {
                shell.WriteLine($"Сущность не найдена: «{args[1]}».");
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

        if (!_entManager.TryGetComponent<HereticComponent>(target, out var comp))
        {
            shell.WriteLine("Цель не является еретиком.");
            return;
        }

        _entManager.System<HereticSystem>().AddKnowledgePoints(target, comp, amount);
        shell.WriteLine($"Выдано {amount} очков знания еретику {target}.");
    }
}
