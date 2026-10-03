using System.Globalization;
using Content.Server.Administration;
using Content.Server.Imperial.Fission;
using Content.Server.Spawners.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Administration.Commands;

/// <summary>
/// fission spawn — собранный реактор NGCR там, где стоит админ;
/// fission fill [моли азота] — стержни во все пустые камеры и азот в активную зону;
/// fission power &lt;0-100&gt; — желаемая мощность (обратное положение управляющих стержней);
/// fission status / freeze / unfreeze / damage &lt;0-1000&gt; / overload — состояние, заморозка, урон, перегрузка ЦК (Дельта).
/// </summary>
[AdminCommand(AdminFlags.Spawn)]
public sealed class FissionCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override string Command => "fission";
    public override string Description => Loc.GetString("cmd-fission-desc");
    public override string Help => Loc.GetString("cmd-fission-help");

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                new[] { "spawn", "fill", "power", "status", "freeze", "unfreeze", "damage", "overload" },
                "<spawn|fill|power|status|freeze|unfreeze|damage|overload>"),
            2 when args[0] == "fill" => CompletionResult.FromHint("[nitrogen moles = 500]"),
            2 when args[0] == "power" => CompletionResult.FromHint("<0-100>"),
            2 when args[0] == "damage" => CompletionResult.FromHint("<0-1000>"),
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

        var fission = _entityManager.System<FissionSystem>();
        if (args[0] == "spawn")
        {
            var anchor = shell.Player?.AttachedEntity;
            if (anchor == null)
            {
                var spawns = _entityManager.EntityQueryEnumerator<SpawnPointComponent>();
                if (spawns.MoveNext(out var spawn, out _))
                    anchor = spawn;
            }

            if (anchor is not { } at)
            {
                shell.WriteError(Loc.GetString("cmd-fission-no-place"));
                return;
            }

            var reactor = fission.SpawnAssembled(_entityManager.GetComponent<TransformComponent>(at).Coordinates);
            shell.WriteLine(reactor is { } r ? fission.Status(r) : Loc.GetString("cmd-fission-no-place"));
            return;
        }

        var number = args.Length > 1 && float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : (float?) null;
        var query = _entityManager.EntityQueryEnumerator<FissionReactorComponent>();
        var any = false;
        while (query.MoveNext(out var uid, out _))
        {
            any = true;
            switch (args[0])
            {
                case "fill":
                    fission.Fill(uid, number ?? 500);
                    break;
                case "power":
                    if (number is not { } power)
                    {
                        shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
                        return;
                    }
                    fission.SetDesiredPower(uid, power);
                    break;
                case "freeze":
                    fission.SetFrozen(uid, true);
                    break;
                case "unfreeze":
                    fission.SetFrozen(uid, false);
                    break;
                case "damage":
                    if (number is not { } damage)
                    {
                        shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
                        return;
                    }
                    fission.SetDamage(uid, damage);
                    break;
                case "overload":
                    if (!fission.Overload(uid))
                        shell.WriteError(Loc.GetString("cmd-fission-overload-already"));
                    break;
                case "status":
                    break;
                default:
                    shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
                    return;
            }

            shell.WriteLine(fission.Status(uid));
        }

        if (!any)
            shell.WriteError(Loc.GetString("cmd-fission-none"));
    }
}
