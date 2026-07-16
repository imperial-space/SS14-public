using Content.Shared.Imperial.Lavaland.Chaos;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Imperial.Lavaland.Chaos;

public sealed class ChaosRageSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private ChaosRageOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChaosRageStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<ChaosRageStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<ChaosRageStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnPlayerAttached);
        SubscribeLocalEvent<ChaosRageStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerDetachedEvent>>(OnPlayerDetached);

        _overlay = new ChaosRageOverlay();
    }

    private void OnApplied(Entity<ChaosRageStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity == args.Target)
            _overlayManager.AddOverlay(_overlay);
    }

    private void OnRemoved(Entity<ChaosRageStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        if (_player.LocalEntity is not { } local || _statusEffects.HasEffectComp<ChaosRageStatusEffectComponent>(local))
            return;

        _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(Entity<ChaosRageStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(Entity<ChaosRageStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
    {
        if (_player.LocalEntity is not { } local || _statusEffects.HasEffectComp<ChaosRageStatusEffectComponent>(local))
            return;

        _overlayManager.RemoveOverlay(_overlay);
    }
}
