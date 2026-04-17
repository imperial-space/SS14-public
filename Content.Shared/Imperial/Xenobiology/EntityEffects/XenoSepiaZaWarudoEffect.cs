using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.EntityEffects;

/// <summary>
/// Эффект зелья сепии (ZA WARUDO).
/// Через <see cref="Delay"/> секунд парализует все существа в радиусе <see cref="Range"/> на <see cref="StunDuration"/> секунд.
/// </summary>
public sealed partial class XenoSepiaZaWarudoEffect : EntityEffectBase<XenoSepiaZaWarudoEffect>
{
    /// <summary>Радиус эффекта (тайлов).</summary>
    [DataField]
    public float Range = 2.5f;

    /// <summary>Задержка перед применением (секунды).</summary>
    [DataField]
    public float Delay = 5f;

    /// <summary>Длительность паралича (секунды).</summary>
    [DataField]
    public float StunDuration = 15f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-xeno-sepia-za-warudo", ("chance", Probability));
}
