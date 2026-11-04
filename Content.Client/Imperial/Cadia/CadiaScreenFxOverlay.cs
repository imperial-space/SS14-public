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
