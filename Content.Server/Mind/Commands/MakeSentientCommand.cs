using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Emoting;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Speech;
using Robust.Shared.Console;

namespace Content.Server.Mind.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class MakeSentientCommand : LocalizedEntityCommands
{
    [Dependency] private readonly MindSystem _mindSystem = default!;

    public override string Command => "makesentient";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Loc.GetString("shell-need-exactly-one-argument"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var entNet) || !EntityManager.TryGetEntity(entNet, out var entId) || !EntityManager.EntityExists(entId))
        {
            shell.WriteLine(Loc.GetString("shell-could-not-find-entity-with-uid", ("uid", args[0])));
            return;
        }

        _mindSystem.MakeSentient(entId.Value);
    }
}
