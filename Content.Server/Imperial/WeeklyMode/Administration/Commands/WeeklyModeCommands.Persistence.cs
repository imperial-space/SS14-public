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
public sealed class WmAutosaveSetCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.autosave.set";
    public override string Description => "Sets weekly autosave interval and warning minutes for a set.";
    public override string Help => "wm.autosave.set <setId> <intervalMinutes> <warningMinutes>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3 ||
            !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var intervalMinutes) ||
            !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var warningMinutes))
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TrySetAutosave(args[0], intervalMinutes, warningMinutes, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmPersistenceMobsCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.persistence.mobs";
    public override string Description => "Toggles autonomous mob persistence for a weekly set.";
    public override string Help => "wm.persistence.mobs <setId> <true|false>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 || !WeeklyModeCommandParsing.TryParseBool(args[1], out var enabled))
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TrySetPersistAutonomousMobs(args[0], enabled, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmPersistenceExcludePrototypeCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.persistence.exclude-prototype";
    public override string Description => "Excludes an entity prototype and descendants from autonomous mob persistence.";
    public override string Help => "wm.persistence.exclude-prototype <setId> <prototypeId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryExcludeMobPrototype(args[0], args[1], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 2
            ? CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<EntityPrototype>(), "Entity prototype")
            : CompletionResult.FromHint("Set id");
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmPersistenceExcludeListCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.persistence.exclude-list";
    public override string Description => "Lists excluded autonomous mob prototypes for a weekly set.";
    public override string Help => "wm.persistence.exclude-list <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.ListExcludedMobPrototypes(args[0]));
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmPersistenceExcludeClearCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.persistence.exclude-clear";
    public override string Description => "Removes one autonomous mob prototype exclusion from a weekly set.";
    public override string Help => "wm.persistence.exclude-clear <setId> <prototypeId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryClearExcludedMobPrototype(args[0], args[1], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}
