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

/// <summary>Финальная сцена: большая картина исхода с медленным наездом, заголовок и две строки рассказа. Идёт до окна итогов.</summary>
public sealed class CadiaEndingControl : Control
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly Robust.Client.UserInterface.IUserInterfaceManager _ui = default!;

    public event Action? Finished;

    private const float TotalSeconds = 24f;
    private const float FadeSeconds = 1.6f;
    private const float TitleAt = 1.2f;
    private const float SecondTextAt = 12.5f;
    private const float Zoom = 0.1f;

    private readonly Texture _image;
    private readonly string _title;
    private readonly string _first;
    private readonly string _second;
    private readonly Color _accent;
    private readonly Font _titleFont;
    private readonly Font _font;
    private readonly TimeSpan _start;
    private bool _done;

    public CadiaEndingControl(CadiaEnding ending, int saved)
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        _start = _timing.RealTime;
        _titleFont = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 34);
        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 19);

        var key = ending.ToString().ToLowerInvariant();
        _image = _cache.GetResource<TextureResource>($"/Textures/Imperial/Cadia/War/Intro/ending_{key}.png").Texture;
        _title = Loc.GetString($"cadia-ending-{key}-title");
        _first = Loc.GetString($"cadia-ending-{key}-1", ("saved", saved));
        _second = Loc.GetString($"cadia-ending-{key}-2", ("saved", saved));
        _accent = ending is CadiaEnding.Held or CadiaEnding.Banished or CadiaEnding.Retreat
            ? Color.FromHex("#e8c66a")
            : Color.FromHex("#ff5a3c");

        var skip = new Button
        {
            Text = Loc.GetString("cadia-intro-skip"),
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
            Margin = new Thickness(0, 0, 24, 24),
            MinWidth = 140,
        };
        skip.OnPressed += _ => Finish();
        AddChild(skip);
    }

    private void Finish()
    {
        if (_done)
            return;
        _done = true;
        // Убираемся после обхода интерфейса: Finish зовётся из FrameUpdate, пока родитель перебирает детей.
        Visible = false;
        _ui.DeferAction(() =>
        {
            Finished?.Invoke();
            Orphan();
        });
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if ((float) (_timing.RealTime - _start).TotalSeconds >= TotalSeconds)
            Finish();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var size = PixelSize;
        handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), Color.Black);

        var elapsed = (float) (_timing.RealTime - _start).TotalSeconds;
        var scene = Math.Clamp(Math.Min(elapsed, TotalSeconds - elapsed) / FadeSeconds, 0f, 1f);
        var scale = Math.Max((float) size.X / _image.Width, (float) size.Y / _image.Height) * (1f + Zoom * elapsed / TotalSeconds);
        var drawn = new Vector2(_image.Width, _image.Height) * scale;
        handle.DrawTextureRect(_image, UIBox2.FromDimensions((new Vector2(size.X, size.Y) - drawn) / 2f, drawn), Color.White.WithAlpha(scene));

        // Заголовок сверху, рассказ снизу: так обе надписи не закрывают главное на картине.
        var title = Math.Clamp((elapsed - TitleAt) / FadeSeconds, 0f, 1f) * scene;
        handle.DrawRect(new UIBox2(0, 0, size.X, 96f), Color.Black.WithAlpha(0.6f * title));
        var titleWidth = handle.GetDimensions(_titleFont, _title, 1f).X;
        handle.DrawString(_titleFont, new Vector2((size.X - titleWidth) / 2f, 24f), _title, _accent.WithAlpha(title));

        var second = elapsed >= SecondTextAt;
        var local = second ? elapsed - SecondTextAt : elapsed;
        var length = second ? TotalSeconds - SecondTextAt : SecondTextAt;
        var alpha = Math.Clamp(Math.Min(local, length - local) / 1f, 0f, 1f);
        var lines = (second ? _second : _first).Split('\n');
        var lineHeight = _font.GetLineHeight(1f);
        var blockHeight = lines.Length * lineHeight + 26f;
        var top = size.Y - blockHeight - 56f;
        handle.DrawRect(new UIBox2(0, top, size.X, top + blockHeight), Color.Black.WithAlpha(0.7f * alpha));
        for (var i = 0; i < lines.Length; i++)
        {
            var width = handle.GetDimensions(_font, lines[i], 1f).X;
            handle.DrawString(_font, new Vector2((size.X - width) / 2f, top + 13f + i * lineHeight), lines[i], Color.White.WithAlpha(alpha));
        }
    }
}
