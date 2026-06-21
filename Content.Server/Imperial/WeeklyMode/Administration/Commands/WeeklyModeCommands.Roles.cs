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

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesDisableCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.disable";
    public override string Description => "Disables weekly mode roles for a set.";
    public override string Help => "wm.roles.disable <setId> <jobId...>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryDisableRoles(args[0], args.Skip(1).ToArray(), out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length >= 2
            ? CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<JobPrototype>(), "Job prototype")
            : CompletionResult.FromHint("Set id");
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesEnableCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.enable";
    public override string Description => "Re-enables weekly mode roles for a set.";
    public override string Help => "wm.roles.enable <setId> <jobId...>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryEnableRoles(args[0], args.Skip(1).ToArray(), out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length >= 2
            ? CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<JobPrototype>(), "Job prototype")
            : CompletionResult.FromHint("Set id");
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesRenameCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.rename";
    public override string Description => "Adds a runtime role alias for a weekly set.";
    public override string Help => "wm.roles.rename <setId> <jobId> <alias>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3)
        {
            shell.WriteError(Help);
            return;
        }

        var alias = string.Join(' ', args.Skip(2));
        if (_weekly.TryRenameRole(args[0], args[1], alias, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("Set id"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<JobPrototype>(), "Job prototype"),
            3 => CompletionResult.FromHint("Alias"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesRenameBatchCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.rename-batch";
    public override string Description => "Adds multiple runtime role aliases for a weekly set.";
    public override string Help => "wm.roles.rename-batch <setId> JobId=\"Alias\";JobId=\"Alias\"";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Help);
            return;
        }

        var batch = string.Join(' ', args.Skip(1));
        if (_weekly.TryRenameRolesBatch(args[0], batch, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesAliasesCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.aliases";
    public override string Description => "Lists runtime role aliases for a weekly set.";
    public override string Help => "wm.roles.aliases <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.ListRoleAliases(args[0]));
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesRenameClearCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.rename-clear";
    public override string Description => "Clears runtime role aliases for a weekly set.";
    public override string Help => "wm.roles.rename-clear <setId> [jobId...]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryClearRoleAliases(args[0], args.Skip(1).ToArray(), out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length >= 2
            ? CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<JobPrototype>(), "Job prototype")
            : CompletionResult.FromHint("Set id");
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesClearCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.clear";
    public override string Description => "Clears weekly disabled roles and aliases for a set.";
    public override string Help => "wm.roles.clear <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryClearRoles(args[0], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesLimitCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.limit";
    public override string Description => "Sets a weekly role limit for a set.";
    public override string Help => "wm.roles.limit <setId> <jobId> <count>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3 ||
            !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TrySetRoleLimit(args[0], args[1], count, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("Set id"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<JobPrototype>(), "Job prototype"),
            3 => CompletionResult.FromHint("Count"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesLimitsCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.limits";
    public override string Description => "Lists weekly role limits for a set.";
    public override string Help => "wm.roles.limits <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.ListRoleLimits(args[0]));
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesLimitClearCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.limit-clear";
    public override string Description => "Clears one weekly role limit for a set.";
    public override string Help => "wm.roles.limit-clear <setId> <jobId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryClearRoleLimit(args[0], args[1], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("Set id"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<JobPrototype>(), "Job prototype"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesLimitClearAllCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.limit-clear-all";
    public override string Description => "Clears all weekly role limits for a set.";
    public override string Help => "wm.roles.limit-clear-all <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryClearRoleLimits(args[0], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmRolesForceCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.force";
    public override string Description => "Forces a campaign job assignment for a specific account.";
    public override string Help => "wm.roles.force <setId> <ckey-or-uuid> <jobId> [--bypass-playtime]";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 3 or > 4 ||
            args.Length == 4 && args[3] != "--bypass-playtime")
        {
            shell.WriteError(Help);
            return;
        }

        var target = await _weekly.ResolveForcedRoleTargetAsync(args[1]);
        if (!target.Success)
        {
            shell.WriteError(target.Message);
            return;
        }

        var createdBy = shell.Player?.Name ?? "server console";
        if (_weekly.TryForceRole(args[0], target.UserId, target.LastKnownCKey, args[2], args.Length == 4, createdBy, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("Set id"),
            2 => CompletionResult.FromHint("CKey or UUID"),
            3 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<JobPrototype>(), "Job prototype"),
            4 => CompletionResult.FromHintOptions(new[] { "--bypass-playtime" }, "Optional playtime bypass"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmRolesForceUpdateCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.force-update";
    public override string Description => "Updates a campaign forced job assignment.";
    public override string Help => "wm.roles.force-update <setId> <ckey-or-uuid> <newJobId> [--bypass-playtime|--no-bypass-playtime]";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 3 or > 4)
        {
            shell.WriteError(Help);
            return;
        }

        bool? bypass = null;
        if (args.Length == 4)
        {
            bypass = args[3] switch
            {
                "--bypass-playtime" => true,
                "--no-bypass-playtime" => false,
                _ => null,
            };

            if (bypass == null)
            {
                shell.WriteError(Help);
                return;
            }
        }

        var target = await _weekly.ResolveForcedRoleTargetAsync(args[1]);
        if (!target.Success)
        {
            shell.WriteError(target.Message);
            return;
        }

        var updatedBy = shell.Player?.Name ?? "server console";
        if (_weekly.TryUpdateForcedRole(args[0], target.UserId, target.LastKnownCKey, args[2], updatedBy, bypass, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("Set id"),
            2 => CompletionResult.FromHint("CKey or UUID"),
            3 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<JobPrototype>(), "Job prototype"),
            4 => CompletionResult.FromHintOptions(new[] { "--bypass-playtime", "--no-bypass-playtime" }, "Optional playtime flag"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesForceListCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.force-list";
    public override string Description => "Lists campaign forced job assignments.";
    public override string Help => "wm.roles.force-list <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.ListForcedRoles(args[0]));
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRolesForceShowCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.force-show";
    public override string Description => "Shows one campaign forced job assignment.";
    public override string Help => "wm.roles.force-show <setId> <ckey-or-uuid>";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        var target = await _weekly.ResolveForcedRoleTargetAsync(args[1]);
        if (!target.Success)
        {
            shell.WriteError(target.Message);
            return;
        }

        shell.WriteLine(_weekly.ShowForcedRole(args[0], target.UserId, args[1]));
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmRolesForceClearCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.force-clear";
    public override string Description => "Clears one campaign forced job assignment.";
    public override string Help => "wm.roles.force-clear <setId> <ckey-or-uuid>";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        var target = await _weekly.ResolveForcedRoleTargetAsync(args[1]);
        if (!target.Success)
        {
            shell.WriteError(target.Message);
            return;
        }

        var removedBy = shell.Player?.Name ?? "server console";
        if (_weekly.TryClearForcedRole(args[0], target.UserId, args[1], removedBy, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmRolesForceClearAllCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.roles.force-clear-all";
    public override string Description => "Clears all campaign forced job assignments.";
    public override string Help => "wm.roles.force-clear-all <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        var removedBy = shell.Player?.Name ?? "server console";
        if (_weekly.TryClearAllForcedRoles(args[0], removedBy, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}
