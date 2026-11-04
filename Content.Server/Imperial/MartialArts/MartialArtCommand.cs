using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>
/// Выдать или забрать боевое искусство. Психотическая драка и искусство джунглей в TG даются
/// травмой мозга и солями для ванн — здесь их источник только эта команда.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class MartialArtCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public string Command => "martialart";
    public string Description => Loc.GetString("imperial-martial-arts-martial-art-command-1");
    public string Help => Loc.GetString("imperial-martial-arts-martial-art-command-2", ("arg0", Command));

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(new[] { "teach", "forget" }, "<teach|forget>"),
            2 => CompletionResult.FromHintOptions(
                _proto.EnumeratePrototypes<MartialArtPrototype>().Select(p => p.ID).Order(),
                "<искусство>"),
            3 => CompletionResult.FromHint("[entityUid]"),
            _ => CompletionResult.Empty,
        };
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 2 or > 3)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!_proto.HasIndex<MartialArtPrototype>(args[1]))
        {
            shell.WriteLine(Loc.GetString("imperial-martial-arts-martial-art-command-3", ("arg0", args[1])));
            return;
        }

        EntityUid target;
        if (args.Length == 3)
        {
            if (!_entManager.TryParseNetEntity(args[2], out var netEnt) || !_entManager.EntityExists(netEnt))
            {
                shell.WriteLine(Loc.GetString("imperial-martial-arts-martial-art-command-4", ("arg0", args[2])));
                return;
            }

            target = netEnt.Value;
        }
        else if (shell.Player?.AttachedEntity is { Valid: true } playerEntity)
        {
            target = playerEntity;
        }
        else
        {
            shell.WriteLine(Loc.GetString("imperial-martial-arts-martial-art-command-5"));
            return;
        }

        var system = _entManager.System<MartialArtsSystem>();
        var art = new ProtoId<MartialArtPrototype>(args[1]);
        switch (args[0])
        {
            case "teach":
                shell.WriteLine(system.Teach(target, art)
                    ? Loc.GetString("imperial-martial-arts-martial-art-command-6", ("arg0", target), ("arg1", art))
                    : Loc.GetString("imperial-martial-arts-martial-art-command-7", ("arg0", target), ("arg1", art)));
                break;
            case "forget":
                system.Unlearn(target, art);
                shell.WriteLine(Loc.GetString("imperial-martial-arts-martial-art-command-8", ("arg0", target), ("arg1", art)));
                break;
            default:
                shell.WriteLine(Help);
                break;
        }
    }
}
