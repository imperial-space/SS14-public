using System.Linq;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticGlitteringArraySystem : EntitySystem
{
    [Dependency] private readonly HereticMoonBrainDamageSystem _brainDamage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticGlitteringArrayComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticGlitteringArrayComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HereticGlitteringArrayComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);

        SubscribeLocalEvent<HereticGlitteringArrayEquippedComponent, KnockDownAttemptEvent>(OnKnockdownAttempt);
        SubscribeLocalEvent<HereticGlitteringArrayEquippedComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<HereticGlitteringArrayEquippedComponent, ShotAttemptedEvent>(OnShootAttempt);
    }

    private void OnEquipped(Entity<HereticGlitteringArrayComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
        Dirty(ent);
        EnsureComp<HereticGlitteringArrayEquippedComponent>(args.Wearer);
    }

    private void OnUnequipped(Entity<HereticGlitteringArrayComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        ent.Comp.Wearer = null;
        Dirty(ent);
        RemComp<HereticGlitteringArrayEquippedComponent>(args.Wearer);
    }

    private void OnDamageModify(Entity<HereticGlitteringArrayComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (ent.Comp.Wearer is not { } wearer)
            return;

        var totalDamage = 0f;
        foreach (var v in args.Args.Damage.DamageDict.Values)
        {
            if (v > FixedPoint2.Zero)
                totalDamage += v.Float();
        }

        if (totalDamage <= 0f)
            return;

        var zeroed = new DamageSpecifier();
        foreach (var key in args.Args.Damage.DamageDict.Keys.ToList())
            zeroed.DamageDict[key] = FixedPoint2.Zero;
        args.Args.Damage = zeroed;

        _brainDamage.AddBrainDamage(wearer, totalDamage);
    }

    private void OnKnockdownAttempt(Entity<HereticGlitteringArrayEquippedComponent> ent, ref KnockDownAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnAttackAttempt(Entity<HereticGlitteringArrayEquippedComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Disarm)
            return;
        if (args.Target == null)
            return;
        if (!HasComp<MobStateComponent>(args.Target.Value))
            return;
        if (args.Weapon != null && HasComp<HereticMoonBladeComponent>(args.Weapon.Value))
            return;
        args.Cancel();
    }

    private void OnShootAttempt(Entity<HereticGlitteringArrayEquippedComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
    }
}
