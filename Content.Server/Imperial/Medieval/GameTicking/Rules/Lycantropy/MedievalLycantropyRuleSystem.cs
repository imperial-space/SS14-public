using Content.Shared.GameTicking.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Server.Imperial.Medieval.Plague;
using Content.Server.Imperial.Medieval.Lycantropy;
using Content.Shared.GameTicking;
using Content.Server.Imperial.DayTime;
using Robust.Shared.Timing;
using Content.Server.Polymorph.Components;
using Content.Shared.Imperial.Medieval.Lycantropy;
using System.Linq;
using Content.Shared.Imperial.Medieval.CCVar;
using Robust.Shared.Configuration;
using Content.Shared.Mobs.Systems;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

public sealed class MedievalLycantropyRuleSystem : GameRuleSystem<MedievalLycantropyRuleComponent>
{
    [Dependency] private readonly LycantropySystem _lycantropy = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly DayTimeSystem _dayTime = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public static bool IsBloodMoon = false;

    private int _curCycle = 0;
    private TimeSpan? _endTime = null;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DayCycleFinishedEvent>(OnDayCycleFinished);
        SubscribeLocalEvent<DayCycleStageChangedEvent>(OnDayStageChanged);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnd);
    }


    private void OnRoundStart(RoundStartedEvent args)
    {
        _curCycle = 0;
    }

    private void OnRoundEnd(RoundEndedEvent args)
    {
        IsBloodMoon = false;
    }

    private void OnDayCycleFinished(ref DayCycleFinishedEvent args)
    {
        _curCycle++;

        if (EntityManager.AllEntities<LycantropyComponent>().Where(x => _mobState.IsAlive(x)).Count() < _config.GetCVar(MedievalCCVars.BloodMoonWerewolves))
            return;

        if (_curCycle >= _config.GetCVar(MedievalCCVars.BloodMoonPeriod))
        {
            _dayTime.ChangePreset("0", "bloody", true);
            _endTime = _timing.CurTime + TimeSpan.FromMinutes(10);
            IsBloodMoon = true;
            _lycantropy.OnNightStarted();
        }
    }

    private void OnDayStageChanged(ref DayCycleStageChangedEvent args)
    {
        if (IsBloodMoon)
            return;

        if (args.NextStage == 5)
            _lycantropy.OnNightStarted();
        else if (args.NextStage == 10)
            _lycantropy.OnNightEnded();
    }

    protected override void AppendRoundEndText(EntityUid uid, MedievalLycantropyRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var werewolfCount = EntityManager.AllEntities<LycantropyComponent>().Count();
        if (werewolfCount == 0)
            return;

        args.AddLine(Loc.GetString("round-end-lycantropy-werewolf-count-summary",
            ("count", werewolfCount)));

        foreach (var item in EntityManager.AllEntities<LycantropyComponent>())
        {
            args.AddLine(Loc.GetString("round-end-lycantropy-werewolf-summary",
                ("name", Name(item.Owner))));
        }
    }


    protected override void ActiveTick(EntityUid uid, MedievalLycantropyRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (_endTime != null && _endTime <= _timing.CurTime)
        {
            _endTime = null;
            GameTicker.EndRound();
            GameTicker.RestartRound();
            return;
        }
    }
}
