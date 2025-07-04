using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.DarkMage;

public sealed class DarkMageOverlay : Overlay
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    private readonly float _outerCircleValue = 10f;
    private readonly float _innerCircleValue = 10f;
    private readonly float _outerCircleMaxRadius = 60f;
    private readonly float _innerCircleMaxRadius = 60f;
    private TimeSpan _timeSpan;
    private readonly float _darknessAlphaOuter = 1f;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private ShaderInstance _circleMaskShader = default!;

    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public DarkMageOverlay(TimeSpan timeSpan)
    {
        _timeSpan = timeSpan;
        IoCManager.InjectDependencies(this);
        _circleMaskShader = _prototypeManager.Index<ShaderPrototype>("MedievalGradientCircleMask").InstanceUnique();
    }
    protected override void Draw(in OverlayDrawArgs args)
    {
        var time = 10f - (float)(_gameTiming.CurTime - _timeSpan).TotalSeconds;
        var lasted = Math.Clamp(time, 0f, 10f);
        _circleMaskShader.SetParameter("color", Color.Purple);
        _circleMaskShader.SetParameter("outerCircleRadius", _outerCircleValue);
        _circleMaskShader.SetParameter("innerCircleRadius", _innerCircleValue);
        _circleMaskShader.SetParameter("outerCircleMaxRadius", _outerCircleMaxRadius);
        _circleMaskShader.SetParameter("innerCircleMaxRadius", _innerCircleMaxRadius);
        _circleMaskShader.SetParameter("time", lasted);
        _circleMaskShader.SetParameter("darknessAlphaOuter", _darknessAlphaOuter);

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;

        worldHandle.UseShader(_circleMaskShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
    }
}
