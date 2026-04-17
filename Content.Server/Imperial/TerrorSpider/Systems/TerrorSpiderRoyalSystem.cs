using Content.Server.Actions;
using Content.Server.Imperial.TerrorSpider.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.TerrorSpider.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Imperial.TerrorSpider.Components;
using Content.Shared.Spider;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.TerrorSpider.Systems;

public sealed class TerrorSpiderRoyalSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MovementModStatusSystem _movementModStatus = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderRoyalComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerrorSpiderRoyalComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TerrorSpiderRoyalComponent, TerrorSpiderRoyalStompActionEvent>(OnStompAction);
    }

    private void OnMapInit(EntityUid uid, TerrorSpiderRoyalComponent comp, MapInitEvent args)
    {
        if (!comp.StompEnabled)
            return;

        _actions.AddAction(uid, ref comp.StompActionEntity, comp.StompAction);
    }

    private void OnShutdown(EntityUid uid, TerrorSpiderRoyalComponent comp, ComponentShutdown args)
    {
        if (!comp.StompEnabled)
            return;

        _actions.RemoveAction(uid, comp.StompActionEntity);
    }

    private void OnStompAction(Entity<TerrorSpiderRoyalComponent> ent, ref TerrorSpiderRoyalStompActionEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.StompEnabled)
            return;

        var origin = Transform(ent.Owner).MapPosition;
        var radiusSquared = ent.Comp.StompRadius * ent.Comp.StompRadius;
        var query = EntityQueryEnumerator<TransformComponent>();
        var targets = new List<EntityUid>();

        while (query.MoveNext(out var uid, out var xform))
        {
            if (uid == ent.Owner)
                continue;

            if (xform.MapPosition.MapId != origin.MapId)
                continue;

            if ((xform.MapPosition.Position - origin.Position).LengthSquared() > radiusSquared)
                continue;

            if (HasComp<SpiderComponent>(uid) || HasComp<TerrorSpiderWebBuffReceiverComponent>(uid))
                continue;

            if (!TryComp<MobStateComponent>(uid, out var mobState) || mobState.CurrentState != MobState.Alive)
                continue;

            targets.Add(uid);
        }

        foreach (var uid in targets)
        {
            if (Deleted(uid))
                continue;

            _movementModStatus.TryUpdateMovementSpeedModDuration(
                uid,
                ent.Comp.StompSlowStatusEffect,
                TimeSpan.FromSeconds(ent.Comp.StompSlowDuration),
                ent.Comp.StompSlowMultiplier);

            if (!TryComp<DamageableComponent>(uid, out _))
                continue;

            var damage = new DamageSpecifier();
            damage.DamageDict[ent.Comp.StompDamageType] = FixedPoint2.New(ent.Comp.StompDamage);
            _damageable.TryChangeDamage(uid, damage, ignoreResistances: false, interruptsDoAfters: false);
        }

        if (ent.Comp.StompSound != null)
            _audio.PlayPvs(ent.Comp.StompSound, ent.Owner);

        args.Handled = true;
    }
}
