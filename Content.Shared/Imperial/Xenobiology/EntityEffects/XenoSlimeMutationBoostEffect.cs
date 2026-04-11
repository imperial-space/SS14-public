using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Xenobiology.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.EntityEffects;

/// <summary>
/// Повышает шанс мутации ксено-слайма при следующем размножении.
/// Работает только когда реагент метаболизируется самим слаймом.
/// </summary>
public sealed partial class XenoSlimeMutationBoost : EntityEffectBase<XenoSlimeMutationBoost>
{
    /// <summary>
    /// На сколько уровней эффективного тира снизить (это повышает шанс мутации).
    /// Значение добавляется к MutationBoost слайма (не превышает 3).
    /// </summary>
    [DataField]
    public byte TierReduction = 1;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-xeno-slime-mutation-boost", ("chance", Probability));
}
