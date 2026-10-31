using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

/// <summary>Большие моменты войны: вспышка на весь экран и тряска. И значки раненых для медиков.</summary>
public sealed class CadiaFrontClientSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;

    private CadiaScreenFxOverlay? _fx;
    private CadiaMedicOverlay? _medic;
    private int _kicksLeft;
    private float _kickPower;
    private TimeSpan _nextKick;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CadiaScreenFxEvent>(OnFx);
        _fx = new CadiaScreenFxOverlay();
        _medic = new CadiaMedicOverlay();
        _overlays.AddOverlay(_fx);
        _overlays.AddOverlay(_medic);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_fx != null)
            _overlays.RemoveOverlay(_fx);
        if (_medic != null)
            _overlays.RemoveOverlay(_medic);
    }

    private void OnFx(CadiaScreenFxEvent ev)
    {
        var (color, seconds, power) = ev.Kind switch
        {
            CadiaScreenFx.PylonFall => (Color.FromHex("#c02a2a"), 1.6f, 1.2f),
            CadiaScreenFx.Prince => (Color.FromHex("#6a0a1a"), 3f, 2f),
            _ => (Color.White, 5f, 3f),
        };
        _fx?.Start(color, seconds);
        _kicksLeft = 10;
        _kickPower = power;
        _nextKick = _timing.RealTime;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_kicksLeft <= 0 || _timing.RealTime < _nextKick || _player.LocalEntity is not { } local)
            return;
        _nextKick = _timing.RealTime + TimeSpan.FromSeconds(0.12);
        _kicksLeft--;
        _recoil.KickCamera(local, _random.NextVector2(_kickPower));
    }
}

public sealed class CadiaScreenFxOverlay : Overlay
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private Color _color;
    private TimeSpan _start;
    private float _seconds;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaScreenFxOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    public void Start(Color color, float seconds)
    {
        _color = color;
        _seconds = seconds;
        _start = _timing.RealTime;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_seconds <= 0f)
            return;
        var t = (float) (_timing.RealTime - _start).TotalSeconds / _seconds;
        if (t >= 1f)
        {
            _seconds = 0f;
            return;
        }
        args.ScreenHandle.DrawRect(args.ViewportBounds, _color.WithAlpha((1f - t) * (1f - t) * 0.7f));
    }
}

/// <summary>Медик видит раненых своей стороны: красный крест, а за краем экрана — стрелка у края.</summary>
public sealed class CadiaMedicOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    private static readonly Color Cross = Color.FromHex("#ff3a2a");
    private const float Arm = 7f;
    private const float Width = 2.5f;
    private const float EdgeInset = 24f;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaMedicOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local || !_entMan.HasComponent<CadiaMedicSightComponent>(local) ||
            !_entMan.TryGetComponent<CadiaFactionMemberComponent>(local, out var mine))
            return;

        var xforms = _entMan.System<SharedTransformSystem>();
        var mapId = _entMan.GetComponent<TransformComponent>(local).MapID;
        var bounds = new Box2(args.ViewportBounds.Left + EdgeInset, args.ViewportBounds.Top + EdgeInset,
            args.ViewportBounds.Right - EdgeInset, args.ViewportBounds.Bottom - EdgeInset);
        var handle = args.ScreenHandle;

        var query = _entMan.EntityQueryEnumerator<MobStateComponent, CadiaFactionMemberComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var state, out var member, out var xform))
        {
            if (uid == local || state.CurrentState != MobState.Critical || member.Faction != mine.Faction || xform.MapID != mapId)
                continue;
            var screen = _eye.WorldToScreen(xforms.GetWorldPosition(xform));
            var clamped = new Vector2(Math.Clamp(screen.X, bounds.Left, bounds.Right), Math.Clamp(screen.Y, bounds.Bottom, bounds.Top));
            handle.DrawRect(UIBox2.FromDimensions(clamped - new Vector2(Arm, Width), new Vector2(Arm * 2, Width * 2)), Cross);
            handle.DrawRect(UIBox2.FromDimensions(clamped - new Vector2(Width, Arm), new Vector2(Width * 2, Arm * 2)), Cross);
        }
    }
}
