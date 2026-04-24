using Content.Server.Administration;
using Content.Server.GameTicking.Rules;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class MakeBloodBrotherPairCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public override string Command => "makebloodbrotherpair";
    public override string Description => "Make two connected players blood brothers.";
    public override string Help => "makebloodbrotherpair <firstCkey> <secondCkey>";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length is 1 or 2)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _players),
                "<ckey>");
        }

        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var bloodBrother = _entityManager.System<BloodBrotherRuleSystem>();

        if (args.Length != 2)
        {
            shell.WriteError($"Usage: {Help}");
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var firstPlayer))
        {
            shell.WriteError($"Player '{args[0]}' is not connected.");
            return;
        }

        if (!_players.TryGetSessionByUsername(args[1], out var secondPlayer))
        {
            shell.WriteError($"Player '{args[1]}' is not connected.");
            return;
        }

        if (firstPlayer == secondPlayer)
        {
            shell.WriteError("Players must be different.");
            return;
        }

        if (!bloodBrother.TryMakeBloodBrotherPair(firstPlayer, secondPlayer))
        {
            shell.WriteError("Unable to create a blood brother pair.");
            return;
        }

        shell.WriteLine($"Made {firstPlayer.Name} and {secondPlayer.Name} blood brothers.");
    }
}