using Content.Shared.Imperial.Lavaland.Chaos;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Lavaland.Chaos;

public sealed class ChaosRageOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "ChaosRage";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly StatusEffectsSystem _statusEffects;
    private readonly ShaderInstance _rageShader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private float _alpha;
    private float _pulse;

    public ChaosRageOverlay()
    {
        IoCManager.InjectDependencies(this);
        _statusEffects = _systems.GetEntitySystem<StatusEffectsSystem>();
        _rageShader = _prototype.Index(Shader).InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _player.LocalEntity;
        if (playerEntity == null)
        {
            _alpha = 0f;
            return;
        }

        if (!_statusEffects.TryGetEffectsEndTimeWithComp<ChaosRageStatusEffectComponent>(playerEntity, out var endTime))
        {
            _alpha = 0f;
            return;
        }

        var secondsLeft = endTime is { } end
            ? MathF.Max(0f, (float) (end - _timing.CurTime).TotalSeconds)
            : 120f;

        var fade = MathF.Min(1f, secondsLeft / 2f);
        var pulse = 0.5f + 0.5f * MathF.Sin((float) _timing.CurTime.TotalSeconds * 6f);
        _alpha = (0.10f + pulse * 0.08f) * fade;
        _pulse = pulse;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_alpha <= 0f)
            return false;

        if (!_entityManager.TryGetComponent(_player.LocalEntity, out EyeComponent? eyeComp))
            return false;

        return args.Viewport.Eye == eyeComp.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
        {
            args.WorldHandle.DrawRect(args.WorldBounds, new Color(0.95f, 0.05f, 0.05f, _alpha));
            return;
        }

        _rageShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _rageShader.SetParameter("TintStrength", _alpha);
        _rageShader.SetParameter("Pulse", _pulse);

        args.WorldHandle.UseShader(_rageShader);
        args.WorldHandle.DrawRect(args.WorldBounds, Color.White);
        args.WorldHandle.UseShader(null);
    }
}
