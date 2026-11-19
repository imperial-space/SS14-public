using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Fax.Components;
using Content.Shared.Imperial.CentCom;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.CentCom;

[AdminCommand(AdminFlags.Admin)]
public sealed class CentComSendPaperCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override string Command => "centcomsendpaper";
    public override string Description => Loc.GetString("centcomsendpapercommand-desc");
    public override string Help => Loc.GetString("centcomsendpapercommand-help");

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _prototype
                .EnumeratePrototypes<CentComPaperPrototype>()
                .Select(p => new CompletionOption(p.ID, p.Desc));

            return CompletionResult.FromHintOptions(options.OrderBy(x => x.Value, StringComparer.Ordinal).ToArray(), Loc.GetString("centcomsendpapercommand-id-preset"));
        }

        if (args.Length == 2)
        {
            var names = new HashSet<string>();
            var enumerator = _entity.EntityQueryEnumerator<FaxMachineComponent>();
            while (enumerator.MoveNext(out var faxComponent))
            {
                names.Add(faxComponent.FaxName);
            }

            var options = names.OrderBy(x => x, StringComparer.Ordinal).Select(n => new CompletionOption(n));
            return CompletionResult.FromHintOptions(options.ToArray(), Loc.GetString("centcomsendpapercommand-fax-hint"));
        }

        if (args.Length == 3)
            return CompletionResult.FromHint(Loc.GetString("centcomsendpapercommand-operator-hint"));

        if (args.Length == 4)
            return CompletionResult.FromHint(Loc.GetString("centcomsendpapercommand-decision-hint"));

        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 || args.Length > 4)
        {
            shell.WriteError(Loc.GetString("centcomsendpapercommand-error-args"));
            return;
        }

        var protoId = args[0];
        if (!_prototype.TryIndex<CentComPaperPrototype>(protoId, out var proto))
        {
            shell.WriteError(Loc.GetString("centcomsendpapercommand-error-paper-not-found", ("protoid", protoId)));
            return;
        }

        var faxNameQuery = args[1];
        var operatorName = args.Length >= 3 && args[2] != "-" ? args[2] : null;
        var decisionText = args.Length >= 4 ? args[3] : null;

        var result = _entity.System<CentComPaperSystem>().SendPaper(proto, faxNameQuery, operatorName, decisionText);
        switch (result)
        {
            case CentComPaperSystem.SendPaperResult.FaxNotFound:
                shell.WriteError(Loc.GetString("centcomsendpapercommand-error-fax-not-found", ("query", faxNameQuery)));
                return;
            case CentComPaperSystem.SendPaperResult.MultipleFaxesFound:
                shell.WriteError(Loc.GetString("centcomsendpapercommand-error-multiple-faxes", ("query", faxNameQuery)));
                return;
        }

        shell.WriteLine(Loc.GetString("shell-command-success"));
    }
}
