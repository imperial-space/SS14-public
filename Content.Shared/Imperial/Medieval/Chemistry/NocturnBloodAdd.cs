using Content.Shared.Medical;
using Content.Shared.EntityEffects;
using Content.Shared.Nocturn.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.ChemistryRandomization
{
    [UsedImplicitly]
    public sealed partial class NocturnBloodAdd : EntityEffect
    {
        [DataField]
        public float BloodAmount = 1.5f;

        protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
            => Loc.GetString("imperial-medieval-chemistry-bloodadd", ("amount", BloodAmount.ToString()));

        public override void Effect(EntityEffectBaseArgs args)
        {
            if (args.EntityManager.TryGetComponent<NocturnComponent>(args.TargetEntity, out var blood))
            {
                blood.BloodLevel += BloodAmount;
            }
        }
    }
}
