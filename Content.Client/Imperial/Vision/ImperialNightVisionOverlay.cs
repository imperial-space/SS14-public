using System.Numerics;
using Content.Client.Light;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Vision;

/// <summary>
/// Добавляет ровный свет в карту освещения сразу после фона (крыши, окружения) и до ламп.
/// Тени от ламп остаются, но тёмные места становятся видны.
/// </summary>
public sealed class ImperialNightVisionOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> AdditiveShader = "ImperialAdditive";

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    private readonly ShaderInstance _additive;

    /// <summary>Сколько света добавить в темноту (линейный цвет).</summary>
    public Color Light = Color.Black;

    public ImperialNightVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = AfterLightTargetOverlay.ContentZIndex + 1;
        _additive = _proto.Index(AdditiveShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        if (viewport.Eye == null)
            return;

        var handle = args.WorldHandle;
        var lightScale = viewport.LightRenderTarget.Size / (Vector2) viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);
        var matrix = viewport.LightRenderTarget.GetWorldToLocalMatrix(viewport.Eye, scale);
        var bounds = args.WorldBounds;
        var light = Light;

        handle.RenderInRenderTarget(viewport.LightRenderTarget, () =>
        {
            handle.SetTransform(matrix);
            handle.UseShader(_additive);
            handle.DrawRect(bounds, light);
            handle.UseShader(null);
        }, null);

        handle.SetTransform(Matrix3x2.Identity);
    }
}
