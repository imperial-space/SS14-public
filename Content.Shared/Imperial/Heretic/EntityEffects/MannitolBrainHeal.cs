using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.EntityEffects;

public sealed partial class MannitolBrainHeal : EntityEffectBase<MannitolBrainHeal>
{
    /// <summary>
    /// Количество единиц урона мозгу, снимаемых за единицу реагента за тик.
    /// </summary>
    [DataField]
    public float HealAmount = 10f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-mannitol-brain-heal");
}
