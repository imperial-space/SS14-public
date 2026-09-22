using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticWeeepingHallucinationOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private float _alpha;
    private float _time;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        _time += args.DeltaSeconds;
        var pulse = 0.5f + 0.5f * MathF.Sin(_time * 1.5f);
        _alpha = 0.18f + pulse * 0.10f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        args.WorldHandle.DrawRect(args.WorldBounds, new Color(0.45f, 0f, 0.75f, _alpha));
    }
}
