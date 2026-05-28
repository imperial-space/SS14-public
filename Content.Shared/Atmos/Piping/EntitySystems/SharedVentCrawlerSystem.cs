using Content.Shared.Atmos.Piping.Components;
using Content.Shared.Interaction.Events;

namespace Content.Shared.Atmos.Piping.EntitySystems;

public sealed class SharedVentCrawlerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActiveVentCrawlingComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<ActiveVentCrawlingComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<ActiveVentCrawlingComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnInteractionAttempt(Entity<ActiveVentCrawlingComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnUseAttempt(EntityUid uid, ActiveVentCrawlingComponent component, UseAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnAttackAttempt(EntityUid uid, ActiveVentCrawlingComponent component, AttackAttemptEvent args)
    {
        args.Cancel();
    }
}