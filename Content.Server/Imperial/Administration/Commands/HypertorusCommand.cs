using System.Globalization;
using Content.Server.Administration;
using Content.Server.Imperial.Hypertorus;
using Content.Shared.Administration;
using Content.Shared.Atmos;
using Content.Shared.Imperial.Hypertorus;
using Content.Server.Spawners.Components;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Administration.Commands;

/// <summary>
/// hypertorus spawn — собранный и связанный реактор там, где стоит админ;
/// hypertorus fill &lt;рецепт&gt; [моли топлива] [газ модератора] [моли модератора] — заправить и включить все ядра;
/// hypertorus status — состояние всех ядер.
/// </summary>
[AdminCommand(AdminFlags.Spawn)]
public sealed class HypertorusCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override string Command => "hypertorus";
    public override string Description => Loc.GetString("cmd-hypertorus-desc");
    public override string Help => Loc.GetString("cmd-hypertorus-help");

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(new[] { "spawn", "fill", "status" }, "<spawn|fill|status>"),
            2 when args[0] == "fill" => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<HypertorusFuelPrototype>(), "<fuel>"),
            3 when args[0] == "fill" => CompletionResult.FromHint("[fuel moles = 1000]"),
            4 when args[0] == "fill" => CompletionResult.FromHintOptions(Enum.GetNames<Gas>(), "[moderator gas]"),
            5 when args[0] == "fill" => CompletionResult.FromHint("[moderator moles = 0]"),
            6 when args[0] == "fill" => CompletionResult.FromHint("[fusion temperature K]"),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var hypertorus = _entityManager.System<HypertorusSystem>();
        switch (args[0])
        {
            case "spawn":
            {
                // Без игрока (консоль сервера) — у первой точки спавна.
                var anchor = shell.Player?.AttachedEntity;
                if (anchor == null)
                {
                    var spawns = _entityManager.EntityQueryEnumerator<SpawnPointComponent>();
                    if (spawns.MoveNext(out var spawn, out _))
                        anchor = spawn;
                }

                if (anchor is not { } at)
                {
                    shell.WriteError(Loc.GetString("cmd-hypertorus-no-place"));
                    return;
                }

                var core = hypertorus.SpawnAssembled(_entityManager.GetComponent<TransformComponent>(at).Coordinates);
                shell.WriteLine(core is { } c ? hypertorus.Status(c) : Loc.GetString("cmd-hypertorus-no-place"));
                break;
            }
            case "fill":
            {
                if (args.Length < 2 || !_proto.HasIndex<HypertorusFuelPrototype>(args[1]))
                {
                    shell.WriteError(Loc.GetString("cmd-hypertorus-bad-fuel"));
                    return;
                }

                var fuelMoles = args.Length > 2 && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 1000;
                var moderatorGas = args.Length > 3 && Enum.TryParse<Gas>(args[3], out var g) ? g : Gas.Plasma;
                var moderatorMoles = args.Length > 4 && float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : 0;
                var temperature = args.Length > 5 && float.TryParse(args[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : 0;

                var query = _entityManager.EntityQueryEnumerator<HypertorusCoreComponent>();
                while (query.MoveNext(out var uid, out _))
                {
                    hypertorus.Fill(uid, args[1], fuelMoles, moderatorGas, moderatorMoles, temperature);
                    shell.WriteLine(hypertorus.Status(uid));
                }
                break;
            }
            case "status":
            {
                var query = _entityManager.EntityQueryEnumerator<HypertorusCoreComponent>();
                while (query.MoveNext(out var uid, out _))
                {
                    shell.WriteLine(hypertorus.Status(uid));
                    shell.WriteLine(hypertorus.PartsInfo(uid));
                }
                break;
            }
            default:
                shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
                break;
        }
    }
}
