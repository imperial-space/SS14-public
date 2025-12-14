using Content.Shared.Imperial.Medieval.Illitid;

namespace Content.Client.Imperial.Medieval.Illitid;

using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.StatusEffect;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

public sealed class IllitidFlashOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SharedIllitidSystem _illitid;
    private readonly StatusEffectsSystem _statusSys;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _shader;
    public float PercentComplete = 0.0f;
    public Texture? ScreenshotTexture;

    public IllitidFlashOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index<ShaderPrototype>("IllitidFlashedEffect").InstanceUnique();
        _illitid = _entityManager.System<SharedIllitidSystem>();
        _statusSys = _entityManager.System<StatusEffectsSystem>();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _playerManager.LocalEntity;

        if (playerEntity == null)
            return;

        if (!_entityManager.HasComponent<IllitidFlashedComponent>(playerEntity)
            || !_entityManager.TryGetComponent<StatusEffectsComponent>(playerEntity, out var status))
            return;

        if (!_statusSys.TryGetTime(playerEntity.Value, _illitid.IllitidFlashedKey, out var time, status))
            return;

        var curTime = _timing.CurTime;
        var lastsFor = (float)(time.Value.Item2 - time.Value.Item1).TotalSeconds;
        var timeDone = (float)(curTime - time.Value.Item1).TotalSeconds;

        PercentComplete = timeDone / lastsFor;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;
        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        return PercentComplete < 1.0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (RequestScreenTexture && ScreenTexture != null)
        {
            ScreenshotTexture = ScreenTexture;
            RequestScreenTexture = false;
        }
        if (ScreenshotTexture == null)
            return;

        var playerEntity = _playerManager.LocalEntity;

        if (playerEntity == null)
            return;

        if (!_entityManager.TryGetComponent<IllitidFlashedComponent>(playerEntity, out var flashed))
            return;

        var worldHandle = args.WorldHandle;
        _shader.SetParameter("percentComplete", PercentComplete);
        _shader.SetParameter("vignetteStrength", flashed.Strength);
        worldHandle.UseShader(_shader);
        worldHandle.DrawTextureRectRegion(ScreenshotTexture, args.WorldBounds);
        worldHandle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();
        ScreenshotTexture = null;
    }
}
