using Content.Shared.Imperial.Cadia;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

/// <summary>Завеса пилонов пала: небо на миг заливает красным варпом и медленно отпускает.</summary>
public sealed class CadiaVeilFallClient : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private VeilFlashOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CadiaVeilFallEvent>(OnFall);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }

    private void OnFall(CadiaVeilFallEvent ev)
    {
        if (_overlay == null)
        {
            _overlay = new VeilFlashOverlay(_timing);
            _overlays.AddOverlay(_overlay);
        }
        _overlay.Start = _timing.RealTime;
    }

    private sealed class VeilFlashOverlay(IGameTiming timing) : Overlay
    {
        /// <summary>Вспышка за четверть секунды, затем красное небо гаснет.</summary>
        private const float Rise = 0.25f;
        private const float Fade = 2.2f;
        private const float Peak = 0.5f;
        private static readonly Color Warp = Color.FromHex("#b0101a");

        public TimeSpan Start = TimeSpan.MinValue;

        public override OverlaySpace Space => OverlaySpace.ScreenSpace;

        protected override void Draw(in OverlayDrawArgs args)
        {
            var t = (float) (timing.RealTime - Start).TotalSeconds;
            if (t < 0f || t > Rise + Fade)
                return;
            var alpha = t < Rise ? t / Rise : 1f - (t - Rise) / Fade;
            args.ScreenHandle.DrawRect(args.ViewportBounds, Warp.WithAlpha(Peak * alpha));
        }
    }
}
