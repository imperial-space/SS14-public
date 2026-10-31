using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Панель ведущего: окно с управлением войной. Открывается командой <c>cadia panel</c>,
/// а каждое действие заново проверяет права, потому что сетевое событие может прислать кто угодно.
/// </summary>
public sealed class CadiaEventPanelSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IAdminLogManager _log = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;
    [Dependency] private readonly CadiaWarpStormSystem _storm = default!;
    [Dependency] private readonly CadiaStatsSystem _stats = default!;
    [Dependency] private readonly CadiaWeatherSystem _weather = default!;
    [Dependency] private readonly CadiaDropPodSystem _drop = default!;

    private const int WarpGrant = 10;
    private static readonly TimeSpan PushRate = TimeSpan.FromSeconds(2);

    private readonly HashSet<ICommonSession> _watchers = new();
    private TimeSpan _nextPush;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CadiaPanelSubscribeEvent>(OnSubscribe);
        SubscribeNetworkEvent<CadiaPanelActionEvent>(OnAction);
    }

    public void Open(ICommonSession session)
    {
        RaiseNetworkEvent(new CadiaPanelOpenEvent(), session.Channel);
    }

    private bool Allowed(ICommonSession session) => _admin.HasAdminFlag(session, AdminFlags.Fun);

    private void OnSubscribe(CadiaPanelSubscribeEvent ev, EntitySessionEventArgs args)
    {
        if (!ev.Subscribe)
        {
            _watchers.Remove(args.SenderSession);
            return;
        }
        if (!Allowed(args.SenderSession))
            return;
        _watchers.Add(args.SenderSession);
        RaiseNetworkEvent(BuildState(), args.SenderSession.Channel);
    }

    private void OnAction(CadiaPanelActionEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!Allowed(session))
            return;

        _log.Add(LogType.Action, LogImpact.High, $"{session.Name} used Cadia panel: {ev.Action} {ev.Text} {ev.Target}");

        if (ev.Action == CadiaPanelAction.Start)
        {
            if (!_rule.TryGetActiveRule(out _, out _))
                _ticker.StartGameRule(CadiaCommand.RuleId);
            return;
        }

        if (!_rule.TryGetActiveRule(out var rule, out var war))
            return;

        switch (ev.Action)
        {
            case CadiaPanelAction.NextPhase:
                if (war.Phase < CadiaWarPhase.Finale)
                    _rule.SetPhase(rule, war.Phase + 1);
                break;
            case CadiaPanelAction.WaveImperium:
                _rule.Wave(rule, war, CadiaFaction.Imperium);
                break;
            case CadiaPanelAction.WaveChaos:
                _rule.Wave(rule, war, CadiaFaction.Chaos);
                break;
            case CadiaPanelAction.WinImperium:
                _rule.Victory(rule, CadiaFaction.Imperium, Loc.GetString("cadia-win-admin"));
                break;
            case CadiaPanelAction.WinChaos:
                _rule.Victory(rule, CadiaFaction.Chaos, Loc.GetString("cadia-win-admin"));
                break;
            case CadiaPanelAction.BeginFall:
                _rule.FallOfCadia(rule, Loc.GetString("cadia-win-admin"));
                break;
            case CadiaPanelAction.AnnounceAll:
                _rule.Broadcast(ev.Text, null);
                break;
            case CadiaPanelAction.AnnounceImperium:
                _rule.Broadcast(ev.Text, CadiaFaction.Imperium);
                break;
            case CadiaPanelAction.AnnounceChaos:
                _rule.Broadcast(ev.Text, CadiaFaction.Chaos);
                break;
            case CadiaPanelAction.StormBloodRain:
                _storm.Start(CadiaWarpStorm.BloodRain);
                break;
            case CadiaPanelAction.StormWhispers:
                _storm.Start(CadiaWarpStorm.Whispers);
                break;
            case CadiaPanelAction.StormDaemonSurge:
                _storm.Start(CadiaWarpStorm.DaemonSurge);
                break;
            case CadiaPanelAction.AddWarp:
                _rule.AddWarp(WarpGrant);
                break;
            case CadiaPanelAction.CycleWeather:
                _weather.Cycle();
                break;
            case CadiaPanelAction.ToggleCloseImperium:
                _rule.ToggleForcedClose(rule, war, CadiaFaction.Imperium);
                break;
            case CadiaPanelAction.ToggleCloseChaos:
                _rule.ToggleForcedClose(rule, war, CadiaFaction.Chaos);
                break;
            case CadiaPanelAction.RemovePlayer when ev.Target is { } removed:
                _rule.RemoveFromField(new NetUserId(removed), false);
                break;
            case CadiaPanelAction.BanPlayer when ev.Target is { } banned:
                _rule.RemoveFromField(new NetUserId(banned), true);
                break;
            case CadiaPanelAction.ShowIntro:
                _rule.ShowIntroToAll();
                break;
            // Завеса пилонов вручную: снять или поднять в любой фазе; минуту к подготовке — только пока она идёт.
            case CadiaPanelAction.VeilToggle:
                war.VeilOverride = !CadiaPylonVeilSystem.VeilUp(war);
                _rule.DirtyWar();
                break;
            case CadiaPanelAction.VeilAddMinute when war.Phase == CadiaWarPhase.Preparation:
                war.PhaseEnd += TimeSpan.FromMinutes(1);
                _rule.DirtyWar();
                break;
            case CadiaPanelAction.VeilCutMinute when war.Phase == CadiaWarPhase.Preparation:
                war.PhaseEnd = TimeSpan.FromTicks(Math.Max((war.PhaseEnd - TimeSpan.FromMinutes(1)).Ticks, (_timing.CurTime + TimeSpan.FromSeconds(10)).Ticks));
                _rule.DirtyWar();
                break;
            case CadiaPanelAction.DropRecharge:
                _drop.RechargeBerths();
                break;
            case CadiaPanelAction.DropPod:
                _drop.SendEmptyToRandomZone();
                break;
        }
        RaiseNetworkEvent(BuildState(), session.Channel);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_watchers.Count == 0)
            return;
        var now = _timing.CurTime;
        if (now < _nextPush)
            return;
        _nextPush = now + PushRate;

        // Права могли снять, а игрок мог выйти: состояние с именами и ckey такому не отправляем.
        _watchers.RemoveWhere(s => s.Status == SessionStatus.Disconnected || !Allowed(s));
        if (_watchers.Count == 0)
            return;
        var state = BuildState();
        foreach (var watcher in _watchers)
            RaiseNetworkEvent(state, watcher.Channel);
    }

    private CadiaPanelStateEvent BuildState()
    {
        var state = new CadiaPanelStateEvent();
        if (!_rule.TryGetActiveRule(out var rule, out var war))
            return state;

        state.Active = true;
        state.Phase = war.Phase;
        state.SecondsLeft = (int) Math.Max(0, (war.PhaseEnd - _timing.CurTime).TotalSeconds);
        state.PylonsAlive = war.PylonsAlive;
        state.PylonsTotal = war.PylonsTotal;
        state.Warp = war.WarpPower;
        state.ImperiumSeats = war.ImperiumSeats;
        state.ChaosSeats = war.ChaosSeats;
        state.ImperiumForced = CadiaDefenseRuleSystem.IsForcedClosed(rule, CadiaFaction.Imperium);
        state.ChaosForced = CadiaDefenseRuleSystem.IsForcedClosed(rule, CadiaFaction.Chaos);
        state.Saved = war.EvacSaved;
        state.VeilUp = CadiaPylonVeilSystem.VeilUp(war);
        state.RefugeesSaved = war.RefugeesSaved;
        state.RefugeesTotal = war.RefugeesTotal;

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } mob ||
                !TryComp<CadiaFactionMemberComponent>(mob, out var member) || member.Role is not { } role)
                continue;
            var alive = !_mobState.IsDead(mob);
            if (alive)
            {
                if (member.Faction == CadiaFaction.Imperium)
                    state.ImperiumAlive++;
                else
                    state.ChaosAlive++;
            }
            var team = _stats.TeamRecord(session.UserId);
            state.Players.Add(new CadiaPanelPlayer
            {
                TeamKills = team.Kills,
                TeamDamage = team.Damage,
                User = session.UserId,
                Name = session.Name,
                Character = Name(mob),
                Faction = member.Faction,
                Role = _proto.TryIndex(role, out var proto) ? Loc.GetString(proto.Name) : role.ToString(),
                Alive = alive,
                Banned = _rule.IsBanned(session.UserId),
            });
        }
        state.Players.Sort((a, b) => a.Faction != b.Faction ? a.Faction.CompareTo(b.Faction) : string.CompareOrdinal(a.Name, b.Name));
        return state;
    }
}
