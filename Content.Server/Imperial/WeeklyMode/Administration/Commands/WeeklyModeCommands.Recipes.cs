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
public sealed class WmRecipeCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.recipe";
    public override string Description => "Adds a campaign-only weekly lathe recipe.";
    public override string Help => "wm.recipe <setId> add <recipeId> <resultPrototype> <resultAmount> <productionTimeSeconds> <latheTargets> <materialId:amount> [materialId:amount ...]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 8 ||
            !string.Equals(args[1], "add", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var resultAmount) ||
            !double.TryParse(args[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var productionTimeSeconds))
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryAddRecipe(args[0], args[2], args[3], resultAmount, productionTimeSeconds, args[6], args.Skip(7).ToArray(), out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            2 => CompletionResult.FromHintOptions(new[] { "add" }, "Action"),
            4 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<EntityPrototype>(), "Result entity prototype"),
            7 => CompletionResult.FromHintOptions(new[] { "protolathe", "security", "medical", "engineering", "service", "science", "cargo", "civilian", "all" }, "Lathe targets"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmRecipeUpdateCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.recipe.update";
    public override string Description => "Updates a campaign-only weekly lathe recipe.";
    public override string Help => "wm.recipe.update <setId> <recipeId> <result|time|targets|materials> <args...>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 4)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryUpdateRecipe(args[0], args[1], args[2], args.Skip(3).ToArray(), out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            3 => CompletionResult.FromHintOptions(new[] { "result", "time", "targets", "materials" }, "Field"),
            4 when args.Length >= 3 && string.Equals(args[2], "result", StringComparison.OrdinalIgnoreCase) =>
                CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<EntityPrototype>(), "Result entity prototype"),
            4 when args.Length >= 3 && string.Equals(args[2], "targets", StringComparison.OrdinalIgnoreCase) =>
                CompletionResult.FromHintOptions(new[] { "protolathe", "security", "medical", "engineering", "service", "science", "cargo", "civilian", "all" }, "Lathe targets"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmRecipeTargetAddCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.recipe.target-add";
    public override string Description => "Adds a target to a campaign-only weekly recipe.";
    public override string Help => "wm.recipe.target-add <setId> <recipeId> <latheTarget>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryAddRecipeTarget(args[0], args[1], args[2], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmRecipeTargetRemoveCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.recipe.target-remove";
    public override string Description => "Removes a target from a campaign-only weekly recipe.";
    public override string Help => "wm.recipe.target-remove <setId> <recipeId> <latheTarget>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryRemoveRecipeTarget(args[0], args[1], args[2], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmRecipeLinkCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.recipe.link";
    public override string Description => "Links a campaign-only weekly recipe to a weekly technology.";
    public override string Help => "wm.recipe.link <setId> <recipeId> <technologyId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryLinkRecipe(args[0], args[1], args[2], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmRecipeUnlinkCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.recipe.unlink";
    public override string Description => "Unlinks a campaign-only weekly recipe from a weekly technology.";
    public override string Help => "wm.recipe.unlink <setId> <recipeId> <technologyId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryUnlinkRecipe(args[0], args[1], args[2], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRecipeListCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.recipe.list";
    public override string Description => "Lists campaign-only weekly recipes.";
    public override string Help => "wm.recipe.list <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.ListRecipes(args[0]));
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRecipeShowCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.recipe.show";
    public override string Description => "Shows a campaign-only weekly recipe.";
    public override string Help => "wm.recipe.show <setId> <recipeId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.ShowRecipe(args[0], args[1]));
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmRecipeRemoveCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.recipe.remove";
    public override string Description => "Removes a campaign-only weekly recipe.";
    public override string Help => "wm.recipe.remove <setId> <recipeId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryRemoveRecipe(args[0], args[1], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmRecipeValidateCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.recipe.validate";
    public override string Description => "Validates campaign-only weekly recipes.";
    public override string Help => "wm.recipe.validate <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.ValidateRecipes(args[0]));
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmRecipeClearCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.recipe.clear";
    public override string Description => "Clears all campaign-only weekly recipes.";
    public override string Help => "wm.recipe.clear <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryClearRecipes(args[0], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}
