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
public sealed class WmCargoCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.cargo";
    public override string Description => "Adds or removes a weekly cargo product entry.";
    public override string Help => "wm.cargo <setId> <add|remove> <productId> [category] [cost] [boxed] [amount] [itemPrototype]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3)
        {
            shell.WriteError(Help);
            return;
        }

        var setId = args[0];
        var action = args[1];
        var productId = args[2];

        if (string.Equals(action, "remove", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 3)
            {
                shell.WriteError(Help);
                return;
            }

            if (_weekly.TryRemoveCargoProduct(setId, productId, out var removeMessage))
                shell.WriteLine(removeMessage);
            else
                shell.WriteError(removeMessage);
            return;
        }

        if (!string.Equals(action, "add", StringComparison.OrdinalIgnoreCase) ||
            args.Length != 8 ||
            !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cost) ||
            !WeeklyModeCommandParsing.TryParseBool(args[5], out var boxed) ||
            !int.TryParse(args[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryAddCargoProduct(setId, productId, args[3], cost, boxed, amount, args[7], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            2 => CompletionResult.FromHintOptions(new[] { "add", "remove" }, "Action"),
            6 => CompletionResult.FromHintOptions(new[] { "true", "false" }, "Boxed"),
            8 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<EntityPrototype>(), "Item prototype"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmCargoUpdateCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.cargo.update";
    public override string Description => "Updates an existing weekly cargo product entry.";
    public override string Help => "wm.cargo.update <setId> <productId> <category> <cost> <boxed> <amount> <itemPrototype>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 7 ||
            !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cost) ||
            !WeeklyModeCommandParsing.TryParseBool(args[4], out var boxed) ||
            !int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryUpdateCargoProduct(args[0], args[1], args[2], cost, boxed, amount, args[6], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            5 => CompletionResult.FromHintOptions(new[] { "true", "false" }, "Boxed"),
            7 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<EntityPrototype>(), "Item prototype"),
            _ => CompletionResult.Empty
        };
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmCargoListCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.cargo.list";
    public override string Description => "Lists weekly cargo products.";
    public override string Help => "wm.cargo.list <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.ListCargoProducts(args[0]));
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmCargoClearCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.cargo.clear";
    public override string Description => "Clears all weekly cargo products.";
    public override string Help => "wm.cargo.clear <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryClearCargoProducts(args[0], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmCargoClearCategoryCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.cargo.clear-category";
    public override string Description => "Clears weekly cargo products in one category.";
    public override string Help => "wm.cargo.clear-category <setId> <category>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Help);
            return;
        }

        var category = string.Join(' ', args.Skip(1));
        if (_weekly.TryClearCargoCategory(args[0], category, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmCargoValidateCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.cargo.validate";
    public override string Description => "Validates weekly cargo products.";
    public override string Help => "wm.cargo.validate <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.ValidateCargoProducts(args[0]));
    }
}
