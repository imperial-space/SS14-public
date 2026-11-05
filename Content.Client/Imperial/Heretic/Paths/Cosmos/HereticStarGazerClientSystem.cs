using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Cosmos;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;

namespace Content.Client.Imperial.Heretic.Paths.Cosmos;

public sealed class HereticStarGazerClientSystem : EntitySystem
{
    [Dependency] private readonly Robust.Shared.Timing.IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;

    private TimeSpan _nextSend;
    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(0.1); // 10 обновлений в секунду

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_timing.RealTime < _nextSend)
            return;

        var player = _player.LocalEntity;
        if (player == null)
            return;

        if (!TryComp<HereticStarGazerComponent>(player.Value, out var comp))
            return;

        if (!comp.BeamWindUp && !comp.BeamChanneling)
            return;

        var mousePos = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mousePos.MapId == MapId.Nullspace)
            return;

        RaiseNetworkEvent(new HereticStarGazerCursorUpdateEvent(GetNetEntity(player.Value), mousePos));
        _nextSend = _timing.RealTime + SendInterval;
    }
}
