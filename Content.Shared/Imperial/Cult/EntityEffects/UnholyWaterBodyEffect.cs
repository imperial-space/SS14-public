using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cult.EntityEffects;

/// <summary>
/// Система обработки эффекта нечистой воды.
/// Исцеляет культистов, наносит урон и заикание остальным.
/// </summary>
public sealed partial class UnholyWaterBodyEffectSystem : EntityEffectSystem<DamageableComponent, UnholyWaterBodyEffect>
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    private static readonly string[] BruteTypes = { "Blunt", "Slash", "Piercing" };
    private static readonly string[] BurnTypes  = { "Heat", "Shock", "Cold", "Caustic" };

    protected override void Effect(Entity<DamageableComponent> entity, ref EntityEffectEvent<UnholyWaterBodyEffect> args)
    {
        if (HasComp<CultistComponent>(entity.Owner))
            ApplyCultistEffects(entity, args.Effect);
        else
            ApplyNonCultistEffects(entity, args.Effect);
    }

    private void ApplyCultistEffects(Entity<DamageableComponent> entity, UnholyWaterBodyEffect effect)
    {
        var heal = new DamageSpecifier();
        foreach (var (dt, dmg) in entity.Comp.Damage.DamageDict)
        {
            if (dmg <= FixedPoint2.Zero) continue;
            if (Array.IndexOf(BruteTypes, dt) >= 0
                || Array.IndexOf(BurnTypes, dt) >= 0
                || dt == "Asphyxiation"
                || dt == "Poison")
            {
                heal.DamageDict[dt] = -(FixedPoint2)effect.HealAmount;
            }
        }
        if (!heal.Empty)
            _damage.TryChangeDamage(entity.Owner, heal, ignoreResistances: true, interruptsDoAfters: false);

        _status.TryRemoveTime(entity.Owner, "Stun", TimeSpan.FromSeconds(effect.RemoveSleepTime));
        _status.TryRemoveTime(entity.Owner, "KnockedDown", TimeSpan.FromSeconds(effect.RemoveSleepTime));
    }

    private void ApplyNonCultistEffects(Entity<DamageableComponent> entity, UnholyWaterBodyEffect effect)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"]  = (FixedPoint2)effect.BruteDamage;
        damage.DamageDict["Poison"] = (FixedPoint2)effect.PoisonDamage;
        _damage.TryChangeDamage(entity.Owner, damage, ignoreResistances: false, interruptsDoAfters: false);

        _status.TryAddStatusEffect(entity.Owner, "Stutter", TimeSpan.FromSeconds(effect.StutterDuration), true, "StutteringAccentComponent");
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class UnholyWaterBodyEffect : EntityEffectBase<UnholyWaterBodyEffect>
{
    [DataField] public float HealAmount = 20f;
    [DataField] public float RemoveSleepTime = 4f;
    [DataField] public float BruteDamage = 3f;
    [DataField] public float PoisonDamage = 1f;
    [DataField] public float StutterDuration = 5f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        "Нечистая вода воздействует на организм по-разному, в зависимости от природы существа.";
}
