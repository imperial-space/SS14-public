using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.TerrorSpider.Components;
using Content.Shared.Imperial.TerrorSpider.Events;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.TerrorSpider.Systems;

public abstract class SharedTerrorSpiderKnightGuardSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderKnightGuardComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerrorSpiderKnightGuardComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TerrorSpiderKnightGuardComponent, TerrorSpiderKnightGuardActionEvent>(OnGuardAction);
        SubscribeLocalEvent<TerrorSpiderKnightGuardComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TerrorSpiderKnightGuardComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsGuarding || now < comp.GuardEndTime)
                continue;

            EndGuard(uid, comp);
        }
    }

    private void OnMapInit(EntityUid uid, TerrorSpiderKnightGuardComponent comp, MapInitEvent args)
    {
        _actions.AddAction(uid, ref comp.ActionEntity, comp.Action);
    }

    private void OnShutdown(EntityUid uid, TerrorSpiderKnightGuardComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, comp.ActionEntity);
    }

    private void OnGuardAction(Entity<TerrorSpiderKnightGuardComponent> ent, ref TerrorSpiderKnightGuardActionEvent args)
    {
        if (args.Handled || ent.Comp.IsGuarding)
            return;

        if (TryComp<TerrorSpiderKnightRageComponent>(ent.Owner, out var rage) && rage.IsEnraged)
            return;

        ent.Comp.IsGuarding = true;
        ent.Comp.GuardEndTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.GuardDuration);

        if (TryComp<MeleeWeaponComponent>(ent.Owner, out var melee))
        {
            ent.Comp.CachedMeleeDamage = new DamageSpecifier(melee.Damage);
            SetDamageValue(melee.Damage, "Piercing", ent.Comp.GuardMeleeDamage);
            Dirty(ent.Owner, melee);
        }

        if (TryComp<PassiveDamageComponent>(ent.Owner, out var passive))
        {
            ent.Comp.CachedPassiveDamage = new DamageSpecifier(passive.Damage);
            passive.Damage = new DamageSpecifier();
            // 6 HP/s total regen (double the base 3 HP/s)
            SetDamageValue(passive.Damage, "Blunt", -1f);
            SetDamageValue(passive.Damage, "Slash", -1f);
            SetDamageValue(passive.Damage, "Piercing", -1f);
            SetDamageValue(passive.Damage, "Heat", -0.5f);
            SetDamageValue(passive.Damage, "Shock", -0.5f);
            SetDamageValue(passive.Damage, "Cold", -0.5f);
            SetDamageValue(passive.Damage, "Caustic", -0.5f);
            SetDamageValue(passive.Damage, "Poison", -1f);
            Dirty(ent.Owner, passive);
        }

        // Apply guard armor modifiers via TerrorSpiderArmorComponent
        if (TryComp<TerrorSpiderArmorComponent>(ent.Owner, out var armor))
        {
            ent.Comp.CachedBruteModifier = armor.BruteModifier;
            ent.Comp.CachedBurnModifier = armor.BurnModifier;
            armor.BruteModifier = ent.Comp.BruteIncomingMultiplier;
            armor.BurnModifier = ent.Comp.BurnIncomingMultiplier;
            Dirty(ent.Owner, armor);
        }

        _movement.RefreshMovementSpeedModifiers(ent.Owner);
        Dirty(ent);
        args.Handled = true;
    }

    private void EndGuard(EntityUid uid, TerrorSpiderKnightGuardComponent comp)
    {
        comp.IsGuarding = false;
        comp.GuardEndTime = TimeSpan.Zero;

        if (TryComp<MeleeWeaponComponent>(uid, out var melee) && comp.CachedMeleeDamage != null)
        {
            melee.Damage = new DamageSpecifier(comp.CachedMeleeDamage);
            Dirty(uid, melee);
        }

        if (TryComp<PassiveDamageComponent>(uid, out var passive) && comp.CachedPassiveDamage != null)
        {
            passive.Damage = new DamageSpecifier(comp.CachedPassiveDamage);
            Dirty(uid, passive);
        }

        comp.CachedMeleeDamage = null;
        comp.CachedPassiveDamage = null;

        // Restore armor modifiers
        if (TryComp<TerrorSpiderArmorComponent>(uid, out var armor))
        {
            armor.BruteModifier = comp.CachedBruteModifier;
            armor.BurnModifier = comp.CachedBurnModifier;
            Dirty(uid, armor);
        }

        _movement.RefreshMovementSpeedModifiers(uid);
        Dirty(uid, comp);
    }

    private void OnRefreshMove(Entity<TerrorSpiderKnightGuardComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.IsGuarding)
            return;

        args.ModifySpeed(ent.Comp.GuardSpeedMultiplier, ent.Comp.GuardSpeedMultiplier);
    }

    private static void SetDamageValue(DamageSpecifier specifier, string key, float value)
    {
        specifier.DamageDict[key] = FixedPoint2.New(value);
    }
}
