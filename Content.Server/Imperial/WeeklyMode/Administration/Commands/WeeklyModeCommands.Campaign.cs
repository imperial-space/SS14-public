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
public sealed class WmSetCreateCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.set.create";
    public override string Description => "Creates a weekly mode set.";
    public override string Help => "wm.set.create <setId> <mapPath> [displayName]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Help);
            return;
        }

        var displayName = args.Length >= 3 ? string.Join(' ', args.Skip(2)) : null;
        if (_weekly.TryCreateSet(args[0], args[1], displayName, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("Set id"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.ContentFilePath(args[1], _resource), "Map path"),
            3 => CompletionResult.FromHint("Display name"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmSetMapCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.set.map";
    public override string Description => "Changes a weekly mode set map path before start.";
    public override string Help => "wm.set.map <setId> <mapPath>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TrySetMap(args[0], args[1], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("Set id"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.ContentFilePath(args[1], _resource), "Map path"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmSetListCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.set.list";
    public override string Description => "Lists weekly mode sets.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError("wm.set.list");
            return;
        }

        shell.WriteLine(_weekly.ListSets());
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmStartCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.start";
    public override string Description => "Starts or resumes a weekly mode round.";
    public override string Help => "wm.start <setId> [snapshotId]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteError(Help);
            return;
        }

        var startedBy = shell.Player?.Name ?? "server console";
        if (_weekly.TryStart(args[0], args.Length == 2 ? args[1] : null, startedBy, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmSaveCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.save";
    public override string Description => "Saves the current weekly station map as a checkpoint.";
    public override string Help => "wm.save <setId> [note]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Help);
            return;
        }

        var note = args.Length > 1
            ? string.Join(' ', args.Skip(1))
            : string.Empty;
        var createdBy = shell.Player?.Name ?? "server console";

        if (_weekly.TrySaveSnapshot(args[0], WeeklySnapshotKind.Manual, note, createdBy, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmRollbackCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.rollback";
    public override string Description => "Restarts weekly mode into a selected snapshot.";
    public override string Help => "wm.rollback <setId> <snapshotId> [--force|token]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 2 or > 3)
        {
            shell.WriteError(Help);
            return;
        }

        var startedBy = shell.Player?.Name ?? "server console";
        bool success;
        string message;

        if (args.Length == 2)
            success = _weekly.TryPrepareRollback(args[0], args[1], startedBy, out _, out message);
        else if (args[2] == "--force")
            success = _weekly.TryRollback(args[0], args[1], startedBy, out message);
        else
            success = _weekly.TryConfirmRollback(args[0], args[1], args[2], startedBy, out message);

        if (success)
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmStatusCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.status";
    public override string Description => "Shows weekly mode status.";
    public override string Help => "wm.status [setId]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.GetStatus(args.Length == 1 ? args[0] : null));
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmSnapshotsCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.snapshots";
    public override string Description => "Lists snapshots for a weekly set.";
    public override string Help => "wm.snapshots <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.ListSnapshots(args[0]));
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmSnapshotDeleteCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.snapshot.delete";
    public override string Description => "Deletes a non-current weekly snapshot.";
    public override string Help => "wm.snapshot.delete <setId> <snapshotId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryDeleteSnapshot(args[0], args[1], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmStopCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.stop";
    public override string Description => "Stops the active weekly campaign without deleting config or snapshots.";
    public override string Help => "wm.stop <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryStop(args[0], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmCancelCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.cancel";
    public override string Description => "Cancels weekly mode and returns to normal round selection.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError("wm.cancel");
            return;
        }

        if (_weekly.TryCancel(out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

internal static class WeeklyModeCommandParsing
{
    public static bool TryParseBool(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "true":
            case "1":
            case "yes":
            case "on":
                result = true;
                return true;
            case "false":
            case "0":
            case "no":
            case "off":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }
}
