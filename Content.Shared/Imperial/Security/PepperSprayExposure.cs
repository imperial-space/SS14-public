using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Security;

/// <summary>
/// condensedcapsaicin/expose_mob(): перцовый спрей на коже или в воздухе.
/// Без защиты рта и глаз — слёзы, слепота и падение.
/// </summary>
public sealed partial class PepperSprayExposure : EntityEffectBase<PepperSprayExposure>
{
    [DataField]
    public TimeSpan BlindTime = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(3);

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-pepper-spray", ("chance", Probability));
}
