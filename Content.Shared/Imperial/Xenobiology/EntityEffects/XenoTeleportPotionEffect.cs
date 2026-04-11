using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.EntityEffects;

/// <summary>
/// Зелье телепортации из Bluespace воды.
/// Первое использование: сохраняет текущую позицию.
/// Второе использование: телепортирует обратно в сохранённую точку.
/// </summary>
public sealed partial class XenoTeleportPotionEffect : EntityEffectBase<XenoTeleportPotionEffect>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-xeno-teleport-potion", ("chance", Probability));
}
