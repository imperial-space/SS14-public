using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Epsilon;

/// <summary>
/// epsilon [минуты] — под эскадрона смерти у ЦК, оповещение начнётся само через указанное время (по умолчанию 5 минут);
/// epsilon start — начать оповещение сейчас; epsilon stop — оборвать трек у игроков.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class EpsilonCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public const string RuleId = "ImperialEpsilon";

    public string Command => "epsilon";
    public string Description => Loc.GetString("cmd-epsilon-desc");
    public string Help => Loc.GetString("cmd-epsilon-help");

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(new[] { "start", "stop" }, Loc.GetString("cmd-epsilon-hint"))
            : CompletionResult.Empty;
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var rules = _entities.System<ImperialEpsilonRuleSystem>();
        switch (args.Length > 0 ? args[0] : null)
        {
            case "start":
                var started = rules.StartNow();
                shell.WriteLine(started > 0
                    ? Loc.GetString("cmd-epsilon-started")
                    : Loc.GetString("cmd-epsilon-no-rule"));
                return;

            case "stop":
                rules.StopMusic();
                shell.WriteLine(Loc.GetString("cmd-epsilon-stopped"));
                return;
        }

        var minutes = 5f;
        if (args.Length > 0 && !float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out minutes))
        {
            shell.WriteError(Help);
            return;
        }

        if (!_entities.System<GameTicker>().StartGameRule(RuleId, out var rule)
            || !_entities.TryGetComponent<ImperialEpsilonRuleComponent>(rule, out var comp))
        {
            shell.WriteError(Loc.GetString("cmd-epsilon-failed"));
            return;
        }

        comp.AutoStartAt = _timing.CurTime + TimeSpan.FromMinutes(Math.Max(0f, minutes));
        shell.WriteLine(Loc.GetString("cmd-epsilon-spawned", ("minutes", minutes)));
    }
}
