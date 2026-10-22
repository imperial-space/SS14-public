using System.Numerics;
using Content.Shared.Imperial.Epsilon;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Epsilon;

/// <summary>
/// Надписи протокола «Эпсилон»: каждая печатается по буквам в свой отрезок трека,
/// цвет уходит от жёлтого ЦК к фиолетовому коду «Эпсилон», текст дрожит и светится.
/// </summary>
public sealed class EpsilonTextOverlay : Overlay
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    /// <param name="From">Начало печати.</param>
    /// <param name="Typed">К этому моменту напечатана вся строка.</param>
    /// <param name="Until">Строка исчезает (последняя — плавно гаснет к этому моменту).</param>
    /// <param name="Mix">0 — жёлтый ЦК, 1 — фиолетовый «Эпсилон».</param>
    private readonly record struct Line(float From, float Typed, float Until, string Text, float Mix, bool Big);

    private static readonly Line[] Lines =
    {
        new(3.8f, 4.5f, 5.1f, "внимание", 0f, true),
        new(5.1f, 5.8f, 6.1f, "Внимание", 0.5f, true),
        new(6.1f, 6.8f, 7f, "ВНИМАНИЕ", 0.85f, true),
        new(EpsilonTimeline.LightsOutStart, EpsilonTimeline.LightsOutEnd, 16f, "Станция теперь под контролем центрального командования", 1f, false),
    };

    private const float FadeOut = 2f;

    private readonly TimeSpan _start;
    private readonly Font _bigFont;
    private readonly Font _lineFont;

    public bool Finished { get; private set; }

    public EpsilonTextOverlay(TimeSpan start)
    {
        IoCManager.InjectDependencies(this);
        _start = start;

        var cache = IoCManager.Resolve<IResourceCache>();
        var bold = cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf");
        _bigFont = new VectorFont(bold, 56);
        _lineFont = new VectorFont(bold, 30);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var elapsed = (float) (_timing.CurTime - _start).TotalSeconds;
        if (elapsed >= Lines[^1].Until)
        {
            Finished = true;
            return;
        }

        Line? current = null;
        foreach (var line in Lines)
        {
            if (elapsed >= line.From && elapsed < line.Until)
                current = line;
        }

        if (current is not { } shown)
            return;

        var handle = args.ScreenHandle;
        var font = shown.Big ? _bigFont : _lineFont;
        var progress = Math.Clamp((elapsed - shown.From) / (shown.Typed - shown.From), 0f, 1f);
        var chars = (int) MathF.Ceiling(progress * shown.Text.Length);
        var alpha = shown == Lines[^1] ? Math.Clamp((shown.Until - elapsed) / FadeOut, 0f, 1f) : 1f;

        var color = Color.InterpolateBetween(EpsilonTimeline.CentcomYellow, EpsilonTimeline.EpsilonViolet, shown.Mix);

        // Строка стоит на месте, пока печатается: позиция считается по полному тексту.
        var bounds = args.ViewportBounds;
        var size = handle.GetDimensions(font, shown.Text, 1f);
        var origin = new Vector2(bounds.Left + (bounds.Width - size.X) / 2f, bounds.Top + bounds.Height * 0.28f - size.Y / 2f);

        // Тёмная полоса под надписью.
        var band = new UIBox2(bounds.Left, origin.Y - size.Y * 0.35f, bounds.Right, origin.Y + size.Y * 1.35f);
        handle.DrawRect(band, Color.Black.WithAlpha(0.45f * alpha));

        // Чем фиолетовее, тем сильнее дрожь.
        var shake = shown.Mix * 2f;
        var jitter = new Vector2(MathF.Sin(elapsed * 53f) * shake, MathF.Cos(elapsed * 71f) * shake);
        var pos = origin + jitter;
        var text = shown.Text.AsSpan(0, chars);

        var glow = color.WithAlpha(0.3f * alpha);
        foreach (var offset in new[] { new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, -2), new Vector2(0, 2) })
        {
            handle.DrawString(font, pos + offset, text, 1f, glow);
        }

        var advance = handle.DrawString(font, pos, text, 1f, color.WithAlpha(alpha));

        // Мигающий курсор, пока строка печатается.
        if (progress < 1f && (int) (elapsed * 8f) % 2 == 0)
            handle.DrawString(font, pos + new Vector2(advance.X, 0), "_", 1f, color.WithAlpha(alpha));
    }
}
