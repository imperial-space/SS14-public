using System.Linq;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Server.GameTicking;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.CCVar;
using Content.Shared.Imperial.Medieval.Revive;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Revive
{
    public sealed class KillsUntilReviveSystem : EntitySystem
    {
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly AlertsSystem _alertsSystem = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<KillsUntilReviveComponent, ComponentStartup>(OnStart);
            SubscribeLocalEvent<KillReviveGoalComponent, DamageChangedEvent>(GoalDamaged);
        }
        public override void Update(float frameTime)
        {
            var enumerator = EntityQueryEnumerator<KillsUntilReviveComponent>();
            while (enumerator.MoveNext(out var uid, out var component))
            {
                _alertsSystem.ShowAlert(uid, component.KillsAlert, (short)(12 - component.CurrentKills));
                if (component.CurrentKills == component.RequiredKills)
                    PlayerReachedGoal(component.Owner, component);
            }
        }
        public void OnStart(EntityUid uid, KillsUntilReviveComponent component, ComponentStartup args)
        {
            var currentRequiredKills = _cfg.GetCVar(MedievalCCVars.GhostKillsToRevive);
            component.RequiredKills = currentRequiredKills;
        }
        public void GoalDamaged(EntityUid uid, KillReviveGoalComponent component, DamageChangedEvent args)
        {
            if (!TryComp<DestructibleComponent>(uid, out var destrComp))
                return;

            bool destructionBehavaviorExecuted = false; // Начался ли процесс уничтожения сущности
            foreach (var threshold in destrComp.Thresholds)
            {
                if (threshold.Behaviors.Any(b => b is DoActsBehavior doActsBehavior &&
                            (doActsBehavior.HasAct(ThresholdActs.Breakage) || doActsBehavior.HasAct(ThresholdActs.Destruction))))
                    destructionBehavaviorExecuted = true;
            }

            if (!destructionBehavaviorExecuted)
                return;

            if (!TryComp<KillsUntilReviveComponent>(args.Origin, out var killerGoalComponent))
                return;

            killerGoalComponent.CurrentKills++;
        }
        public void PlayerReachedGoal(EntityUid uid, KillsUntilReviveComponent component)
        {
            // Игрок достиг поставленной цели по убийствам. Даем крутой эффект и отправляем его в лобби.
            var effect = Spawn(component.EffectProto, Transform(uid).Coordinates);
            _transform.SetParent(effect, uid);
            Timer.Spawn(TimeSpan.FromSeconds(3), () =>
                {
                    if (TryComp<ActorComponent>(uid, out var actor))
                    {
                        if (actor.PlayerSession != null)
                        {
                            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
                            var ticker = sysMan.GetEntitySystem<GameTicker>();
                            ticker.Respawn(actor.PlayerSession);
                            QueueDel(actor.Owner);
                        }
                    }
                });
        }
    }
}
