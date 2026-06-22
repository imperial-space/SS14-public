using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Administration;
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Imperial.CentCom;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.CentCom;

[AdminCommand(AdminFlags.Admin)]
public sealed class CentComAnnounceCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly Regex StationCodeRegex = new(@"[A-Z]{2}-\d{3}", RegexOptions.Compiled);

    public override string Command => "centcomannounce";
    public override string Description => Loc.GetString("centcomannouncecommand-desc");
    public override string Help => Loc.GetString("centcomannouncecommand-help");

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _prototype
                .EnumeratePrototypes<CentComAnnouncePrototype>()
                .Select(p => new CompletionOption(p.ID, p.Desc));

            return CompletionResult.FromHintOptions(options.OrderBy(x => x.Value, StringComparer.Ordinal).ToArray(), Loc.GetString("centcomannouncecommand-id-preset"));
        }

        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("centcomannouncecommand-error-args"));
            return;
        }

        var protoId = args[0];
        if (!_prototype.TryIndex<CentComAnnouncePrototype>(protoId, out var proto))
        {
            shell.WriteError(Loc.GetString("centcomannouncecommand-error-preset-not-found", ("protoid", protoId)));
            return;
        }

        var stationSystem = _entity.System<StationSystem>();
        var stations = stationSystem.GetStationNames();
        if (stations.Count == 0)
        {
            shell.WriteError(Loc.GetString("centcomannouncecommand-error-no-station"));
            return;
        }

        var stationName = stations[0].Name;
        var codeMatch = StationCodeRegex.Match(stationName);
        var stationCode = codeMatch.Success ? codeMatch.Value : stationName;

        var message = proto.Message.Replace("XX-###", stationCode);

        _entity.System<ChatSystem>().DispatchGlobalAnnouncement(message, colorOverride: Color.Gold);
        shell.WriteLine(Loc.GetString("shell-command-success"));
    }
}
