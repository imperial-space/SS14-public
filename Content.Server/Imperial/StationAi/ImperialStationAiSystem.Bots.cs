using System.Linq;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Power.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.StationAi;

/// <summary>call_bot(): бот идёт к точке, потом возвращается к своим делам.</summary>
[RegisterComponent]
public sealed partial class ImperialAiBotSummonComponent : Component
{
    [ViewVariables]
    public HTNCompoundTask? OldRoot;

    [ViewVariables]
    public EntityCoordinates Target;

    [ViewVariables]
    public TimeSpan Until;
}

/// <summary>Боты станции (robot_control.dm), болты ядра (toggle_anchor) и потеря питания ядра (disconnect_shell).</summary>
public sealed partial class ImperialStationAiSystem
{
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;

    private const string BotParent = "MobSiliconBase";
    private const string FollowCompound = "FollowCompound";
    private static readonly TimeSpan SummonTimeout = TimeSpan.FromSeconds(60);
    private const float SummonArrived = 1.5f;

    private void InitializeBots()
    {
        SubscribeLocalEvent<ImperialStationAiComponent, ImperialAiBotsActionEvent>(OnBotsAction);
        SubscribeLocalEvent<ImperialStationAiComponent, ImperialAiCoreBoltsActionEvent>(OnCoreBolts);
        SubscribeNetworkEvent<ImperialAiBotCommandEvent>(OnBotCommand);
    }

    private bool IsBot(EntityUid uid)
    {
        if (!HasComp<HTNComponent>(uid) || MetaData(uid).EntityPrototype is not { } proto)
            return false;

        return _proto.EnumerateParents<EntityPrototype>(proto.ID, true).Any(p => p.ID == BotParent);
    }

    private void OnBotsAction(Entity<ImperialStationAiComponent> ent, ref ImperialAiBotsActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        var station = CoreStation(ent);
        var list = new List<ImperialAiBotEntry>();
        var query = EntityQueryEnumerator<HTNComponent, TransformComponent>();
        while (query.MoveNext(out var bot, out _, out var xform))
        {
            if (!IsBot(bot) || _mobs.IsDead(bot))
                continue;

            if (station != null && _station.GetOwningStation(bot, xform) != station)
                continue;

            list.Add(new ImperialAiBotEntry(GetNetEntity(bot), Name(bot), _navMap.GetNearestBeaconString((bot, xform), true),
                HasComp<ImperialAiBotSummonComponent>(bot)));
        }

        RaiseNetworkEvent(new ImperialAiBotListEvent(list.OrderBy(b => b.Name).ToList()), actor.PlayerSession);
    }

    private void OnBotCommand(ImperialAiBotCommandEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ai || !TryComp<ImperialStationAiComponent>(ai, out var comp)
            || !TryGetEntity(ev.Bot, out var bot) || !IsBot(bot.Value))
            return;

        switch (ev.Command)
        {
            case ImperialAiBotCommand.Track:
                if (!CanTrack(ai, bot.Value))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-ai-track-failed"), ai, ai);
                    return;
                }

                comp.Tracked = bot;
                comp.TrackFails = 0;
                comp.TrackPlaced = null;
                break;
            case ImperialAiBotCommand.Summon:
                if (Eye(ai) is not { } eye)
                    return;

                Summon(bot.Value, Transform(eye).Coordinates);
                _popup.PopupEntity(Loc.GetString("imperial-ai-bot-summoned", ("bot", Name(bot.Value))), ai, ai);
                break;
        }
    }

    /// <summary>set_waypoint(): бот идёт к точке через FollowCompound.</summary>
    public void Summon(EntityUid bot, EntityCoordinates target)
    {
        if (!TryComp<HTNComponent>(bot, out var htn))
            return;

        var summon = EnsureComp<ImperialAiBotSummonComponent>(bot);
        summon.OldRoot ??= htn.RootTask;
        summon.Target = target;
        summon.Until = _timing.CurTime + SummonTimeout;

        htn.RootTask = new HTNCompoundTask { Task = FollowCompound };
        _npc.SetBlackboard(bot, NPCBlackboard.FollowTarget, target);
        _npc.SetBlackboard(bot, "FollowRange", 1f);
        _npc.SetBlackboard(bot, "FollowCloseRange", 0.5f);
        _htn.Replan(htn);
    }

    private void EndSummon(Entity<ImperialAiBotSummonComponent> bot)
    {
        if (TryComp<HTNComponent>(bot, out var htn) && bot.Comp.OldRoot != null)
        {
            htn.RootTask = bot.Comp.OldRoot;
            htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
            _htn.Replan(htn);
        }

        RemComp<ImperialAiBotSummonComponent>(bot);
    }

    private void UpdateBotsAndPower(TimeSpan now)
    {
        var bots = EntityQueryEnumerator<ImperialAiBotSummonComponent, TransformComponent>();
        var done = new List<Entity<ImperialAiBotSummonComponent>>();
        while (bots.MoveNext(out var uid, out var summon, out var xform))
        {
            if (now >= summon.Until || _mobs.IsDead(uid) || _transform.InRange(xform.Coordinates, summon.Target, SummonArrived))
                done.Add((uid, summon));
        }

        foreach (var bot in done)
        {
            EndSummon(bot);
        }

        // ai_lose_power() → disconnect_shell(): ядро ушло на резервную батарею.
        var shells = EntityQueryEnumerator<ImperialAiDeployedComponent>();
        var lost = new List<Entity<ImperialAiDeployedComponent>>();
        while (shells.MoveNext(out var uid, out var deployed))
        {
            if (_ai.TryGetCore(deployed.Ai, out var core)
                && TryComp<ApcPowerReceiverBatteryComponent>(core.Owner, out var battery) && battery.Enabled)
                lost.Add((uid, deployed));
        }

        foreach (var shell in lost)
        {
            var ai = shell.Comp.Ai;
            Undeploy(shell);
            Notify(ai, Loc.GetString("imperial-ai-power-lost-shell"));
        }
    }

    /// <summary>toggle_anchor(): поднять или опустить болты ядра.</summary>
    private void OnCoreBolts(Entity<ImperialStationAiComponent> ent, ref ImperialAiCoreBoltsActionEvent args)
    {
        if (args.Handled || !_ai.TryGetCore(ent, out var core))
            return;

        args.Handled = true;
        var xform = Transform(core.Owner);
        if (xform.Anchored)
        {
            _transform.Unanchor(core.Owner, xform);
            _popup.PopupEntity(Loc.GetString("imperial-ai-bolts-up"), ent, ent);
        }
        else if (_transform.AnchorEntity(core.Owner, xform))
        {
            _popup.PopupEntity(Loc.GetString("imperial-ai-bolts-down"), ent, ent);
        }
    }
}
