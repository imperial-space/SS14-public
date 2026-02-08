using Content.Server.Body;
using Content.Server.Humanoid;
using Content.Shared.Body;
using Content.Shared.Chemistry.ReactionEffects;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;

namespace Content.Server.Imperial.ChemistryRework;


public sealed partial class RemoveMarkEntityEffectSystem : EntityEffectSystem<VisualBodyComponent, RemoveMark>
{
    [Dependency] private readonly VisualBodySystem _visualBodySystem = default!;


    protected override void Effect(Entity<VisualBodyComponent> entity, ref EntityEffectEvent<RemoveMark> args)
    {
        // if (!_visualBodySystem.TryGatherMarkingsData(entity.Owner, null, out var _, out var markings, out var _)) return;

        // markings.Remove(args.Effect.MarkingCategory);

        // _visualBodySystem.RemoveMarking(entity, args.Effect.MarkingCategory, 0);
    }
}
