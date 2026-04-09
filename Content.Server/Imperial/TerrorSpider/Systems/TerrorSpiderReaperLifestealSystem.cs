using Content.Server.Imperial.TerrorSpider.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Imperial.TerrorSpider.Systems;

public sealed class TerrorSpiderReaperLifestealSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderReaperLifestealComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<TerrorSpiderReaperLifestealComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var attacker = args.User;

        if (attacker != ent.Owner)
            attacker = ent.Owner;

        if (!TryComp<DamageableComponent>(attacker, out var damageable))
            return;

        var validHits = 0;
        foreach (var target in args.HitEntities)
        {
            if (target == attacker)
                continue;

            if (!TryComp<MobStateComponent>(target, out var mobState))
                continue;

            if (mobState.CurrentState != MobState.Alive)
                continue;

            validHits++;
        }

        if (validHits <= 0)
            return;

        var totalHeal = ent.Comp.HealAmount * validHits;

        var heal = new DamageSpecifier();
        foreach (var group in damageable.DamagePerGroup.Keys)
        {
            heal.DamageDict[group] = -totalHeal;
        }

        foreach (var damageType in damageable.Damage.DamageDict.Keys)
        {
            heal.DamageDict[damageType] = -totalHeal;
        }

        if (heal.Empty)
            return;

        _damageable.TryChangeDamage(attacker, heal, ignoreResistances: true, interruptsDoAfters: false);
    }
}
