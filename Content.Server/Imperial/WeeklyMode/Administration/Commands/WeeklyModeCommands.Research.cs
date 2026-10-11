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
public sealed class WmTechCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.tech";
    public override string Description => "Adds or removes a weekly research technology entry.";
    public override string Help => "wm.tech <setId> <branch> <add|remove> <technologyId> [cost] [tier] [recipeId...]\nwm.tech <setId> <branch> remove <technologyId> [--force]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 4)
        {
            shell.WriteError(Help);
            return;
        }

        var setId = args[0];
        var branch = args[1];
        var action = args[2];
        var technologyId = args[3];

        if (string.Equals(action, "remove", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length is not (4 or 5) ||
                args.Length == 5 && !string.Equals(args[4], "--force", StringComparison.OrdinalIgnoreCase))
            {
                shell.WriteError(Help);
                return;
            }

            var force = args.Length == 5;
            if (_weekly.TryRemoveTechnology(setId, branch, technologyId, force, out var removeMessage))
                shell.WriteLine(removeMessage);
            else
                shell.WriteError(removeMessage);
            return;
        }

        if (!string.Equals(action, "add", StringComparison.OrdinalIgnoreCase) ||
            args.Length < 6 ||
            !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cost) ||
            !int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier))
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryAddTechnology(setId, branch, technologyId, cost, tier, args.Skip(6).ToArray(), out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            2 => CompletionResult.FromHintOptions(new[] { "industrial", "arsenal", "experimental", "service" }, "Branch"),
            3 => CompletionResult.FromHintOptions(new[] { "add", "remove" }, "Action"),
            7 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<LatheRecipePrototype>(), "Recipe prototype"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmTechUpdateCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.tech.update";
    public override string Description => "Updates an existing weekly research technology entry.";
    public override string Help => "wm.tech.update <setId> <technologyId> <branch> <cost> <tier> [recipeId...]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 5 ||
            !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cost) ||
            !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier))
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryUpdateTechnology(args[0], args[1], args[2], cost, tier, args.Skip(5).ToArray(), out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            3 => CompletionResult.FromHintOptions(new[] { "industrial", "arsenal", "experimental", "service" }, "Branch"),
            6 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<LatheRecipePrototype>(), "Recipe prototype"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmTechListCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.tech.list";
    public override string Description => "Lists weekly research technologies.";
    public override string Help => "wm.tech.list <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.ListTechnologies(args[0]));
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmTechClearCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.tech.clear";
    public override string Description => "Clears all weekly research technologies.";
    public override string Help => "wm.tech.clear <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryClearTechnologies(args[0], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmTechClearBranchCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.tech.clear-branch";
    public override string Description => "Clears one weekly research branch.";
    public override string Help => "wm.tech.clear-branch <setId> <branch>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryClearTechnologyBranch(args[0], args[1], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmTechValidateCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.tech.validate";
    public override string Description => "Validates weekly research technologies.";
    public override string Help => "wm.tech.validate <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.ValidateTechnologies(args[0]));
    }
}
