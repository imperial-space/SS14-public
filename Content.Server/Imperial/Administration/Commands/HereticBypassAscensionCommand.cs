using Content.Server.Administration;
using Content.Server.Imperial.Heretic;
using Content.Shared.Administration;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class HereticBypassAscensionCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public override string Command => "heretic_bypass_ascension";
    public override string Description => "Bypass ascension objectives check for a heretic player.";
    public override string Help => $"Usage: {Command} <ckey>";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _players), "<ckey>");
        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
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

        var uid = player.AttachedEntity;
        if (uid == null)
        {
            shell.WriteError("Player has no attached entity.");
            return;
        }

        if (!_entityManager.HasComponent<HereticComponent>(uid.Value))
        {
            shell.WriteError("Player is not a heretic.");
            return;
        }

        _entityManager.System<HereticSystem>().SetAscensionBypass(uid.Value, true);

        shell.WriteLine($"Ascension bypass enabled for {player.Name}.");
    }
}
