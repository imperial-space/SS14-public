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

/// <summary>
/// datum/reagent/fuel/unholywater: культиста лечит и бодрит, остальных травит.
/// Значения — за секунду метаболизма (scale = доля от 1 с).
/// </summary>
public sealed partial class CultUnholyWater : EntityEffectBase<CultUnholyWater>;

public sealed class CultUnholyWaterEffectSystem : EntityEffectSystem<DamageableComponent, CultUnholyWater>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    protected override void Effect(Entity<DamageableComponent> entity, ref EntityEffectEvent<CultUnholyWater> args)
    {
        var scale = args.Scale;
        if (HasComp<CultistComponent>(entity))
        {
            // drowsiness -2 с, AdjustAllImmobility -0.8 с, stamina -2, tox/oxy/brute/burn -0.4, кровь +0.6.
            _stun.TryAddStunDuration(entity, TimeSpan.FromSeconds(-0.8 * scale));
            _stamina.TakeStaminaDamage(entity, -2f * scale, visual: false);
            Heal(entity, "Toxin", 0.4f * scale);
            Heal(entity, "Airloss", 0.4f * scale);
            Heal(entity, "Brute", 0.4f * scale);
            Heal(entity, "Burn", 0.4f * scale);
            _bloodstream.TryModifyBloodLevel(entity.Owner, 0.6f * scale);
            return;
        }

        // Около 90 урона за брошенные 50 ед.
        var damage = new DamageSpecifier();
        damage.DamageDict["Poison"] = 0.2f * scale;
        damage.DamageDict["Heat"] = 0.2f * scale;
        damage.DamageDict["Asphyxiation"] = 0.2f * scale;
        damage.DamageDict["Blunt"] = 0.2f * scale;
        damage.DamageDict["Cellular"] = 0.6f * scale;
        _damageable.TryChangeDamage(entity.Owner, damage, true);
    }

    private void Heal(Entity<DamageableComponent> entity, ProtoId<DamageGroupPrototype> group, float amount)
    {
        _damageable.HealDistributed(entity.AsNullable(), -FixedPoint2.New(amount), group);
    }
}
