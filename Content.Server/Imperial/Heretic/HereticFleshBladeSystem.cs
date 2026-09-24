using Content.Server.Body.Systems;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticFleshBladeSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly HereticSystem     _heretic     = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticFleshBladeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(EntityUid uid, HereticFleshBladeComponent comp, MeleeHitEvent args)
    {
        if (!TryComp<HereticComponent>(args.User, out var heretic))
            return;
        if (!_heretic.HasKnowledge(heretic, "KnowledgeBleedingSteel"))
            return;

        foreach (var target in args.HitEntities)
            _bloodstream.TryModifyBleedAmount(target, 5f);
    }
}
