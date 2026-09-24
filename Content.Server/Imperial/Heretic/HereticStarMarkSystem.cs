using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticStarMarkSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StarMarkStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<StarMarkStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<StarMarkStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        var target = args.Target;
        if (HasComp<HereticComponent>(target) || HasComp<CosmosMarkImmuneComponent>(target))
        {
            _statusEffects.TryRemoveStatusEffect(target, "StarMarkStatusEffect");
            return;
        }
        var comp = EnsureComp<StarMarkComponent>(target);
        var overlay = Spawn("HereticStarMarkOverlay", new EntityCoordinates(target, Vector2.Zero));
        comp.OverlayEntity = overlay;
    }

    private void OnRemoved(Entity<StarMarkStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        var target = args.Target;
        if (TryComp<StarMarkComponent>(target, out var comp) && comp.OverlayEntity.HasValue)
            QueueDel(comp.OverlayEntity.Value);
        RemCompDeferred<StarMarkComponent>(target);
    }
}
