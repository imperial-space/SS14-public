using System.Linq;
using Content.Server.Administration;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.UpstreamFixes;


[AdminCommand(Shared.Administration.AdminFlags.Mapping)]
public sealed class SaveGridCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IResourceManager _resource = default!;

    public override string Command => "hotfixes_savegrid";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Not enough arguments.");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var uidNet))
        {
            shell.WriteError("Not a valid entity ID.");
            return;
        }

        var uid = _ent.GetEntity(uidNet);

        // no saving default grid
        if (!_ent.EntityExists(uid))
        {
            shell.WriteError("That grid does not exist.");
            return;
        }

        _resource.UserData.CreateDir(new ResPath(args[1].Split("/").SkipLast(1).Aggregate((a, b) => $"{a}/{b}")));

        bool saveSuccess = _ent.System<MapLoaderSystem>().TrySaveGrid(uid, new ResPath(args[1]));
        if (saveSuccess)
        {
            shell.WriteLine("Save successful. Look in the user data directory.");
        }
        else
        {
            shell.WriteError("Save unsuccessful!");
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                return CompletionResult.FromHint(Loc.GetString("cmd-hint-savebp-id"));
            case 2:
                var opts = CompletionHelper.UserFilePath(args[1], _resource.UserData);
                return CompletionResult.FromHintOptions(opts, Loc.GetString("cmd-hint-savemap-path"));
        }
        return CompletionResult.Empty;
    }
}
