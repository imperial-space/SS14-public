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

public abstract class SharedTerrorSpiderKnightRageSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string PiercingDamageType = "Piercing";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderKnightRageComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerrorSpiderKnightRageComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TerrorSpiderKnightRageComponent, TerrorSpiderKnightRageActionEvent>(OnRageAction);
        SubscribeLocalEvent<TerrorSpiderKnightRageComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TerrorSpiderKnightRageComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsEnraged || now < comp.RageEndTime)
                continue;

            EndRage(uid, comp);
        }
    }

    private void OnMapInit(EntityUid uid, TerrorSpiderKnightRageComponent comp, MapInitEvent args)
    {
        _actions.AddAction(uid, ref comp.ActionEntity, comp.Action);
    }

    private void OnShutdown(EntityUid uid, TerrorSpiderKnightRageComponent comp, ComponentShutdown args)
    {
        if (comp.IsEnraged)
            EndRage(uid, comp);

        _actions.RemoveAction(uid, comp.ActionEntity);
    }

    private void OnRageAction(Entity<TerrorSpiderKnightRageComponent> ent, ref TerrorSpiderKnightRageActionEvent args)
    {
        if (args.Handled || ent.Comp.IsEnraged)
            return;

        if (TryComp<TerrorSpiderKnightGuardComponent>(ent.Owner, out var guard) && guard.IsGuarding)
            return;

        ent.Comp.IsEnraged = true;
        ent.Comp.RageEndTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.RageDuration);

        if (TryComp<MeleeWeaponComponent>(ent.Owner, out var melee))
        {
            ent.Comp.CachedMeleeDamage = new DamageSpecifier(melee.Damage);
            SetDamageValue(melee.Damage, PiercingDamageType, ent.Comp.EnragedMeleeDamage);
            Dirty(ent.Owner, melee);
        }

        if (TryComp<PassiveDamageComponent>(ent.Owner, out var passive))
        {
            ent.Comp.CachedPassiveDamage = new DamageSpecifier(passive.Damage);
            passive.Damage = new DamageSpecifier();
            Dirty(ent.Owner, passive);
        }

        if (TryComp<TerrorSpiderArmorComponent>(ent.Owner, out var armor))
        {
            ent.Comp.CachedBruteModifier = armor.BruteModifier;
            ent.Comp.CachedBurnModifier = armor.BurnModifier;
            ent.Comp.HasCachedArmor = true;
            armor.BruteModifier = ent.Comp.BruteIncomingMultiplier;
            armor.BurnModifier = ent.Comp.BurnIncomingMultiplier;
            Dirty(ent.Owner, armor);
        }

        _movement.RefreshMovementSpeedModifiers(ent.Owner);
        Dirty(ent.Owner, ent.Comp);
        args.Handled = true;
    }

    private void EndRage(EntityUid uid, TerrorSpiderKnightRageComponent comp)
    {
        comp.IsEnraged = false;
        comp.RageEndTime = TimeSpan.Zero;

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

        if (comp.HasCachedArmor && TryComp<TerrorSpiderArmorComponent>(uid, out var armor))
        {
            armor.BruteModifier = comp.CachedBruteModifier;
            armor.BurnModifier = comp.CachedBurnModifier;
            Dirty(uid, armor);
        }

        comp.HasCachedArmor = false;

        _movement.RefreshMovementSpeedModifiers(uid);
        Dirty(uid, comp);
    }

    private void OnRefreshMove(Entity<TerrorSpiderKnightRageComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.IsEnraged)
            return;

        args.ModifySpeed(ent.Comp.EnragedSpeedMultiplier, ent.Comp.EnragedSpeedMultiplier);
    }

    private static void SetDamageValue(DamageSpecifier specifier, string key, float value)
    {
        specifier.DamageDict[key] = FixedPoint2.New(value);
    }
}
