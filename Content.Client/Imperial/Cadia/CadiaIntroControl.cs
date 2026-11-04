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

/// <summary>Вступительная сцена: три картины с медленным наездом и текстом, кнопка пропуска. Мир за ней живёт дальше.</summary>
public sealed class CadiaIntroControl : Control
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly Robust.Client.UserInterface.IUserInterfaceManager _ui = default!;

    public event Action? Finished;

    private const int Cards = 3;
    private const float CardSeconds = 9f;
    private const float FadeSeconds = 1.2f;
    private const float Zoom = 0.08f;
    private const float TextMargin = 60f;

    private readonly Texture[] _images = new Texture[Cards];
    private readonly string[] _texts = new string[Cards];
    private readonly Font _font;
    private readonly TimeSpan _start;
    private bool _done;

    public CadiaIntroControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        _start = _timing.RealTime;
        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 20);
        for (var i = 0; i < Cards; i++)
        {
            _images[i] = _cache.GetResource<TextureResource>($"/Textures/Imperial/Cadia/War/Intro/card{i + 1}.png").Texture;
            _texts[i] = Loc.GetString($"cadia-intro-{i + 1}");
        }

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
        if ((float) (_timing.RealTime - _start).TotalSeconds >= CardSeconds * Cards)
            Finish();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var size = PixelSize;
        handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), Color.Black);

        var elapsed = (float) (_timing.RealTime - _start).TotalSeconds;
        var card = Math.Clamp((int) (elapsed / CardSeconds), 0, Cards - 1);
        var local = elapsed - card * CardSeconds;
        var alpha = Math.Clamp(Math.Min(local, CardSeconds - local) / FadeSeconds, 0f, 1f);

        // Картина вписана целиком по большей стороне и медленно приближается.
        var image = _images[card];
        var scale = Math.Max((float) size.X / image.Width, (float) size.Y / image.Height) * (1f + Zoom * local / CardSeconds);
        var drawn = new Vector2(image.Width, image.Height) * scale;
        var at = (new Vector2(size.X, size.Y) - drawn) / 2f;
        handle.DrawTextureRect(image, UIBox2.FromDimensions(at, drawn), Color.White.WithAlpha(alpha));

        // Плашка снизу, чтобы текст читался на любой картине.
        var text = _texts[card];
        var lines = text.Split('\n');
        var lineHeight = _font.GetLineHeight(1f);
        var blockHeight = lines.Length * lineHeight + 28f;
        var top = size.Y - blockHeight - TextMargin;
        handle.DrawRect(new UIBox2(0, top, size.X, top + blockHeight), Color.Black.WithAlpha(0.7f * alpha));
        for (var i = 0; i < lines.Length; i++)
        {
            var width = handle.GetDimensions(_font, lines[i], 1f).X;
            handle.DrawString(_font, new Vector2((size.X - width) / 2f, top + 14f + i * lineHeight), lines[i], Color.FromHex("#e8c66a").WithAlpha(alpha));
        }
    }
}
