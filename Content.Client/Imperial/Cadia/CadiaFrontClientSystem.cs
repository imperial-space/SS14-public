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
