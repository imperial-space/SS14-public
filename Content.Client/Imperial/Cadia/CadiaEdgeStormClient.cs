using Content.Shared.Imperial.ZLevels;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Content.Shared.Imperial.Cadia;

namespace Content.Client.Imperial.Cadia;

/// <summary>
/// Буран у края поля Кадии: за скалами Кадийских врат стоит белая стена, а к ней снег сгущается полосами.
/// Только над поверхностью: верхний этаж — отдельные крыши и стены, а в катакомбах бурана нет.
/// Пока держится завеса пилонов, посреди поля стоит ещё и стена бурана между сторонами.
/// </summary>
public sealed class CadiaEdgeStormClient : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    private EdgeStormOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new EdgeStormOverlay();
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }

    private sealed class EdgeStormOverlay : Overlay
    {
        /// <summary>Буря посреди поля: плотная в середине, тает к краям; пока держится завеса пилонов.</summary>
        private void DrawMidStorm(DrawingHandleWorld handle, Box2 box)
        {
            var up = false;
            var wars = _entMan.EntityQueryEnumerator<CadiaWarStateComponent>();
            while (wars.MoveNext(out var war))
                up |= CadiaPylonVeilSystem.VeilUp(war);
            if (!up)
                return;
            var center = CadiaMidStorm.Column + 0.5f;
            var half = CadiaMidStorm.VisualHalfWidth;
            const int strips = 14;
            var step = half / strips;
            for (var i = 0; i < strips; i++)
            {
                var t = 1f - (float) i / strips;
                var color = Snow.WithAlpha(0.95f * t * t);
                var d0 = i * step;
                var d1 = d0 + step;
                handle.DrawRect(new Box2(center + d0, box.Bottom, center + d1, box.Top), color);
                handle.DrawRect(new Box2(center - d1, box.Bottom, center - d0, box.Top), color);
            }
        }

        [Dependency] private readonly IEntityManager _entMan = default!;
        [Dependency] private readonly IPlayerManager _player = default!;

        /// <summary>Сколько клеток от кромки сгущается буран и сколько полос в градиенте.</summary>
        private const float Depth = 14f;
        private const int Bands = 14;
        /// <summary>Стена бурана за кромкой: дальше камера никого не покажет.</summary>
        private const float Wall = 40f;
        private static readonly Color Snow = Color.FromHex("#dfe6ee");

        public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

        public EdgeStormOverlay()
        {
            IoCManager.InjectDependencies(this);
            ZIndex = 5;
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            if (_player.LocalEntity is not { } local || !_entMan.TryGetComponent<TransformComponent>(local, out var xform) ||
                xform.MapUid is not { } field || !_entMan.TryGetComponent<CadiaFieldBoundsComponent>(field, out var bounds) ||
                !_entMan.TryGetComponent<ZLevelMapComponent>(field, out var z) || z.Level != 0 || field != args.MapUid)
                return;

            var box = bounds.Bounds;
            var handle = args.WorldHandle;
            // Стена за кромкой — плотная, полосы к центру — всё прозрачнее (квадрат, чтобы край был резче).
            handle.DrawRect(new Box2(box.Left - Wall, box.Top, box.Right + Wall, box.Top + Wall), Snow);
            handle.DrawRect(new Box2(box.Left - Wall, box.Bottom - Wall, box.Right + Wall, box.Bottom), Snow);
            handle.DrawRect(new Box2(box.Left - Wall, box.Bottom, box.Left, box.Top), Snow);
            handle.DrawRect(new Box2(box.Right, box.Bottom, box.Right + Wall, box.Top), Snow);
            var step = Depth / Bands;
            for (var i = 0; i < Bands; i++)
            {
                var t = 1f - (float) i / Bands;
                var color = Snow.WithAlpha(0.85f * t * t);
                var d0 = i * step;
                var d1 = d0 + step;
                handle.DrawRect(new Box2(box.Left + d0, box.Bottom + d0, box.Left + d1, box.Top - d0), color);
                handle.DrawRect(new Box2(box.Right - d1, box.Bottom + d0, box.Right - d0, box.Top - d0), color);
                handle.DrawRect(new Box2(box.Left + d1, box.Bottom + d0, box.Right - d1, box.Bottom + d1), color);
                handle.DrawRect(new Box2(box.Left + d1, box.Top - d1, box.Right - d1, box.Top - d0), color);
            }

            DrawMidStorm(handle, box);
        }
    }
}
