using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.EntityEffects;

/// <summary>
/// Зелье переноса сознания (Радужная кровь).
/// Переносит разум игрока в ближайшее существо в радиусе <see cref="Range"/> тайлов.
/// </summary>
public sealed partial class XenoConsciousnessTransferEffect : EntityEffectBase<XenoConsciousnessTransferEffect>
{
    /// <summary>Максимальный радиус поиска цели (тайлов).</summary>
    [DataField]
    public float Range = 3f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-xeno-consciousness-transfer", ("chance", Probability));
}
