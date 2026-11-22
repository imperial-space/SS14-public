using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Physics.Events;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.SyndieBattle.Components;
using Content.Server.Imperial.SyndieBattle.Systems;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Imperial.SyndieBattle;

/// <summary>
/// Отправляет игрока в лобби при касании того у кого есть компонент.
/// </summary>
public sealed class RespawnTouchSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawningSystem = default!;
    [Dependency] private readonly SyndieBattleRuleSystem _ruleSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RespawnTouchComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<RespawnTouchComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<RespawnTouchComponent, ActivateInWorldEvent>(OnActivateInWorld);
    }

    private void OnStartCollide(EntityUid uid, RespawnTouchComponent component, ref StartCollideEvent args)
    {
        ReturnToBattleAttempt(args.OtherEntity, component);
    }

    private void OnInteractHand(EntityUid uid, RespawnTouchComponent component, InteractHandEvent args)
    {
        if (ReturnToBattleAttempt(args.User, component))
            args.Handled = true;
    }

    private void OnActivateInWorld(EntityUid uid, RespawnTouchComponent component, ActivateInWorldEvent args)
    {
        if (ReturnToBattleAttempt(args.User, component))
            args.Handled = true;
    }

    private bool ReturnToBattleAttempt(EntityUid combatant, RespawnTouchComponent component)
    {
        if (!TryComp<MobStateComponent>(combatant, out var mobState) ||
            !_mobStateSystem.IsAlive(combatant, mobState) ||
            !HasComp<SyndieBattleArenaCombatantComponent>(combatant))
            return false;

        if (!TryComp<ActorComponent>(combatant, out var actor) ||
            !TryGetActiveRule(out var rule) ||
            !rule.PlayerScores.TryGetValue(actor.PlayerSession.UserId, out var score) ||
            score.CurrentEntity != combatant ||
            score.Profile is not { } profile ||
            score.Station is not { } station ||
            Deleted(station) ||
            !_mindSystem.TryGetMind(combatant, out var mindId, out var mind) ||
            _stationSpawningSystem.SpawnPlayerCharacterOnStation(station, null, profile) is not { } mob)
        {
            return false;
        }

        EnsureComp<MindContainerComponent>(mob);
        _mindSystem.TransferTo(mindId, mob, ghostCheckOverride: true, mind: mind);
        score.ArenaCompletions++;
        _ruleSystem.EnsureComebackBalance(rule, score);
        _ruleSystem.PrepareRespawnedCombatant(mob, rule, profile, station);

        if (component.DeleteBody)
            QueueDel(combatant);

        return true;
    }

    private bool TryGetActiveRule(out SyndieBattleRuleComponent component)
    {
        var query = EntityQueryEnumerator<SyndieBattleRuleComponent, SyndieBattleRespawnComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var rule, out _, out var gameRule))
        {
            if (_gameTicker.IsGameRuleActive(uid, gameRule) && rule.Active)
            {
                component = rule;
                return true;
            }
        }

        component = default!;
        return false;
    }
}
