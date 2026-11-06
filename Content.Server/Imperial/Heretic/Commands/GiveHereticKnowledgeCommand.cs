using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Imperial.Heretic.Core;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Heretic.Commands;

[AdminCommand(AdminFlags.Fun)]
sealed class GiveHereticKnowledgeCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "hereticknowledge";
    public string Description => Loc.GetString("imperial-heretic-give-heretic-knowledge-command-1");
    public string Help => Loc.GetString("imperial-heretic-give-heretic-knowledge-command-2", ("arg0", Command));

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHint(Loc.GetString("imperial-heretic-give-heretic-knowledge-command-amount-hint"));
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
            shell.WriteLine(Loc.GetString("imperial-heretic-give-heretic-knowledge-command-3", ("arg0", args[0])));
            return;
        }

        EntityUid target;

        if (args.Length == 2)
        {
            if (!_entManager.TryParseNetEntity(args[1], out var netEnt) || !_entManager.EntityExists(netEnt))
            {
                shell.WriteLine(Loc.GetString("imperial-heretic-give-heretic-knowledge-command-4", ("arg0", args[1])));
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
            shell.WriteLine(Loc.GetString("imperial-heretic-give-heretic-knowledge-command-5"));
            return;
        }

        if (!_entManager.TryGetComponent<HereticComponent>(target, out var comp))
        {
            shell.WriteLine(Loc.GetString("imperial-heretic-give-heretic-knowledge-command-6"));
            return;
        }

        _entManager.System<HereticSystem>().AddKnowledgePoints(target, comp, amount);
        shell.WriteLine(Loc.GetString("imperial-heretic-give-heretic-knowledge-command-7", ("arg0", amount), ("arg1", target)));
    }
}
