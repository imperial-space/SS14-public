using Content.Server.Administration;
using Content.Server.Imperial.Contractor;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class MakeContractorCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public override string Command => "makecontractor";
    public override string Description => "Make a connected traitor a contractor.";
    public override string Help => "makecontractor <ckey>";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _players),
                "<ckey>");
        }

        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var contractor = _entityManager.System<ContractorSystem>();

        if (args.Length != 1)
        {
            shell.WriteError($"Usage: {Help}");
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var player))
        {
            shell.WriteError("Player is not connected.");
            return;
        }

        if (!contractor.TryMakeContractor(player, out var error))
        {
            shell.WriteError(error);
            return;
        }

        shell.WriteLine($"Made {player.Name} a contractor.");
    }
}