using Content.Server.Actions;
using Content.Server.Imperial.TerrorSpider.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Imperial.TerrorSpider.Components;
using Content.Shared.Imperial.TerrorSpider.Events;
using Content.Shared.Interaction;
using Content.Shared.Spider;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.TerrorSpider.Systems;

public sealed class TerrorSpiderHealerSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderHealerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerrorSpiderHealerComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<TerrorSpiderHealerComponent, TerrorSpiderHealerPulseActionEvent>(OnPulseAction);
        SubscribeLocalEvent<TerrorSpiderHealerComponent, TerrorSpiderHealerLayEggActionEvent>(OnLayEggAction);
        SubscribeLocalEvent<TerrorSpiderHealerComponent, TerrorSpiderCocoonWrappedEvent>(OnCocoonWrapped);
        SubscribeLocalEvent<TerrorSpiderHealerComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<TerrorSpiderWebBuffReceiverComponent, BeforeDamageChangedEvent>(OnBeforeSpiderDamageChanged);
        SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);

        SubscribeLocalEvent<TerrorSpiderWebBuffReceiverComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<TerrorSpiderHealerBlindWebComponent, StartCollideEvent>(OnBlindWebStartCollide);
    }

    private void OnMapInit(EntityUid uid, TerrorSpiderHealerComponent comp, MapInitEvent args)
    {
        _actions.AddAction(uid, ref comp.PulseActionEntity, comp.PulseAction);
        _actions.AddAction(uid, ref comp.EggRusarActionEntity, comp.EggRusarAction);
        _actions.AddAction(uid, ref comp.EggDronActionEntity, comp.EggDronAction);
        _actions.AddAction(uid, ref comp.EggLurkerActionEntity, comp.EggLurkerAction);
        _actions.AddAction(uid, ref comp.EggHealerActionEntity, comp.EggHealerAction);
    }

    private void OnShutdown(EntityUid uid, TerrorSpiderHealerComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, comp.PulseActionEntity);
        _actions.RemoveAction(uid, comp.EggRusarActionEntity);
        _actions.RemoveAction(uid, comp.EggDronActionEntity);
        _actions.RemoveAction(uid, comp.EggLurkerActionEntity);
        _actions.RemoveAction(uid, comp.EggHealerActionEntity);
    }

    private void OnPulseAction(Entity<TerrorSpiderHealerComponent> ent, ref TerrorSpiderHealerPulseActionEvent args)
    {
        if (args.Handled)
            return;

        var origin = Transform(ent.Owner).MapPosition;

        var rangeSquared = ent.Comp.PulseRange * ent.Comp.PulseRange;
        var query = EntityQueryEnumerator<TerrorSpiderWebBuffReceiverComponent, TransformComponent>();

        while (query.MoveNext(out var target, out _, out var xform))
        {
            var targetCoords = xform.MapPosition;

            if (targetCoords.MapId != origin.MapId)
                continue;

            if ((targetCoords.Position - origin.Position).LengthSquared() > rangeSquared)
                continue;

            ApplyHealAllDamageTypes(target, ent.Comp.PulseHealAmount);
        }

        args.Handled = true;
    }

    private void OnLayEggAction(Entity<TerrorSpiderHealerComponent> ent, ref TerrorSpiderHealerLayEggActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.SatietyLevel < ent.Comp.EggRequiredSatiety)
            return;

        Spawn(args.EggPrototype, Transform(ent.Owner).Coordinates);
        ent.Comp.SatietyLevel -= ent.Comp.EggRequiredSatiety;
        Dirty(ent);
        args.Handled = true;
    }

    private void OnCocoonWrapped(Entity<TerrorSpiderHealerComponent> ent, ref TerrorSpiderCocoonWrappedEvent args)
    {
        ent.Comp.SatietyLevel = Math.Min(ent.Comp.MaxSatietyLevel, ent.Comp.SatietyLevel + ent.Comp.CocoonSatietyGain);
        Dirty(ent);
    }

    private void OnInteractHand(Entity<TerrorSpiderWebBuffReceiverComponent> target, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == args.User)
            return;

        if (!TryComp<TerrorSpiderHealerComponent>(args.User, out var healer))
            return;

        TryApplyTouchHeal(args.Target, healer);
        args.Handled = true;
    }

    private void OnBeforeInteractHand(Entity<TerrorSpiderHealerComponent> ent, ref BeforeInteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == ent.Owner)
            return;

        if (!TryComp<TerrorSpiderWebBuffReceiverComponent>(args.Target, out _))
            return;

        TryApplyTouchHeal(args.Target, ent.Comp);
        args.Handled = true;
    }

    private void OnBeforeSpiderDamageChanged(Entity<TerrorSpiderWebBuffReceiverComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin is not { } origin)
            return;

        if (!TryComp<TerrorSpiderHealerComponent>(origin, out var healer))
            return;

        var amount = GetTouchHealAmount(healer);
        args.Damage = new DamageSpecifier();
        ApplyHealAllDamageTypes(ent.Owner, amount);
    }

    private void OnMeleeHit(MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (!TryComp<TerrorSpiderHealerComponent>(args.User, out var healer))
            return;

        var amount = GetTouchHealAmount(healer);

        foreach (var target in args.HitEntities)
        {
            if (!HasComp<TerrorSpiderWebBuffReceiverComponent>(target) || target == args.User)
                continue;

            TryApplyTouchHeal(target, healer, amount);
        }
    }

    private bool TryApplyTouchHeal(EntityUid target, TerrorSpiderHealerComponent healer, float? amount = null)
    {
        var now = _timing.CurTime;
        if (now < healer.NextTouchHealTime)
            return false;

        healer.NextTouchHealTime = now + TimeSpan.FromSeconds(healer.TouchHealCooldown);

        var healAmount = amount ?? GetTouchHealAmount(healer);
        ApplyHealAllDamageTypes(target, healAmount);
        return true;
    }

    private void ApplyHealAllDamageTypes(EntityUid target, float amount)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable))
            return;

        var heal = new DamageSpecifier();
        foreach (var group in damageable.DamagePerGroup.Keys)
        {
            heal.DamageDict[group] = -amount;
        }

        foreach (var damageType in damageable.Damage.DamageDict.Keys)
        {
            heal.DamageDict[damageType] = -amount;
        }

        if (heal.Empty)
            return;

        _damageable.TryChangeDamage(target, heal, ignoreResistances: true, interruptsDoAfters: false);
    }

    private void OnBlindWebStartCollide(Entity<TerrorSpiderHealerBlindWebComponent> ent, ref StartCollideEvent args)
    {
        if (HasComp<IgnoreSpiderWebComponent>(args.OtherEntity))
            return;

        if (!TryComp<StatusEffectsComponent>(args.OtherEntity, out var statusEffects))
            return;

        _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(
            args.OtherEntity,
            TemporaryBlindnessSystem.BlindingStatusEffect,
            TimeSpan.FromSeconds(ent.Comp.BlindDuration),
            true,
            statusEffects);
    }

    private static float GetTouchHealAmount(TerrorSpiderHealerComponent comp)
    {
        return comp.SatietyLevel switch
        {
            <= 1 => comp.TouchHealLevel1,
            2 => comp.TouchHealLevel2,
            _ => comp.TouchHealLevel3,
        };
    }
}
