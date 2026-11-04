using Content.Server.Polymorph.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Xenobiology;

public sealed class SlimeTransformationExposureEffectSystem : EntityEffectSystem<MetaDataComponent, SlimeTransformationExposure>
{
    [Dependency] private readonly SlimeToxinSystem _toxin = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<SlimeTransformationExposure> args)
    {
        _toxin.Infect(entity);
    }
}
