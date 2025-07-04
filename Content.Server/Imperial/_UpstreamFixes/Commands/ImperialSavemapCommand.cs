using System.Linq;
using Content.Server.Administration;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.UpstreamFixes;


[AdminCommand(Shared.Administration.AdminFlags.Mapping)]
public sealed class ImperialSaveMap : LocalizedCommands
{
    [Dependency] private readonly IEntitySystemManager _system = default!;
    [Dependency] private readonly IResourceManager _resource = default!;

    public override string Command => "hotfixes_savemap";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                return CompletionResult.FromHint(Loc.GetString("cmd-hint-savemap-id"));
            case 2:
                var opts = CompletionHelper.UserFilePath(args[1], _resource.UserData);
                return CompletionResult.FromHintOptions(opts, Loc.GetString("cmd-hint-savemap-path"));
            case 3:
                return CompletionResult.FromHint(Loc.GetString("cmd-hint-savemap-force"));
        }
        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!int.TryParse(args[0], out var intMapId))
        {
            shell.WriteLine(Help);
            return;
        }

        var mapId = new MapId(intMapId);

        // no saving null space
        if (mapId == MapId.Nullspace)
            return;

        var sys = _system.GetEntitySystem<SharedMapSystem>();
        if (!sys.MapExists(mapId))
        {
            shell.WriteError(Loc.GetString("cmd-savemap-not-exist"));
            return;
        }

        if (sys.IsInitialized(mapId) &&
            (args.Length < 3 || !bool.TryParse(args[2], out var force) || !force))
        {
            shell.WriteError(Loc.GetString("cmd-savemap-init-warning"));
            return;
        }

        _resource.UserData.CreateDir(new ResPath(args[1].Split("/").SkipLast(1).Aggregate((a, b) => $"{a}/{b}")));

        shell.WriteLine(Loc.GetString("cmd-savemap-attempt", ("mapId", mapId), ("path", args[1])));
        bool saveSuccess = _system.GetEntitySystem<MapLoaderSystem>().TrySaveMap(mapId, new ResPath(args[1]));
        if (saveSuccess)
        {
            shell.WriteLine(Loc.GetString("cmd-savemap-success"));
        }
        else
        {
            shell.WriteError(Loc.GetString("cmd-savemap-error"));
        }
    }
}
