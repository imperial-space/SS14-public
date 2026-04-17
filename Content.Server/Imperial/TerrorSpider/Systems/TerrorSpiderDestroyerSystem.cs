using Content.Server.Actions;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Imperial.TerrorSpider.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.TerrorSpider.Events;
using Content.Shared.Mobs;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.TerrorSpider.Systems;

public sealed class TerrorSpiderDestroyerSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    private readonly HashSet<Entity<FlammableComponent>> _nearFlammables = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderDestroyerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerrorSpiderDestroyerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TerrorSpiderDestroyerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<TerrorSpiderDestroyerComponent, TerrorSpiderDestroyerEmpScreamActionEvent>(OnEmpScreamAction);
        SubscribeLocalEvent<TerrorSpiderDestroyerComponent, TerrorSpiderDestroyerFireBurstActionEvent>(OnFireBurstAction);
    }

    private void OnMapInit(EntityUid uid, TerrorSpiderDestroyerComponent comp, MapInitEvent args)
    {
        _actions.AddAction(uid, ref comp.EmpScreamActionEntity, comp.EmpScreamAction);
        _actions.AddAction(uid, ref comp.FireBurstActionEntity, comp.FireBurstAction);
    }

    private void OnShutdown(EntityUid uid, TerrorSpiderDestroyerComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, comp.EmpScreamActionEntity);
        _actions.RemoveAction(uid, comp.FireBurstActionEntity);
    }

    private void OnEmpScreamAction(Entity<TerrorSpiderDestroyerComponent> ent, ref TerrorSpiderDestroyerEmpScreamActionEvent args)
    {
        if (args.Handled)
            return;

        _emp.EmpPulse(
            Transform(ent.Owner).Coordinates,
            ent.Comp.DeathEmpRange,
            ent.Comp.EmpScreamEnergyConsumption,
            TimeSpan.FromSeconds(ent.Comp.EmpScreamDisableSeconds),
            ent.Owner);

        if (ent.Comp.EmpUseSound != null)
            _audio.PlayPvs(ent.Comp.EmpUseSound, ent.Owner);

        args.Handled = true;
    }

    private void OnMobStateChanged(Entity<TerrorSpiderDestroyerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (ent.Comp.DeathEmpTriggered)
            return;

        ent.Comp.DeathEmpTriggered = true;

        _emp.EmpPulse(
            Transform(ent.Owner).Coordinates,
            ent.Comp.EmpScreamRange,
            ent.Comp.EmpScreamEnergyConsumption,
            TimeSpan.FromSeconds(ent.Comp.EmpScreamDisableSeconds),
            ent.Owner);

        if (ent.Comp.EmpDeathSound != null)
            _audio.PlayPvs(ent.Comp.EmpDeathSound, ent.Owner);
    }

    private void OnFireBurstAction(Entity<TerrorSpiderDestroyerComponent> ent, ref TerrorSpiderDestroyerFireBurstActionEvent args)
    {
        if (args.Handled)
            return;

        var origin = Transform(ent.Owner).MapPosition;

        var ignited = new HashSet<EntityUid>();

        IgniteArea(origin, ent.Comp.FireBurstRadius, ent, ignited);

        args.Handled = true;
    }

    private void IgniteArea(MapCoordinates center, float radius, Entity<TerrorSpiderDestroyerComponent> source, HashSet<EntityUid> ignited)
    {
        _nearFlammables.Clear();
        _lookup.GetEntitiesInRange(
            center,
            radius,
            _nearFlammables,
            LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Static);

        foreach (var (uid, flammable) in _nearFlammables)
        {
            if (uid == source.Owner)
                continue;

            _flammable.AdjustFireStacks(uid, source.Comp.FireStacksPerHit, flammable);
            _flammable.Ignite(uid, source.Owner, flammable);

            if (!ignited.Add(uid))
                continue;

            var heatDamage = new DamageSpecifier();
            heatDamage.DamageDict["Heat"] = FixedPoint2.New(source.Comp.FireHeatDamagePerHit);
            _damageable.TryChangeDamage(uid, heatDamage, ignoreResistances: false, interruptsDoAfters: false);
        }
    }
}
