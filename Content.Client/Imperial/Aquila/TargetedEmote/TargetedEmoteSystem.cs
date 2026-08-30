using Content.Client.Interactable.Components;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Emoting;
using Content.Shared.Interaction;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Content.Shared.Mind.Components;
using Robust.Shared.Timing;
using Robust.Client.Player;
using Robust.Client.Input;
using Robust.Client.GameObjects;
using static Robust.Shared.Input.Binding.PointerInputCmdHandler;
using Content.Shared.Imperial.Aquila.TargetedEmote;

namespace Content.Client.Imperial.Aquila.TargetedEmote;

public sealed class TargetedEmoteSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private string? _pendingEmoteId;
    private float _pendingRange;

    private readonly List<EntityUid> _outlinedEntities = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmotingComponent, InRangeOverrideEvent>(OnInRangeOverride);

        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Use,
                new PointerInputCmdHandler(OnUseClick, outsidePrediction: true),
                typeof(SharedInteractionSystem))
            .BindBefore(EngineKeyFunctions.UIRightClick,
                new PointerInputCmdHandler(OnRightClick, outsidePrediction: true))
            .Register<TargetedEmoteSystem>();
    }
    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<TargetedEmoteSystem>();
    }

    private bool OnRightClick(in PointerInputCmdArgs args)
    {
        if (!_timing.IsFirstTimePredicted)
            return false;

        if (_pendingEmoteId == null)
            return false;

        Cancel();
        return true;
    }

    private bool OnUseClick(in PointerInputCmdArgs args)
    {
        if (!_timing.IsFirstTimePredicted)
            return false;

        if (_pendingEmoteId == null)
            return false;

        if (args.EntityUid == EntityUid.Invalid)
        {
            Cancel();
            return true;
        }
        var target = args.EntityUid;

        if (!TryValidateTarget(target))
        {
            Cancel();
            return true;
        }

        RaiseNetworkEvent(new PlayTargetedEmoteMessage(
            _pendingEmoteId,
            GetNetEntity(target)));

        Cancel();
        return true;
    }

    private bool TryValidateTarget(EntityUid target)
    {
        if (target == EntityUid.Invalid)
            return false;

        if (_player.LocalEntity == null)
            return false;

        if (target == _player.LocalEntity.Value)
            return false;

        if (!HasComp<MindContainerComponent>(target))
            return false;

        return IsTargetInRange(_player.LocalEntity.Value, target);
    }

    private bool IsTargetInRange(EntityUid source, EntityUid target)
    {
        var sourcePos = _transform.GetMapCoordinates(source);
        var targetPos = _transform.GetMapCoordinates(target);
        return (sourcePos.Position - targetPos.Position).Length() <= _pendingRange;
    }

    public void StartTargeting(TargetedEmotePrototype emote)
    {
        Cancel();

        _pendingEmoteId = emote.ID;
        _pendingRange = emote.Range;

        var query = EntityQueryEnumerator<MindContainerComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (uid == _player.LocalEntity)
                continue;

            if (!TryComp<InteractionOutlineComponent>(uid, out var outline))
                continue;

            var inRange = IsTargetInRange(_player.LocalEntity!.Value, uid);

            outline.OnMouseEnter(uid, inRange, 1);
            _outlinedEntities.Add(uid);
        }
    }

    public void Cancel()
    {
        _pendingEmoteId = null;

        foreach (var uid in _outlinedEntities)
        {
            if (TryComp<InteractionOutlineComponent>(uid, out var outline))
                outline.OnMouseLeave(uid);
        }

        _outlinedEntities.Clear();
    }
    private void OnInRangeOverride(Entity<EmotingComponent> ent, ref InRangeOverrideEvent args)
    {
        if (ent.Owner != _player.LocalEntity)
            return;

        if (_pendingEmoteId == null)
            return;

        args.Handled = true;
        args.InRange = IsTargetInRange(ent.Owner, args.Target);
    }

    private void FrameUpdateTargetOutline()
    {
        if (_pendingEmoteId == null || _player.LocalEntity == null)
            return;

        foreach (var uid in _outlinedEntities)
        {
            if (!Exists(uid))
                continue;

            if (!TryComp<InteractionOutlineComponent>(uid, out var outline))
                continue;

            if (!TryComp<SpriteComponent>(uid, out var sprite))
                continue;

            var inRange = IsTargetInRange(_player.LocalEntity.Value, uid);

            if (sprite.PostShader == null)
                outline.OnMouseEnter(uid, inRange, 1);
            else
                outline.UpdateInRange(uid, inRange, 1);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        FrameUpdateTargetOutline();
    }
}
