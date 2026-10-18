using Content.Server.Administration;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Imperial.Changeling;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class MakeImperialAntagCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    private static readonly EntProtoId CultRule = "Cult";
    private static readonly EntProtoId BlobRule = "Blob";
    private static readonly EntProtoId ChangelingRule = "ImperialChangelingRule";

    public override string Command => "makeimperialantag";
    public override string Description => "Make a player a cultist, blob, or changeling.";
    public override string Help => "makeimperialantag <ckey> <cult|blob|changeling>";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _players),
                "<ckey>");
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(
                [
                    new CompletionOption("cult", "Make the player a cultist."),
                    new CompletionOption("blob", "Make the player a blob."),
                    new CompletionOption("changeling", "Make the player a changeling.")
                ],
                "<cult|blob|changeling>");
        }

        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var antag = _entityManager.System<AntagSelectionSystem>();

        if (args.Length != 2)
        {
            shell.WriteError($"Usage: {Help}");
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var player))
        {
            shell.WriteError("Player is not connected.");
            return;
        }

        switch (args[1].ToLowerInvariant())
        {
            case "cult":
                antag.ForceMakeAntag<CultRuleComponent>(player, CultRule);
                shell.WriteLine($"Made {player.Name} a cultist.");
                break;

            case "blob":
                antag.ForceMakeAntag<BlobRuleComponent>(player, BlobRule);
                shell.WriteLine($"Made {player.Name} a blob.");
                break;

            case "changeling":
                antag.ForceMakeAntag<ImperialChangelingRuleComponent>(player, ChangelingRule);
                shell.WriteLine($"Made {player.Name} a changeling.");
                break;

            default:
                shell.WriteError("Role must be either 'cult' or 'blob'.");
                break;
        }
    }
}