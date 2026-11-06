using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

/// <summary>Лента событий слева: короткие строки о главном в бою, старые гаснут сами.</summary>
public sealed class CadiaFeedOverlay : Overlay
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int MaxLines = 6;
    private const float LifeSeconds = 14f;
    private const float FadeSeconds = 2f;
    private const float RowHeight = 20f;
    private const float MaxWidth = 380f;

    private readonly Font _font;
    private readonly List<(string Text, Color Color, TimeSpan At)> _lines = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaFeedOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 11);
    }

    public void Add(string text, Color color)
    {
        _lines.Add((text, color, _timing.RealTime));
        if (_lines.Count > MaxLines)
            _lines.RemoveAt(0);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var now = _timing.RealTime;
        _lines.RemoveAll(l => (now - l.At).TotalSeconds >= LifeSeconds);
        if (_lines.Count == 0)
            return;

        var handle = args.ScreenHandle;
        var x = args.ViewportBounds.Left + 12f;
        var y = args.ViewportBounds.Top + args.ViewportBounds.Height * 0.32f;
        foreach (var (text, color, at) in _lines)
        {
            var left = LifeSeconds - (float)(now - at).TotalSeconds;
            var alpha = Math.Clamp(left / FadeSeconds, 0f, 1f);
            var width = Math.Min(handle.GetDimensions(_font, text, 1f).X + 14f, MaxWidth);
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(width, RowHeight - 2f)), Color.FromHex("#0b0d0a").WithAlpha(0.75f * alpha));
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(3f, RowHeight - 2f)), color.WithAlpha(alpha));
            handle.DrawString(_font, new Vector2(x + 8f, y + 1f), text, Color.White.WithAlpha(alpha));
            y += RowHeight;
        }
    }
}
