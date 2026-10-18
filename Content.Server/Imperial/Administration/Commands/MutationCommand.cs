using System.Linq;
using Content.Server.Administration;
using Content.Server.Imperial.Genetics;
using Content.Shared.Administration;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Administration.Commands;

/// <summary>
/// Управление мутациями игрока: выдать, снять, снять все, показать геном.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class MutationCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override string Command => "mutation";
    public override string Description => Loc.GetString("cmd-mutation-desc");
    public override string Help => Loc.GetString("cmd-mutation-help");

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(new[] { "add", "remove", "clear", "genome" }, "<add|remove|clear|genome>"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _players), "<ckey>"),
            3 when args[0] is "add" or "remove" =>
                CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<GeneticMutationPrototype>(), "<mutation>"),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!_players.TryGetSessionByUsername(args[1], out var player) || player.AttachedEntity is not { } target)
        {
            shell.WriteError(Loc.GetString("cmd-mutation-no-player", ("player", args[1])));
            return;
        }

        var genetics = _entityManager.System<GeneticsSystem>();
        if (!genetics.TryGetGenome(target, out var genome))
        {
            shell.WriteError(Loc.GetString("cmd-mutation-no-genome"));
            return;
        }

        switch (args[0])
        {
            case "add" when args.Length == 3 && _proto.HasIndex<GeneticMutationPrototype>(args[2]):
                shell.WriteLine(genetics.TryActivate(target, args[2], MutationSource.Admin)
                    ? Loc.GetString("cmd-mutation-added", ("mutation", args[2]))
                    : Loc.GetString("cmd-mutation-failed", ("mutation", args[2])));
                break;
            case "remove" when args.Length == 3:
                shell.WriteLine(genetics.Deactivate(target, args[2])
                    ? Loc.GetString("cmd-mutation-removed", ("mutation", args[2]))
                    : Loc.GetString("cmd-mutation-failed", ("mutation", args[2])));
                break;
            case "clear":
                genetics.DeactivateAll(target, includeVault: true);
                shell.WriteLine(Loc.GetString("cmd-mutation-cleared"));
                break;
            case "genome":
                shell.WriteLine(Loc.GetString("cmd-mutation-stability", ("stability", genome.Comp.Stability)));
                foreach (var gene in genome.Comp.Blocks)
                {
                    var state = genetics.IsComplete(gene) ? "+" : " ";
                    shell.WriteLine($"[{state}] {gene.Mutation}: {gene.Sequence}");
                }

                shell.WriteLine(Loc.GetString("cmd-mutation-active",
                    ("list", string.Join(", ", genome.Comp.Active.Keys.Select(k => k.Id)))));
                break;
            default:
                shell.WriteError(Loc.GetString("cmd-mutation-help"));
                break;
        }
    }
}
