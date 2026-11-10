using Content.Server.WeeklyMode.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands.WeeklyMode;

public abstract class WeeklyModeConfigCommandBase : LocalizedEntityCommands
{
    [Dependency] protected readonly WeeklyModeSystem Weekly = default!;

    public override string Help => $"{Command} <setId>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        try
        {
            shell.WriteLine(GetOutput(args[0]));
        }
        catch (Exception e)
        {
            shell.WriteError(Loc.GetString("weekly-mode-config-error", ("setId", args[0]), ("error", e.Message)));
        }
    }

    protected abstract string GetOutput(string setId);
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmConfigShowCommand : WeeklyModeConfigCommandBase
{
    public override string Command => "wm.config.show";
    public override string Description => "Shows weekly campaign configuration.";
    protected override string GetOutput(string setId) => Weekly.ShowConfig(setId);
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmConfigValidateCommand : WeeklyModeConfigCommandBase
{
    public override string Command => "wm.config.validate";
    public override string Description => "Validates weekly campaign configuration.";
    protected override string GetOutput(string setId) => Weekly.ValidateConfig(setId);
}

[AdminCommand(AdminFlags.Round)]
public sealed class WmConfigExportCommand : WeeklyModeConfigCommandBase
{
    public override string Command => "wm.config.export";
    public override string Description => "Exports weekly campaign configuration as JSON.";
    protected override string GetOutput(string setId) => Weekly.ExportConfig(setId);
}
