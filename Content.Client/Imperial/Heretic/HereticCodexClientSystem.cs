using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticCodexClientSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticCodexComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HereticCodexComponent, AfterAutoHandleStateEvent>(OnStateHandled);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<HereticCodexComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var codex, out var sprite))
        {
            if (codex.TransitionEndTime is not { } end || _timing.CurTime < end)
                continue;

            codex.TransitionEndTime = null;
            var finalState = codex.IsOpen ? codex.SpriteStateOpen : codex.SpriteStateClosed;
            _sprite.LayerSetRsiState((uid, sprite), 0, finalState);
        }
    }

    private void OnStartup(Entity<HereticCodexComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.VisualIsOpen = ent.Comp.IsOpen;
        var state = ent.Comp.IsOpen ? ent.Comp.SpriteStateOpen : ent.Comp.SpriteStateClosed;
        _sprite.LayerSetRsiState(ent.Owner, 0, state);
    }

    private void OnStateHandled(Entity<HereticCodexComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.VisualIsOpen == null || ent.Comp.VisualIsOpen == ent.Comp.IsOpen)
            return;

        ent.Comp.VisualIsOpen = ent.Comp.IsOpen;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var transitionState = ent.Comp.IsOpen ? ent.Comp.SpriteStateOpening : ent.Comp.SpriteStateClosing;
        _sprite.LayerSetRsiState((ent, sprite), 0, transitionState);

        var length = ent.Comp.DefaultTransitionLength;
        if (sprite.BaseRSI != null
            && sprite.BaseRSI.TryGetState(transitionState, out var rsiState)
            && rsiState.AnimationLength > 0f)
        {
            length = TimeSpan.FromSeconds(rsiState.AnimationLength);
        }

        ent.Comp.TransitionEndTime = _timing.CurTime + length;
    }
}
