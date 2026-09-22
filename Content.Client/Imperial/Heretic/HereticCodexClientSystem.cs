using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticCodexClientSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, bool> _lastOpen = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticCodexComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HereticCodexComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<HereticCodexComponent, AfterAutoHandleStateEvent>(OnStateHandled);
    }

    private void OnInit(EntityUid uid, HereticCodexComponent comp, ComponentInit args)
    {
        _lastOpen[uid] = comp.IsOpen;
        SetSprite(uid, comp.IsOpen ? comp.SpriteStateOpen : comp.SpriteStateClosed);
    }

    private void OnRemove(EntityUid uid, HereticCodexComponent comp, ComponentRemove args)
    {
        _lastOpen.Remove(uid);
    }

    private void OnStateHandled(EntityUid uid, HereticCodexComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (!_lastOpen.TryGetValue(uid, out var was) || was == comp.IsOpen)
            return;

        _lastOpen[uid] = comp.IsOpen;

        var transitionState = comp.IsOpen ? comp.SpriteStateOpening : comp.SpriteStateClosing;
        var finalState      = comp.IsOpen ? comp.SpriteStateOpen    : comp.SpriteStateClosed;

        SetSprite(uid, transitionState);

        var delayMs = 500;
        if (TryComp<SpriteComponent>(uid, out var sprite) &&
            sprite.BaseRSI != null &&
            sprite.BaseRSI.TryGetState(transitionState, out var rsiState) &&
            rsiState.AnimationLength > 0f)
        {
            delayMs = (int)(rsiState.AnimationLength * 1000);
        }

        Timer.Spawn(delayMs, () =>
        {
            if (Deleted(uid) || !TryComp<SpriteComponent>(uid, out _))
                return;
            SetSprite(uid, finalState);
        });
    }

    private void SetSprite(EntityUid uid, string state)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            sprite.LayerSetState(0, state);
    }
}
