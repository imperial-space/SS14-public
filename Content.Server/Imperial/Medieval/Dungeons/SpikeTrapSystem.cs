using Content.Server.SpikeTrap.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using Content.Shared.Humanoid;
using Content.Shared.Damage;
using Content.Server.MagicBarrier.Components;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Shared.Imperial.Medieval.GameTicking.Rules;

namespace Content.Server.MagicPotionsMaker
{
    public sealed partial class SpikeTrapSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SpikeTrapComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<SpikeTrapComponent, ComponentShutdown>(OnShutdown);
        }

        private void OnStartup(EntityUid uid, SpikeTrapComponent component, ComponentStartup args)
        {
            if (!component.Enabled)
                return;
            TrapDeactivate(uid, component); // Деактивируем при старте
        }

        private void OnShutdown(EntityUid uid, SpikeTrapComponent component, ComponentShutdown args)
        {
            // Убедитесь, что удаляете все сущности, связанные с ловушкой, при ее удалении
            if (component.ActiveTrapEntity != null)
                QueueDel(component.ActiveTrapEntity);
            if (component.DeactiveTrapEntity != null)
                QueueDel(component.DeactiveTrapEntity);
        }

        public void TrapDeactivate(EntityUid uid, SpikeTrapComponent component)
        {
            if (!component.Enabled)
                return;

            if (component.ActiveTrapEntity != null)
                QueueDel(component.ActiveTrapEntity);

            var xform = Transform(component.Owner);
            var coords = xform.Coordinates;
            component.DeactiveTrapEntity = Spawn(component.DeactiveTrap, coords);
            Audio.PlayPvs(new SoundPathSpecifier(component.DeactiveSoundEffect), uid, AudioParams.Default.WithVariation(0.15f));

            component.StartTime = _timing.CurTime;
            component.EndTime = component.StartTime + component.ReloadTime;
            component.Ready = true;
            component.Cooldown = 0f; // Сбрасываем кулдаун
        }


        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var curTime = _timing.CurTime; // Кэшируем время

            foreach (var comp in EntityManager.EntityQuery<SpikeTrapComponent>())
            {
                if (!comp.Enabled)
                    continue;

                comp.Cooldown -= frameTime;

                if (comp.Cooldown <= 0f && !comp.Ready)
                {
                    TrapDeactivate(comp.Owner, comp);
                }

                if (curTime > comp.EndTime && comp.Ready) // Проверяем только когда пора активироваться
                {
                    var xform = Transform(comp.Owner);
                    var coords = xform.Coordinates;

                    if (CheckPlayersNearby(coords, comp))
                    {
                        if (comp.DeactiveTrapEntity != null)
                            QueueDel(comp.DeactiveTrapEntity);
                        comp.ActiveTrapEntity = Spawn(comp.ActiveTrap, coords);
                        Audio.PlayPvs(new SoundPathSpecifier(comp.ActiveSoundEffect), comp.Owner, AudioParams.Default.WithVariation(0.15f));
                        comp.Ready = false;
                        comp.Cooldown = 0.6f;
                    }
                    comp.StartTime = curTime;
                    comp.EndTime = comp.StartTime + comp.ReloadTime;
                }
            }
        }


        public bool CheckPlayersNearby(EntityCoordinates coords, SpikeTrapComponent comp)
        {
            foreach (var entity in _lookup.GetEntitiesInRange(coords, 0.5f, flags: LookupFlags.Dynamic))
            {
                if (TryComp<MedievalSpikeTargetComponent>(entity, out var target) && target.Enabled && _mobState.IsAlive(entity))
                {
                    _damageableSystem.TryChangeDamage(entity, comp.SpikeDamage, false, true);
                    foreach (var barrier in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
                    {
                        barrier.SpikeTrapActiveted++;
                    }
                    return true; // Возвращаем true, только если нашли цель и нанесли урон
                }
            }
            return false; // Возвращаем false, если рядом никого нет
        }

    }
}
