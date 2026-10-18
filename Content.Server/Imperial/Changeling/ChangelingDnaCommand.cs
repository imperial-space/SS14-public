using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Imperial.Changeling;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Changeling;

/// <summary>Выдать генокраду очки генов (ДНК в магазине эволюции) и, по желанию, поглощения.</summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class ChangelingDnaCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly ImperialChangelingSystem _changeling = default!;

    public override string Command => "changelingdna";
    public override string Description => "Выдать генокраду ДНК (очки генов) и поглощения.";
    public override string Help => "changelingdna <ckey> <ДНК> [поглощения]";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _players), "<ckey>"),
            2 => CompletionResult.FromHint("<ДНК>"),
            3 => CompletionResult.FromHint("[поглощения]"),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 2 or > 3 || !int.TryParse(args[1], out var dna))
        {
            shell.WriteError($"Использование: {Help}");
            return;
        }

        var absorbs = 0;
        if (args.Length == 3 && !int.TryParse(args[2], out absorbs))
        {
            shell.WriteError($"Использование: {Help}");
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var player) || player.AttachedEntity is not { } body
            || !EntityManager.TryGetComponent<ImperialChangelingComponent>(body, out var ling))
        {
            shell.WriteError("Игрок не найден или не генокрад.");
            return;
        }

        _changeling.GiveDna((body, ling), dna, absorbs);
        shell.WriteLine($"{player.Name}: ДНК {ling.GeneticPoints}/{ling.TotalGeneticPoints}, поглощений {ling.TrueAbsorbs}.");
    }
}
