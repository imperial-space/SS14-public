using System.Linq;
using Content.Server.Administration;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Shuttles.Systems;
using Content.Shared.Administration;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Console;
using Robust.Shared.Random;

namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>makemalfai [entityUid]: сделать ИИ сбойным (без uid — первый ИИ в ядре).</summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class MakeMalfAiCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "makemalfai";
    public string Description => Loc.GetString("imperial-station-ai-make-malf-ai-command-1");
    public string Help => Loc.GetString("imperial-station-ai-make-malf-ai-command-2", ("arg0", Command));

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        EntityUid? ai = null;
        if (args.Length > 0)
        {
            if (!_entManager.TryParseNetEntity(args[0], out var net) || !_entManager.EntityExists(net))
            {
                shell.WriteLine(Loc.GetString("imperial-station-ai-make-malf-ai-command-3", ("arg0", args[0])));
                return;
            }

            ai = net.Value;
        }
        else
        {
            var query = _entManager.EntityQueryEnumerator<StationAiHeldComponent>();
            if (query.MoveNext(out var found, out _))
                ai = found;
        }

        if (ai == null)
        {
            shell.WriteLine(Loc.GetString("imperial-station-ai-make-malf-ai-command-4"));
            return;
        }

        shell.WriteLine(_entManager.System<ImperialMalfAiRuleSystem>().MakeMalf(ai.Value)
            ? Loc.GetString("imperial-station-ai-make-malf-ai-command-5", ("arg0", _entManager.ToPrettyString(ai.Value)))
            : Loc.GetString("imperial-station-ai-make-malf-ai-command-6"));
    }
}
