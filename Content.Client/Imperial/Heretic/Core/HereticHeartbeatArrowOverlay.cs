using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic.Core;

/// <summary>
/// Экранный оверлей со стрелкой-компасом для способности «Сердцебиение Мансуса».
/// Указывает на текущую выбранную именную цель еретика.
/// Анимация: появление → слежение (вращается в сторону цели) → исчезновение.
/// </summary>
public sealed class HereticHeartbeatArrowOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private static readonly ResPath RsiPath = new("/Textures/Imperial/heretic/navigate_arrow.rsi");

    /// <summary>Половина размера стрелки на экране, в пикселях.</summary>
    private const float HalfSizePx = 32f;

    /// <summary>Сколько секунд стрелка следит за целью перед исчезновением.</summary>
    private const float TrackDuration = 3f;

    private readonly EntityLookupSystem _lookup;

    private float _trackTimeLeft;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    // Кешированные кадры для трёх состояний
    private Texture[]? _appearFrames;
    private float[]? _appearDelays;
    private Texture[]? _trackFrames;
    private float[]? _trackDelays;
    private Texture[]? _disappearFrames;
    private float[]? _disappearDelays;

    // Состояние анимации
    private enum AnimState
    {
        Hidden,
        Appearing,
        Tracking,
        Disappearing,
    }

    private AnimState _anim = AnimState.Hidden;
    private float _stateTime;

    // Текущий индекс кадра при перемотке
    private int _frameIdx;

    /// <summary>Сущность цели, в сторону которой смотрит стрелка.</summary>
    public EntityUid? Target { get; set; }

    public HereticHeartbeatArrowOverlay()
    {
        IoCManager.InjectDependencies(this);
        _lookup = _entMan.System<EntityLookupSystem>();

        if (!_resourceCache.TryGetResource<RSIResource>(RsiPath, out var res))
            return;

        var rsi = res.RSI;

        if (rsi.TryGetState("navigate_arrow_appear", out var appear))
        {
            _appearFrames = appear.GetFrames(RsiDirection.South);
            _appearDelays = appear.GetDelays();
        }

        if (rsi.TryGetState("multitool_arrow", out var track))
        {
            _trackFrames = track.GetFrames(RsiDirection.South);
            _trackDelays = track.GetDelays();
        }

        if (rsi.TryGetState("navigate_arrow_disappear", out var disappear))
        {
            _disappearFrames = disappear.GetFrames(RsiDirection.South);
            _disappearDelays = disappear.GetDelays();
        }
    }

    /// <summary>Запустить анимацию появления (вызвать при выборе цели).</summary>
    public void Show()
    {
        if (_anim == AnimState.Tracking)
        {
            _trackTimeLeft = TrackDuration;
            return;
        }

        if (_anim == AnimState.Appearing)
            return;

        _anim = AnimState.Appearing;
        _stateTime = 0f;
        _frameIdx = 0;
        _trackTimeLeft = TrackDuration;
    }

    /// <summary>Запустить анимацию исчезновения (вызвать при закрытии BUI).</summary>
    public void Hide()
    {
        if (_anim is AnimState.Hidden or AnimState.Disappearing)
            return;

        _anim = AnimState.Disappearing;
        _stateTime = 0f;
        _frameIdx = 0;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var dt = args.DeltaSeconds;

        switch (_anim)
        {
            case AnimState.Appearing when _appearFrames != null && _appearDelays != null:
                if (AdvanceOneShot(_appearFrames, _appearDelays, dt))
                    SetState(AnimState.Tracking);
                break;

            case AnimState.Tracking when _trackFrames != null && _trackDelays != null:
                _trackTimeLeft -= dt;
                if (_trackTimeLeft <= 0f)
                {
                    SetState(AnimState.Disappearing);
                    break;
                }

                _stateTime += dt;
                while (_trackDelays.Length > 0 && _stateTime >= _trackDelays[_frameIdx])
                {
                    _stateTime -= _trackDelays[_frameIdx];
                    _frameIdx = (_frameIdx + 1) % _trackFrames.Length;
                }

                break;

            case AnimState.Disappearing when _disappearFrames != null && _disappearDelays != null:
                // Остаётся в менеджере как «мёртвый» оверлей (Hidden = ничего не рисует).
                if (AdvanceOneShot(_disappearFrames, _disappearDelays, dt))
                    _anim = AnimState.Hidden;
                break;
        }
    }

    /// <summary>
    /// Проигрывает неповторяющуюся анимацию. Возвращает true, когда показан последний кадр.
    /// </summary>
    private bool AdvanceOneShot(Texture[] frames, float[] delays, float dt)
    {
        _stateTime += dt;
        while (_frameIdx < frames.Length - 1 && _stateTime >= delays[_frameIdx])
        {
            _stateTime -= delays[_frameIdx];
            _frameIdx++;
        }

        return _frameIdx >= frames.Length - 1 && _stateTime >= delays[_frameIdx];
    }

    private void SetState(AnimState state)
    {
        _anim = state;
        _stateTime = 0f;
        _frameIdx = 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_anim == AnimState.Hidden)
            return;

        if (Target is not { } target || !_entMan.TryGetComponent(target, out TransformComponent? targetXform))
            return;

        if (targetXform.MapID != args.MapId)
            return;

        if (_player.LocalEntity is not { } player)
            return;

        var frames = _anim switch
        {
            AnimState.Appearing => _appearFrames,
            AnimState.Tracking => _trackFrames,
            AnimState.Disappearing => _disappearFrames,
            _ => null,
        };

        if (frames == null || frames.Length == 0)
            return;

        var frame = frames[Math.Clamp(_frameIdx, 0, frames.Length - 1)];

        // Стрелка рисуется в центре спрайта еретика.
        var arrowPos = _eyeManager.WorldToScreen(_lookup.GetWorldAABB(player).Center);
        var targetPos = _eyeManager.WorldToScreen(_lookup.GetWorldAABB(target).Center);
        var screenDelta = targetPos - arrowPos;

        // atan2(dx, -dy): угол по часовой стрелке от «вверх» в экранных координатах (Y вниз).
        var angle = new Angle(MathF.Atan2(screenDelta.X, -screenDelta.Y));

        args.ScreenHandle.SetTransform(arrowPos, angle);
        args.ScreenHandle.DrawTextureRect(frame, new UIBox2(-HalfSizePx, -HalfSizePx, HalfSizePx, HalfSizePx));
        args.ScreenHandle.SetTransform(Vector2.Zero, Angle.Zero);
    }
}
