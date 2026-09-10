using System.Linq;
using System.Globalization;
using Content.Server.WeeklyMode.Systems;
using Content.Shared.Administration;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Maps;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.Roles;
using Content.Shared.WeeklyMode;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration.Commands.WeeklyMode;

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmGamerulesRandomCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.gamerules.random";
    public override string Description => "Toggles automatic random gamerules for a weekly set.";
    public override string Help => "wm.gamerules.random <setId> <true|false>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 || !WeeklyModeCommandParsing.TryParseBool(args[1], out var enabled))
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TrySetRandomGameRules(args[0], enabled, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmGamerulesStatusCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.gamerules.status";
    public override string Description => "Shows weekly gamerule settings.";
    public override string Help => "wm.gamerules.status <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.GetGameRulesStatus(args[0]));
    }
}
