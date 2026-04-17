using Content.Server.Actions;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Imperial.TerrorSpider.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.TerrorSpider.Components;
using Content.Shared.Imperial.TerrorSpider.Events;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.TerrorSpider.Systems;

public sealed class TerrorSpiderMotherSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> TerrorSpiderTag = "TerrorSpider";

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementModStatusSystem _movementModStatus = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly WeldableSystem _weldable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderMotherComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerrorSpiderMotherComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TerrorSpiderMotherComponent, TerrorSpiderMotherPulseActionEvent>(OnPulseAction);
        SubscribeLocalEvent<TerrorSpiderMotherComponent, TerrorSpiderMotherRemoteViewNextActionEvent>(OnRemoteViewNextAction);
        SubscribeLocalEvent<TerrorSpiderMotherComponent, TerrorSpiderMotherRemoteViewPreviousActionEvent>(OnRemoteViewPreviousAction);
        SubscribeLocalEvent<TerrorSpiderMotherComponent, TerrorSpiderMotherRemoteViewExitActionEvent>(OnRemoteViewExitAction);
        SubscribeLocalEvent<TerrorSpiderMotherComponent, TerrorSpiderMotherLayJellyActionEvent>(OnLayJellyAction);
        SubscribeLocalEvent<TerrorSpiderMotherComponent, TerrorSpiderVentUnweldActionEvent>(OnUnweldVentAction);
        SubscribeLocalEvent<TerrorSpiderMotherComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<TerrorSpiderMotherComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<TerrorSpiderWebBuffReceiverComponent, MeleeHitEvent>(OnAnyMeleeHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        UpdateRegenBuffs(now);
        var mothers = EntityQueryEnumerator<TerrorSpiderMotherComponent>();

        while (mothers.MoveNext(out var mother, out var comp))
        {
            if (TryComp<EyeComponent>(mother, out var eye))
                UpdateRemoteViewMovementLock((mother, comp), eye.Target);

            if (comp.NextAuraTick == TimeSpan.Zero)
                comp.NextAuraTick = now + TimeSpan.FromSeconds(comp.AuraInterval);

            if (now < comp.NextAuraTick)
                continue;

            comp.NextAuraTick = now + TimeSpan.FromSeconds(comp.AuraInterval);

            foreach (var target in _lookup.GetEntitiesInRange(mother, comp.AuraHalfRange, LookupFlags.Dynamic))
            {
                if (target == mother)
                    continue;

                if (!TryComp<DamageableComponent>(target, out var damageable))
                    continue;

                if (IsTerrorSpider(target))
                {
                    ApplyHealAllDamageTypes(target, damageable, comp.AuraHealAmount);
                    continue;
                }

                if (!TryComp<MobStateComponent>(target, out var mobState) || mobState.CurrentState != MobState.Alive)
                    continue;

                var damage = new DamageSpecifier();
                damage.DamageDict["Poison"] = comp.AuraDamageAmount;
                _damageable.TryChangeDamage(target, damage, ignoreResistances: false, interruptsDoAfters: false);
            }
        }
    }

    private void OnMapInit(EntityUid uid, TerrorSpiderMotherComponent comp, MapInitEvent args)
    {
        _actions.AddAction(uid, ref comp.PulseActionEntity, comp.PulseAction);
        _actions.AddAction(uid, ref comp.RemoteViewNextActionEntity, comp.RemoteViewNextAction);
        _actions.AddAction(uid, ref comp.RemoteViewPreviousActionEntity, comp.RemoteViewPreviousAction);
        _actions.AddAction(uid, ref comp.RemoteViewExitActionEntity, comp.RemoteViewExitAction);
        _actions.AddAction(uid, ref comp.LayJellyActionEntity, comp.LayJellyAction);
        _actions.AddAction(uid, ref comp.UnweldVentActionEntity, comp.UnweldVentAction);
    }

    private void OnShutdown(EntityUid uid, TerrorSpiderMotherComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, comp.PulseActionEntity);
        _actions.RemoveAction(uid, comp.RemoteViewNextActionEntity);
        _actions.RemoveAction(uid, comp.RemoteViewPreviousActionEntity);
        _actions.RemoveAction(uid, comp.RemoteViewExitActionEntity);
        _actions.RemoveAction(uid, comp.LayJellyActionEntity);
        _actions.RemoveAction(uid, comp.UnweldVentActionEntity);
    }

    private void OnPulseAction(Entity<TerrorSpiderMotherComponent> ent, ref TerrorSpiderMotherPulseActionEvent args)
    {
        if (args.Handled)
            return;

        foreach (var target in _lookup.GetEntitiesInRange(ent.Owner, ent.Comp.PulseRange, LookupFlags.Dynamic))
        {
            if (!IsTerrorSpider(target))
                continue;

            if (!TryComp<DamageableComponent>(target, out var damageable))
                continue;

            ApplyHealAllDamageTypes(target, damageable, ent.Comp.PulseHealAmount);
        }

        args.Handled = true;
    }

    private void OnRemoteViewNextAction(Entity<TerrorSpiderMotherComponent> ent, ref TerrorSpiderMotherRemoteViewNextActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<EyeComponent>(ent.Owner, out _))
            return;

        var candidates = GetRemoteViewCandidates(ent.Owner);

        if (candidates.Count == 0)
        {
            _eye.SetTarget(ent.Owner, null);
            ent.Comp.RemoteViewIndex = -1;
            UpdateRemoteViewMovementLock(ent, null);
            args.Handled = true;
            return;
        }

        if (ent.Comp.RemoteViewIndex < -1 || ent.Comp.RemoteViewIndex >= candidates.Count)
            ent.Comp.RemoteViewIndex = -1;

        ent.Comp.RemoteViewIndex = (ent.Comp.RemoteViewIndex + 1) % candidates.Count;
        _eye.SetTarget(ent.Owner, candidates[ent.Comp.RemoteViewIndex]);
        UpdateRemoteViewMovementLock(ent, candidates[ent.Comp.RemoteViewIndex]);
        args.Handled = true;
    }

    private void OnRemoteViewPreviousAction(Entity<TerrorSpiderMotherComponent> ent, ref TerrorSpiderMotherRemoteViewPreviousActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<EyeComponent>(ent.Owner, out _))
            return;

        var candidates = GetRemoteViewCandidates(ent.Owner);

        if (candidates.Count == 0)
        {
            _eye.SetTarget(ent.Owner, null);
            ent.Comp.RemoteViewIndex = -1;
            UpdateRemoteViewMovementLock(ent, null);
            args.Handled = true;
            return;
        }

        if (ent.Comp.RemoteViewIndex < 0 || ent.Comp.RemoteViewIndex >= candidates.Count)
            ent.Comp.RemoteViewIndex = 0;

        ent.Comp.RemoteViewIndex = (ent.Comp.RemoteViewIndex - 1 + candidates.Count) % candidates.Count;
        _eye.SetTarget(ent.Owner, candidates[ent.Comp.RemoteViewIndex]);
        UpdateRemoteViewMovementLock(ent, candidates[ent.Comp.RemoteViewIndex]);
        args.Handled = true;
    }

    private void OnRemoteViewExitAction(Entity<TerrorSpiderMotherComponent> ent, ref TerrorSpiderMotherRemoteViewExitActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<EyeComponent>(ent.Owner, out _))
            return;

        ent.Comp.RemoteViewIndex = -1;
        _eye.SetTarget(ent.Owner, null);
        UpdateRemoteViewMovementLock(ent, null);
        args.Handled = true;
    }

    private void UpdateRemoteViewMovementLock(Entity<TerrorSpiderMotherComponent> ent, EntityUid? currentTarget)
    {
        var speedMultiplier = currentTarget == null ? 1f : 0f;
        _movementModStatus.TryUpdateMovementSpeedModDuration(
            ent.Owner,
            ent.Comp.RemoteViewImmobileStatusEffect,
            TimeSpan.FromSeconds(ent.Comp.RemoteViewImmobileRefresh),
            speedMultiplier);
    }

    

    private void OnLayJellyAction(Entity<TerrorSpiderMotherComponent> ent, ref TerrorSpiderMotherLayJellyActionEvent args)
    {
        if (args.Handled)
            return;

        Spawn(ent.Comp.JellyPrototype, Transform(ent.Owner).Coordinates);
        args.Handled = true;
    }

    private void OnBeforeInteractHand(Entity<TerrorSpiderMotherComponent> ent, ref BeforeInteractHandEvent args)
    {
        if (args.Target == ent.Owner)
            return;

        if (!IsTerrorSpider(args.Target))
            return;

        ApplyHealAllDamageTypes(args.Target, ent.Comp.TouchHealAmount);
        args.Handled = true;
    }

    private void OnMeleeHit(Entity<TerrorSpiderMotherComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var target in args.HitEntities)
        {
            if (target == ent.Owner)
                continue;

            if (!IsTerrorSpider(target))
                continue;

            ApplyHealAllDamageTypes(target, ent.Comp.TouchHealAmount);
        }
    }

    private void OnUnweldVentAction(Entity<TerrorSpiderMotherComponent> ent, ref TerrorSpiderVentUnweldActionEvent args)
    {
        if (args.Handled)
            return;

        if (!IsWeldedVent(args.Target))
            return;

        _weldable.SetWeldedState(args.Target, false);
        args.Handled = true;
    }

    private bool IsWeldedVent(EntityUid uid)
    {
        if (!HasComp<GasVentPumpComponent>(uid)
            && !HasComp<GasVentScrubberComponent>(uid)
            && !HasComp<GasPassiveVentComponent>(uid))
            return false;

        return _weldable.IsWelded(uid);
    }

    private void OnAnyMeleeHit(Entity<TerrorSpiderWebBuffReceiverComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        var now = _timing.CurTime;

        foreach (var target in args.HitEntities)
        {
            if (!TryComp<TerrorSpiderMotherJellyComponent>(target, out var jelly))
                continue;

            var buff = EnsureComp<TerrorSpiderMotherRegenBuffComponent>(args.User);
            buff.HealPerTick = jelly.BuffHealPerTick;
            buff.TickInterval = jelly.BuffInterval;
            buff.NextTick = TimeSpan.Zero;
            buff.ExpiresAt = now + TimeSpan.FromSeconds(jelly.BuffDuration);

            if (!Deleted(target))
                Del(target);
        }
    }

    private void UpdateRegenBuffs(TimeSpan now)
    {
        var buffs = EntityQueryEnumerator<TerrorSpiderMotherRegenBuffComponent>();

        while (buffs.MoveNext(out var uid, out var buff))
        {
            if (buff.ExpiresAt != TimeSpan.Zero && now >= buff.ExpiresAt)
            {
                RemCompDeferred<TerrorSpiderMotherRegenBuffComponent>(uid);
                continue;
            }

            if (buff.NextTick == TimeSpan.Zero)
                buff.NextTick = now + TimeSpan.FromSeconds(buff.TickInterval);

            if (now < buff.NextTick)
                continue;

            buff.NextTick = now + TimeSpan.FromSeconds(buff.TickInterval);
            ApplyHealAllDamageTypes(uid, buff.HealPerTick);
        }
    }

    private void ApplyHealAllDamageTypes(EntityUid target, float amount)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable))
            return;

        ApplyHealAllDamageTypes(target, damageable, amount);
    }

    private void ApplyHealAllDamageTypes(EntityUid target, DamageableComponent damageable, float amount)
    {
        var currentDamage = _damageable.GetPositiveDamage((target, damageable));
        var heal = new DamageSpecifier();

        foreach (var damageType in currentDamage.DamageDict.Keys)
        {
            heal.DamageDict[damageType] = -amount;
        }

        if (heal.Empty)
            return;

        _damageable.TryChangeDamage(target, heal, ignoreResistances: true, interruptsDoAfters: false);
    }

    private List<EntityUid> GetRemoteViewCandidates(EntityUid mother)
    {
        var candidates = new List<EntityUid>();
        var motherPos = Transform(mother).MapPosition;
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var mob, out var xform))
        {
            if (uid == mother)
                continue;

            if (mob.CurrentState != MobState.Alive)
                continue;

            if (!IsTerrorSpider(uid))
                continue;

            if (xform.MapPosition.MapId != motherPos.MapId)
                continue;

            candidates.Add(uid);
        }

        candidates.Sort((a, b) => a.Id.CompareTo(b.Id));
        return candidates;
    }

    private bool IsTerrorSpider(EntityUid uid)
    {
        return HasComp<TerrorSpiderWebBuffReceiverComponent>(uid) || _tagSystem.HasTag(uid, TerrorSpiderTag);
    }
}
