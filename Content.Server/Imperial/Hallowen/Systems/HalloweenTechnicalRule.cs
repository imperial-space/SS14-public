using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Content.Server.RoundEnd;
using System.Linq;
using Robust.Shared.Prototypes;
using Content.Server.Imperial.Halloween.Components;

namespace Content.Server.Imperial.Halloween;

public sealed class HalloweenTechnicalRuleSystem : GameRuleSystem<HalloweenTechnicalRuleComponent>
{
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JackSpawnedEvent>(OnJackPumpkinAppear);
    }

    protected override void Started(EntityUid uid,
        HalloweenTechnicalRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
    }

    protected override void AppendRoundEndText(EntityUid uid,
        HalloweenTechnicalRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        var halloween = Loc.GetString("halloween-technical-main");
        args.AddLine(halloween);
        var text = Loc.GetString("halloween-technical-endround");
        args.AddLine(text);
    }
    private void OnJackPumpkinAppear(JackSpawnedEvent ev)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var hllw, out _))
        {
            if (GameTicker.IsGameRuleActive("HalloweenTechnical"))
            {
                _roundEndSystem.EndRound();
                GameTicker.EndGameRule(uid);
            }

        }
    }
}
