using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticStarGazerClientSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye      = default!;
    [Dependency] private readonly IInputManager _input   = default!;

    private float _sendTimer;
    private const float SendInterval = 0.1f; // 10 обновлений в секунду

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _sendTimer -= frameTime;
        if (_sendTimer > 0f)
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
        _sendTimer = SendInterval;
    }
}
