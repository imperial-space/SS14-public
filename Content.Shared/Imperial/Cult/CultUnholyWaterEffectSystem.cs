using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cult;

public sealed class CultUnholyWaterEffectSystem : EntityEffectSystem<DamageableComponent, CultUnholyWater>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    private static readonly ProtoId<DamageTypePrototype> PoisonDamageType = "Poison";
    private static readonly ProtoId<DamageTypePrototype> HeatDamageType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationDamageType = "Asphyxiation";
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> CellularDamageType = "Cellular";

    private static readonly ProtoId<DamageGroupPrototype> ToxinDamageGroup = "Toxin";
    private static readonly ProtoId<DamageGroupPrototype> AirlossDamageGroup = "Airloss";
    private static readonly ProtoId<DamageGroupPrototype> BruteDamageGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnDamageGroup = "Burn";

    protected override void Effect(Entity<DamageableComponent> entity, ref EntityEffectEvent<CultUnholyWater> args)
    {
        var scale = args.Scale;
        if (HasComp<CultistComponent>(entity))
        {
            // drowsiness -2 с, AdjustAllImmobility -0.8 с, stamina -2, tox/oxy/brute/burn -0.4, кровь +0.6.
            _stun.TryAddStunDuration(entity, TimeSpan.FromSeconds(-0.8 * scale));
            _stamina.TakeStaminaDamage(entity, -2f * scale, visual: false);
            Heal(entity, ToxinDamageGroup, 0.4f * scale);
            Heal(entity, AirlossDamageGroup, 0.4f * scale);
            Heal(entity, BruteDamageGroup, 0.4f * scale);
            Heal(entity, BurnDamageGroup, 0.4f * scale);
            _bloodstream.TryModifyBloodLevel(entity.Owner, 0.6f * scale);
            return;
        }

        // Около 90 урона за брошенные 50 ед.
        var damage = new DamageSpecifier();
        damage.DamageDict[PoisonDamageType] = 0.2f * scale;
        damage.DamageDict[HeatDamageType] = 0.2f * scale;
        damage.DamageDict[AsphyxiationDamageType] = 0.2f * scale;
        damage.DamageDict[BluntDamageType] = 0.2f * scale;
        damage.DamageDict[CellularDamageType] = 0.6f * scale;
        _damageable.TryChangeDamage(entity.Owner, damage, true);
    }

    private void Heal(Entity<DamageableComponent> entity, ProtoId<DamageGroupPrototype> group, float amount)
    {
        _damageable.HealDistributed(entity.AsNullable(), -FixedPoint2.New(amount), group);
    }
}
