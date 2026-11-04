using System.Linq;
using Content.Server.Administration;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.Wizard.GrandRitual;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Shared.Administration;
using Content.Shared.Dataset;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Robust.Shared.Console;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

/// <summary>makewizard [entityUid]: сделать персонажа магом Федерации (снаряжение, книга, цели).</summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class MakeImperialWizardCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "makewizard";
    public string Description => Loc.GetString("imperial-wizard-make-imperial-wizard-command-1");
    public string Help => Loc.GetString("imperial-wizard-make-imperial-wizard-command-2", ("arg0", Command));

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        EntityUid? target = shell.Player?.AttachedEntity;
        if (args.Length > 0)
        {
            if (!_entManager.TryParseNetEntity(args[0], out var net) || !_entManager.EntityExists(net))
            {
                shell.WriteLine(Loc.GetString("imperial-wizard-make-imperial-wizard-command-3", ("arg0", args[0])));
                return;
            }

            target = net.Value;
        }

        if (target == null)
        {
            shell.WriteLine(Help);
            return;
        }

        var ok = _entManager.System<ImperialWizardRuleSystem>().MakeWizard(target.Value);
        shell.WriteLine(ok ? Loc.GetString("imperial-wizard-make-imperial-wizard-command-4") : Loc.GetString("imperial-wizard-make-imperial-wizard-command-5"));
    }
}
