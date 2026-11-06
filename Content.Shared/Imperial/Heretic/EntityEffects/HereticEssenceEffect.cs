using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Heretic.Core;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.EntityEffects;

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class HereticEssenceEffect : EntityEffectBase<HereticEssenceEffect>
{
    [DataField]
    public DamageSpecifier HealDamage = new();

    [DataField]
    public DamageSpecifier PoisonDamage = new();

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-heretic-essence");
}
