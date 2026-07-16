using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Imperial.Lavaland.GraveKatana;

public sealed class LavalandTagBonusDamageSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandTagBonusDamageComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(EntityUid uid, LavalandTagBonusDamageComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var target in args.HitEntities)
        {
            if (_tags.HasTag(target, comp.RequiredTag))
            {
                args.BonusDamage += comp.BonusDamage;
                break;
            }
        }
    }
}
