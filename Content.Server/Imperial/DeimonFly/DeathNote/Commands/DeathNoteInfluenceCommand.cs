using Content.Server.Administration;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Commands;

/// <summary>
/// Отправляет стилизованное окно влияния без создания записи в тетради.
/// Команда предназначена для ивентологов, продолжающих управляемую сцену вручную.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class DeathNoteInfluenceCommand : LocalizedCommands
{
    private const int MaximumInfluenceLength = 512;

    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public override string Command => "deathnoteinfluence";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _players),
                Loc.GetString("cmd-deathnoteinfluence-hint-ckey"));
        }

        return args.Length == 2
            ? CompletionResult.FromHint(Loc.GetString("cmd-deathnoteinfluence-hint-text"))
            : CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("cmd-deathnoteinfluence-error-usage", ("help", Help)));
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var target))
        {
            shell.WriteError(Loc.GetString("cmd-deathnoteinfluence-error-not-connected"));
            return;
        }

        var text = string.Join(" ", args[1..]).Trim();
        if (text.Length == 0)
        {
            shell.WriteError(Loc.GetString("cmd-deathnoteinfluence-error-empty"));
            return;
        }

        if (text.Length > MaximumInfluenceLength)
        {
            shell.WriteError(Loc.GetString(
                "cmd-deathnoteinfluence-error-too-long",
                ("limit", MaximumInfluenceLength)));
            return;
        }

        var administrator = shell.Player?.Name ?? Loc.GetString("cmd-deathnoteinfluence-server-console");
        _entities.System<DeathNoteSystem>()
            .SendAdministrativeInfluence(target, text, administrator);
        shell.WriteLine(Loc.GetString("cmd-deathnoteinfluence-success", ("player", target.Name)));
    }
}
