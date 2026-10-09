using Content.Server.Administration;
using Content.Server.Imperial.Chaplain;
using Content.Shared.Administration;
using Content.Shared.Imperial.Chaplain.Components;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Administration.Commands;

/// <summary>
/// Выдаёт святую роль и снаряжение капеллана (роль в SS13 выдаётся должностью, у нас — командой,
/// чтобы не менять прототип должности).
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class MakeChaplainCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public override string Command => "makechaplain";
    public override string Description => Loc.GetString("cmd-makechaplain-desc");
    public override string Help => Loc.GetString("cmd-makechaplain-help");

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _players), "<ckey>"),
            2 => CompletionResult.FromHintOptions(
            [
                new CompletionOption("auto", Loc.GetString("cmd-makechaplain-role-auto")),
                new CompletionOption("highpriest", Loc.GetString("cmd-makechaplain-role-highpriest")),
                new CompletionOption("priest", Loc.GetString("cmd-makechaplain-role-priest")),
                new CompletionOption("deacon", Loc.GetString("cmd-makechaplain-role-deacon")),
            ], "[auto|highpriest|priest|deacon]"),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            shell.WriteLine(Help);
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var player) || player.AttachedEntity is not { } target)
        {
            shell.WriteError(Loc.GetString("cmd-makechaplain-no-player", ("player", args[0])));
            return;
        }

        HolyRole? role = null;
        if (args.Length >= 2)
        {
            switch (args[1].ToLowerInvariant())
            {
                case "auto":
                    break;
                case "highpriest":
                    role = HolyRole.HighPriest;
                    break;
                case "priest":
                    role = HolyRole.Priest;
                    break;
                case "deacon":
                    role = HolyRole.Deacon;
                    break;
                default:
                    shell.WriteError(Loc.GetString("cmd-makechaplain-bad-role", ("role", args[1])));
                    return;
            }
        }

        var religion = _entityManager.System<ImperialReligionSystem>();
        var given = religion.MakeHoly(target, role);

        shell.WriteLine(Loc.GetString("cmd-makechaplain-success",
            ("player", player.Name),
            ("role", given.ToString())));
    }
}
