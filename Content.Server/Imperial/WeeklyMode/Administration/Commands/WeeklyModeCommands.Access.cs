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
public sealed class WmAccessMinPlaytimeCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.access.min-playtime";
    public override string Description => "Sets minimum overall server playtime for weekly participation.";
    public override string Help => "wm.access.min-playtime <setId> <hours>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 ||
            !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours))
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TrySetMinPlaytime(args[0], hours, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmAccessDiscordCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.access.discord";
    public override string Description => "Sets the Discord channel/link shown in weekly access notices.";
    public override string Help => "wm.access.discord <setId> <channel-or-link>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Help);
            return;
        }

        var channel = string.Join(' ', args.Skip(1));
        if (_weekly.TrySetDiscordChannel(args[0], channel, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmAccessStatusCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.access.status";
    public override string Description => "Shows weekly access restrictions.";
    public override string Help => "wm.access.status <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(_weekly.GetAccessStatus(args[0]));
    }
}

[AdminCommand(AdminFlags.Round | AdminFlags.Server)]
public sealed class WmAccessClearCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WeeklyModeSystem _weekly = default!;

    public override string Command => "wm.access.clear";
    public override string Description => "Clears weekly access restrictions.";
    public override string Help => "wm.access.clear <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (_weekly.TryClearAccess(args[0], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}
