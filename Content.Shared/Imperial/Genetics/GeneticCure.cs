using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics;

/// <summary>
/// Генетическое лечение (мутадон из SS13): снимает мутации от активаторов и мутаторов, возвращает гены
/// в исходное состояние, останавливает распад ДНК и клеточный распад после срыва.
/// </summary>
public sealed partial class GeneticCure : EntityEffectBase<GeneticCure>
{
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("entity-effect-guidebook-genetic-cure", ("chance", Probability));
}
