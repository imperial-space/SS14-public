using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.EntityEffects;

/// <summary>
/// Зелье послушания: заставляет ближайших диких ксено-слаймов стать дружелюбными
/// к тому, кто использовал зелье, и ко всем игрокам рядом.
/// </summary>
public sealed partial class XenoObediencePotionEffect : EntityEffectBase<XenoObediencePotionEffect>
{
    /// <summary>Радиус поиска слаймов вокруг метаболизирующей сущности.</summary>
    [DataField]
    public float Range = 5f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-xeno-obedience-potion", ("chance", Probability));
}
