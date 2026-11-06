using System.Numerics;
using System.Text;
using Content.Client.Message;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Heretic.UI;

/// <summary>
/// Переливающаяся надпись в меню персонажа еретика, отсылающая к Книге Мансуса.
/// </summary>
public sealed class HereticMansusControl : BoxContainer
{
    private const float HueSpeed = 0.4f;

    private readonly RichTextLabel _label;
    private readonly string _text;
    private float _time;

    public HereticMansusControl()
    {
        Orientation = LayoutOrientation.Vertical;
        _text = Loc.GetString("heretic-character-info-mansus");
        _label = new RichTextLabel
        {
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 8, 0, 8),
        };
        AddChild(_label);
        UpdateLabel();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < _text.Length; i++)
        {
            var hue = (_time * HueSpeed + i / (float)_text.Length) % 1f;
            var color = Color.FromHsv(new Vector4(hue, 1f, 1f, 1f));
            sb.Append($"[color={color.ToHexNoAlpha()}]{_text[i]}[/color]");
        }

        _label.SetMarkup(sb.ToString());
    }
}
