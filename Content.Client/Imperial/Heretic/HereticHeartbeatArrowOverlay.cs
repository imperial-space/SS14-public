using System;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic;

/// <summary>
/// Экранный оверлей со стрелкой-компасом для способности «Сердцебиение Мансуса».
/// Указывает на текущую выбранную именную цель еретика.
/// Анимация: появление → слежение (вращается в сторону цели) → исчезновение.
/// </summary>
public sealed class HereticHeartbeatArrowOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly SharedTransformSystem _xformSys;
    private SharedAudioSystem _audioSys = default!;
    private EntityLookupSystem _lookup = default!;

    private const string HeartbeatSound = "/Audio/Imperial/heretic/sound_effects_singlebeat.ogg";
    private float _trackTimeLeft;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private static readonly ResPath RsiPath = new("/Textures/Imperial/heretic/navigate_arrow.rsi");

    // Кешированные кадры для трёх состояний
    private Texture[]? _appearFrames;
    private float[]?   _appearDelays;
    private Texture[]? _trackFrames;
    private float[]?   _trackDelays;
    private Texture[]? _disappearFrames;
    private float[]?   _disappearDelays;

    // Состояние анимации
    private enum AnimState { Hidden, Appearing, Tracking, Disappearing }
    private AnimState _anim = AnimState.Hidden;
    private TimeSpan _stateStart;
    private float _stateTime;

    // Текущий индекс кадра при перемотке
    private int _frameIdx;

    /// <summary>Сущность цели, в сторону которой смотрит стрелка.</summary>
    public EntityUid? Target { get; set; }

    public HereticHeartbeatArrowOverlay()
    {
        IoCManager.InjectDependencies(this);
        _xformSys = _entMan.System<SharedTransformSystem>();
        _audioSys = _entMan.System<SharedAudioSystem>();
        _lookup   = _entMan.System<EntityLookupSystem>();

        var cache = IoCManager.Resolve<IResourceCache>();
        if (!cache.TryGetResource<RSIResource>(RsiPath, out var res))
            return;

        var rsi = res.RSI;

        if (rsi.TryGetState("navigate_arrow_appear", out var appear))
        {
            _appearFrames = appear.GetFrames(RsiDirection.South);
            _appearDelays = appear.GetDelays();
        }

        if (rsi.TryGetState("multitool_arrow", out var track))
        {
            _trackFrames  = track.GetFrames(RsiDirection.South);
            _trackDelays  = track.GetDelays();
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
            _trackTimeLeft = 3f;
            return;
        }
        if (_anim == AnimState.Appearing)
            return;
        _anim          = AnimState.Appearing;
        _stateStart    = _timing.CurTime;
        _stateTime     = 0f;
        _frameIdx      = 0;
        _trackTimeLeft = 3f;
    }

    /// <summary>Запустить анимацию исчезновения (вызвать при закрытии BUI).</summary>
    public void Hide()
    {
        if (_anim is AnimState.Hidden or AnimState.Disappearing)
            return;
        _anim       = AnimState.Disappearing;
        _stateStart = _timing.CurTime;
        _stateTime  = 0f;
        _frameIdx   = 0;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var dt = (float)args.DeltaSeconds;

        switch (_anim)
        {
            case AnimState.Appearing when _appearDelays != null:
                _stateTime += dt;
                while (_frameIdx < _appearFrames!.Length - 1
                       && _stateTime >= _appearDelays[_frameIdx])
                {
                    _stateTime -= _appearDelays[_frameIdx];
                    _frameIdx++;
                }
                if (_frameIdx >= _appearFrames!.Length - 1
                    && _stateTime >= _appearDelays[_frameIdx])
                {
                    _anim      = AnimState.Tracking;
                    _stateTime = 0f;
                    _frameIdx  = 0;
                }
                break;

            case AnimState.Tracking when _trackDelays != null:
                _stateTime     += dt;
                _trackTimeLeft -= dt;
                if (_trackTimeLeft <= 0f)
                {
                    _anim      = AnimState.Disappearing;
                    _stateTime = 0f;
                    _frameIdx  = 0;
                    break;
                }
                while (_trackDelays.Length > 0
                       && _stateTime >= _trackDelays[_frameIdx])
                {
                    _stateTime -= _trackDelays[_frameIdx];
                    _frameIdx   = (_frameIdx + 1) % _trackFrames!.Length;
                }
                break;

            case AnimState.Disappearing when _disappearDelays != null:
                _stateTime += dt;
                while (_frameIdx < _disappearFrames!.Length - 1
                       && _stateTime >= _disappearDelays[_frameIdx])
                {
                    _stateTime -= _disappearDelays[_frameIdx];
                    _frameIdx++;
                }
                if (_frameIdx >= _disappearFrames!.Length - 1
                    && _stateTime >= _disappearDelays[_frameIdx])
                {
                    _anim = AnimState.Hidden;
                    // Остаётся в менеджере как «мёртвый» оверлей (Hidden = ничего не рисует).
                    // При следующей активации способности новый экземпляр заменит его через AddOverlay.
                }
                break;
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_anim == AnimState.Hidden) return;
        if (Target == null || !_entMan.EntityExists(Target.Value)) return;
        if (!_entMan.TryGetComponent(Target.Value, out TransformComponent? txform)) return;
        if (txform.MapID != args.MapId) return;

        var playerEnt = _player.LocalEntity;
        if (playerEnt == null) return;

        Texture? frame = _anim switch
        {
            AnimState.Appearing    => _appearFrames?   [Math.Clamp(_frameIdx, 0, (_appearFrames?.Length    ?? 1) - 1)],
            AnimState.Tracking     => _trackFrames?    [Math.Clamp(_frameIdx, 0, (_trackFrames?.Length     ?? 1) - 1)],
            AnimState.Disappearing => _disappearFrames?[Math.Clamp(_frameIdx, 0, (_disappearFrames?.Length ?? 1) - 1)],
            _                      => null,
        };
        if (frame == null) return;

        // Экранная позиция центра спрайта еретика
        var heroAabb   = _lookup.GetWorldAABB(playerEnt.Value);
        var heroScreen = _eyeManager.WorldToScreen(heroAabb.Center);

        // Стрелка — в центре спрайта еретика
        var arrowPos = new Vector2(heroScreen.X, heroScreen.Y);

        // Угол к цели от точки над головой
        var targetAabb   = _lookup.GetWorldAABB(Target.Value);
        var targetScreen = _eyeManager.WorldToScreen(targetAabb.Center);
        var screenDelta  = new Vector2(targetScreen.X, targetScreen.Y) - arrowPos;

        // atan2(dx, -dy): угол CW от «вверх» в Y-down пространстве
        var angle = new Angle(MathF.Atan2(screenDelta.X, -screenDelta.Y));

        const float HalfPx = 32f;
        args.ScreenHandle.SetTransform(arrowPos, angle);
        args.ScreenHandle.DrawTextureRect(frame, new UIBox2(-HalfPx, -HalfPx, HalfPx, HalfPx));
        args.ScreenHandle.SetTransform(Vector2.Zero, Angle.Zero);
    }
}
