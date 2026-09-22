using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.EntityEffects;

/// <summary>
/// Эффект «Эльдрической эссенции»: исцеляет еретиков, наносит урон остальным.
/// </summary>
public sealed partial class HereticEssenceEffectSystem : EntityEffectSystem<DamageableComponent, HereticEssenceEffect>
{
    [Dependency] private readonly DamageableSystem _damage = default!;

    protected override void Effect(Entity<DamageableComponent> entity, ref EntityEffectEvent<HereticEssenceEffect> args)
    {
        if (HasComp<HereticComponent>(entity.Owner))
        {
            var heal = new DamageSpecifier(args.Effect.HealDamage);
            heal *= args.Scale;
            _damage.TryChangeDamage(entity.AsNullable(), heal, ignoreResistances: true, interruptsDoAfters: false);
        }
        else
        {
            var damage = new DamageSpecifier(args.Effect.PoisonDamage);
            damage *= args.Scale;
            _damage.TryChangeDamage(entity.AsNullable(), damage, ignoreResistances: false, interruptsDoAfters: false);
        }
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class HereticEssenceEffect : EntityEffectBase<HereticEssenceEffect>
{
    [DataField]
    public DamageSpecifier HealDamage = new();

    [DataField]
    public DamageSpecifier PoisonDamage = new();

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        "Эльдрическая эссенция по-разному воздействует на тех, кто причастен к Мансусу, и на обычных смертных.";
}
