using Robust.Client.Graphics;
using Robust.Client.Player;
using Content.Shared.CCVar;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Content.Shared.Eye.Blinding.Components;
using Robust.Shared.Configuration;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Plague;

public sealed class VomitSicknessOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _drunkShader;
    private readonly ShaderInstance _blurShaderX;
    private readonly ShaderInstance _blurShaderY;

    public float CurrentBoozePower = 0.0f;

    private const float VisualThreshold = 10.0f;
    private const float PowerDivisor = 250.0f;

    private float _visualScale = 0;

    public VomitSicknessOverlay()
    {
        IoCManager.InjectDependencies(this);
        _drunkShader = _prototypeManager.Index<ShaderPrototype>("Drunk").InstanceUnique();
        _blurShaderX = _prototypeManager.Index<ShaderPrototype>("BlurryVisionX").InstanceUnique();
        _blurShaderY = _prototypeManager.Index<ShaderPrototype>("BlurryVisionY").InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {

        var playerEntity = _playerManager.LocalEntity;

        if (playerEntity == null)
            return;

        if (!_entityManager.TryGetComponent<VomitSicknessComponent>(_playerManager.LocalEntity, out var comp))
            return;
        var cur = _timing.CurTime;
        if (cur < comp.StartTime || cur > comp.EndTime)
            return;

        if (_entityManager.TryGetComponent<BlindableComponent>(_playerManager.LocalEntity, out var blindComp)
                && blindComp.IsBlind)
            return;

        var middle = comp.EndTime - TimeSpan.FromSeconds(comp.Duration) / 2;

        CurrentBoozePower = Math.Max(CurrentBoozePower + (_timing.CurTime < middle ? 17 : -17) * args.DeltaSeconds, 0.0f);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        _visualScale = PowerToVisual(CurrentBoozePower);
        return _visualScale > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _drunkShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _drunkShader.SetParameter("boozePower", _visualScale + 35f);
        handle.UseShader(_drunkShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);

        _blurShaderX.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _blurShaderX.SetParameter("BLUR_AMOUNT", _visualScale * 1.5f);
        handle.UseShader(_blurShaderX);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);

        _blurShaderY.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _blurShaderY.SetParameter("BLUR_AMOUNT", _visualScale);
        handle.UseShader(_blurShaderY);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    private float PowerToVisual(float boozePower)
    {
        return Math.Clamp((boozePower - VisualThreshold) / PowerDivisor, 0.0f, 1.0f);
    }
}
